using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gallop.Live;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UmaMusumeAudio;
using Object = UnityEngine.Object;
using M = Gallop.Live.Rendering.OriginalCompositeProducerMath;
using A = Gallop.Live.Rendering.OriginalCompositeCueAdapter;

// Selected-only local PlayMode acceptance. Actual assets and profile stay outside
// the repository. Director supplies time through its normal audio/deltaTime path.
[InitializeOnLoad]
public static class OriginalLivePlaybackValidation
{
    const string Active = "OriginalLivePlaybackValidation.Active";
    static string output;
    static Result result;
    static double started, playbackStarted;
    static OriginalLiveContinuousProvider provider;
    static OriginalLiveContinuousRenderer preview;
    static RenderTexture target;
    static int? selectedCamera;
    static bool ending;
    static Action<int> cameraHandler;
    static Director director;
    static float previousOwnerTime;
    static int previousKey = int.MinValue;
    static readonly HashSet<int> keys = new HashSet<int>();
    static readonly List<Sample> samples = new List<Sample>();
    static readonly HashSet<int> leftKeys = new HashSet<int>();
    static int rightTrack, leftTrack = -1, previousLeftKey = int.MinValue;
    static Func<OriginalLiveTimelineBridge.Frame, Camera> resolveBaseCamera;
    static OriginalLiveChannelEvidence channelEvidence;
    static OriginalNaturalPostProcessProbe frameInputs;
    static OriginalLiveAudioClock audioClock;
    static OriginalLiveSmoothingClock smoothingClock;
    static StreamingAudioCapture pcmCapture;
    static StreamingAudioCapture.Snapshot pcmSnapshot;
    static readonly List<PcmClockSample> pcmSamples = new List<PcmClockSample>();
    static string Argument(string name) => Environment.GetCommandLineArgs().SkipWhile(x => x != name).Skip(1).FirstOrDefault();
    static OriginalLivePlaybackValidation()
    {
        if (SessionState.GetBool(Active, false))
        {
            if (SessionState.GetBool(Active + ".Ending", false))
            { EditorApplication.delayCall += ExitSaved; return; }
            EditorApplication.playModeStateChanged += State;
            EditorApplication.update += Poll;
            started = EditorApplication.timeSinceStartup;
        }
    }
    public static void Run()
    {
        output = Path.GetFullPath(Argument("-liveOutput") ?? throw new ArgumentException("External output required"));
        var repo = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (output.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || output == repo)
            throw new ArgumentException("Keep playback results outside the repository");
        Directory.CreateDirectory(output);
        if (!int.TryParse(Argument("-liveMusicId"), out int id) || id <= 0) throw new ArgumentException("Explicit song required");
        string profile = Argument("-liveProfile");
        if (string.IsNullOrEmpty(profile) || new FileInfo(profile).Length > 65536) throw new ArgumentException("Bounded explicit profile required");
        int selectedRight = Argument("-liveRightTrack") == null ? 0 : int.Parse(Argument("-liveRightTrack"));
        int selectedLeft = Argument("-liveLeftTrack") == null ? -1 : int.Parse(Argument("-liveLeftTrack"));
        if (selectedRight < 0 || selectedRight > 7 || selectedLeft < -1 || selectedLeft > 7 || selectedLeft == selectedRight)
            throw new ArgumentException("Explicit distinct ordered track ordinals required");
        int timeout = Argument("-liveMaximumWallSeconds") == null ? 240 : int.Parse(Argument("-liveMaximumWallSeconds"));
        if (timeout < 240 || timeout > 600) throw new ArgumentOutOfRangeException("-liveMaximumWallSeconds");
        SessionState.SetBool(Active, true);
        SessionState.SetBool(Active + ".Ending", false);
        SessionState.SetString(Active + ".Output", output);
        SessionState.SetInt(Active + ".Music", id);
        SessionState.SetInt(Active + ".Right", selectedRight);
        SessionState.SetInt(Active + ".Left", selectedLeft);
        SessionState.SetString(Active + ".Profile", Path.GetFullPath(profile));
        SessionState.SetBool(Active + ".FrameInputs", Argument("-liveFrameInputs") == "true");
        SessionState.SetBool(Active + ".AudioClock", Argument("-liveAudioClock") == "true");
        bool pcm = Argument("-liveAudioPcm") == "true";
        if (pcm && Argument("-liveAudioClock") != "true") throw new ArgumentException("PCM observation requires the explicit audio clock.");
        SessionState.SetBool(Active + ".AudioPcm", pcm);
        string checkpoint = Argument("-liveClockCheckpoint");
        if (checkpoint != null)
        {
            checkpoint = Path.GetFullPath(checkpoint);
            if (Argument("-liveAudioClock") != "true" || checkpoint == repo ||
                checkpoint.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                new FileInfo(checkpoint).Length > 65536) throw new ArgumentException("Bounded external clock checkpoint requires the audio clock.");
        }
        SessionState.SetString(Active + ".ClockCheckpoint", checkpoint ?? "");
        SessionState.SetBool(Active + ".AudioTail", Argument("-liveAudioClockTailToExistingDirector") == "true");
        SessionState.SetInt(Active + ".MaximumWallSeconds", timeout);
        EditorApplication.playModeStateChanged += State;
        EditorApplication.update += Poll;
        EditorSceneManager.OpenScene("Assets/Scenes/Version2.unity");
        started = EditorApplication.timeSinceStartup;
        EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            output = SessionState.GetString(Active + ".Output", "");
            result = new Result { unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString() };
            started = EditorApplication.timeSinceStartup;
            Application.logMessageReceived += Log;
            try { Begin(); }
            catch (Exception e) { Finish(e.ToString()); }
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Active + ".Ending", false))
        {
            ExitSaved();
        }
    }
    static void ExitSaved()
    {
        output = SessionState.GetString(Active + ".Output", "");
        var saved = JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(output, "playback.private.json")));
        SessionState.SetBool(Active, false); SessionState.SetBool(Active + ".Ending", false);
        EditorApplication.Exit(saved.status == "NaturalViewerPlaybackCompleted" ? 0 : 1);
    }
    static void Begin()
    {
        var main = UmaViewerMain.Instance;
        if (!main || main.AbList == null) throw new InvalidOperationException("Real Viewer startup failed");
        // Avoid unrelated icon/corpus/UI enumeration. Keep the real scene, builder,
        // asset manager, database and live loader; select only one local song.
        main.StopAllCoroutines(); main.enabled = false;
        Config.Instance.DownloadMissingResources = false;
        int id = SessionState.GetInt(Active + ".Music", 0);
        var row = UmaDatabaseController.Instance.LiveData.First(x => Convert.ToInt32(x["music_id"]) == id);
        var settings = UmaAssetManager.LoadAssetBundle(main.AbList["livesettings"]);
        var text = settings.LoadAsset<TextAsset>(id.ToString());
        if (!text) throw new InvalidOperationException("Local song settings missing");
        var song = new LiveEntry(text.text) { MusicId = id, SongName = row["songname"].ToString(),
            MemberCount = Convert.ToInt32(row["live_member_number"]), DefaultDress = Convert.ToInt32(row["default_main_dress"]) };
        if (song.MemberCount < 1 || song.MemberCount > 18) throw new InvalidOperationException("Selected member bound");
        var chara = UmaDatabaseController.Instance.CharaData.First(x => Convert.ToInt32(x["id"]) == 1001);
        var entry = new CharaEntry { Id = Convert.ToInt32(chara["id"]), Name = chara["charaname"].ToString() };
        var slots = new List<LiveCharacterSelect>();
        for (int i = 0; i < song.MemberCount; i++)
        {
            var slot = new GameObject("Selected live member").AddComponent<LiveCharacterSelect>();
            slot.CharaEntry = entry; slot.CostumeId = "0002_00_00"; slot.HeadCostumeId = "";
            slots.Add(slot);
        }
        var selectedData = LiveCharacterLoadData.CaptureAll(slots);
        var roots = Director.GetLivePreloadEntries(song, selectedData, true);
        var all = roots.SelectMany(x => UmaAssetManager.SearchAB(main, x)).GroupBy(x => x.Name).Select(x => x.First()).ToArray();
        result.preloadBundles = all.Length;
        result.preloadBytes = all.Where(x => File.Exists(x.Path)).Sum(x => new FileInfo(x.Path).Length);
        result.missingPreloads = all.Where(x => !File.Exists(x.Path)).Select(x => x.Name).ToArray();
        if (all.Length > 1024 || result.preloadBytes > 1024L * 1024 * 1024 || result.missingPreloads.Length > 0)
            throw new InvalidOperationException("Selected local preload bounds or availability failed");
        UmaViewerUI.Instance.isRequireStage = true;
        UmaViewerUI.Instance.isRecordVMD = false;
        if (SessionState.GetBool(Active + ".AudioPcm", false))
        {
            var audio = UmaViewerAudio.getSoundPath(string.Format("sound/l/{0}/snd_bgm_live_{0}_oke_01", id));
            if (audio.awb == null) throw new InvalidOperationException("Selected accompaniment asset missing.");
            result.pcmAssetPath = audio.awb.FilePath;
            if (new FileInfo(result.pcmAssetPath).Length > 64 * 1024 * 1024)
                throw new InvalidOperationException("Selected accompaniment hash input exceeds the diagnostic limit.");
            using (var file = File.OpenRead(result.pcmAssetPath))
            using (var hash = System.Security.Cryptography.SHA256.Create())
                result.pcmAssetSha256 = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
            pcmCapture = StreamingAudioCapture.Register(audio.awb.Name, 0);
            result.pcmRequested = true;
        }
        UmaViewerBuilder.Instance.LoadLive(song, slots, false, Attach);
    }
    static void Attach(Director loaded)
    {
        director = loaded;
        var p = JsonUtility.FromJson<Profile>(File.ReadAllText(SessionState.GetString(Active + ".Profile", "")));
        var profile = new M.ConfirmedProfile(p.fixedThickness, p.minimumThickness, p.angleDivisor,
            p.secondaryOrigin, p.secondaryScale, p.durationMultiplier, p.kindBases);
        var control = director._liveTimelineControl;
        var tracks = control.data.worksheetList[0].multiCameraPosKeys;
        rightTrack = SessionState.GetInt(Active + ".Right", 0); leftTrack = SessionState.GetInt(Active + ".Left", -1);
        bool dual = leftTrack >= 0;
        if ((!dual && (tracks.Count != 1 || rightTrack != 0)) || tracks.Count > 8 || rightTrack >= tracks.Count || leftTrack >= tracks.Count)
            throw new InvalidOperationException("Explicit channels must match the loaded authored tracks");
        var bindings = new List<OriginalLiveTimelineRegistration.TrackBinding> {
            OriginalLiveTimelineRegistration.TrackBinding.FromTrack(rightTrack, tracks[rightTrack]) };
        if (dual) bindings.Add(OriginalLiveTimelineRegistration.TrackBinding.FromTrack(leftTrack, tracks[leftTrack]));
        result.channelCount = bindings.Count; result.rightTrack = rightTrack; result.leftTrack = leftTrack;
        result.expectedKeyStates = tracks[rightTrack].keys.Count + (dual ? tracks[leftTrack].keys.Count : 0);
        target = new RenderTexture(512, 288, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { name = "Explicit continuous Viewer preview", useMipMap = false, antiAliasing = 1 };
        if (!target.Create()) throw new InvalidOperationException("Preview output unavailable");
        var mapping = OriginalLiveContinuousRenderer.FullViewportMapping(target.height);
        provider = new OriginalLiveContinuousProvider(control, profile, bindings,
            selection => new OriginalLiveDrawInputResolver.Inputs(default, mapping));
        var nodes = (GameObject[])typeof(Director).GetField("_cameraNodes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
        var freeCamera = nodes[0].GetComponentInChildren<Camera>();
        cameraHandler = index => selectedCamera = index;
        control.OnUpdateCameraSwitcher += cameraHandler;
        resolveBaseCamera = frame => !selectedCamera.HasValue ? null : selectedCamera.Value < 0 ? freeCamera : control.GetCamera(selectedCamera.Value)?.camera;
        preview = new OriginalLiveContinuousRenderer(provider, rightTrack, dual ? (int?)leftTrack : null,
            resolveBaseCamera, Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), target, dual);
        if (SessionState.GetBool(Active + ".FrameInputs", false))
        {
            frameInputs = new OriginalNaturalPostProcessProbe(preview);
            preview.SetInputFramePostProcessor(frameInputs.Processor, Shader.Find("Hidden/OriginalRendering/DepthCopy"));
        }
        if (SessionState.GetBool(Active + ".AudioClock", false))
        {
            string checkpointPath = SessionState.GetString(Active + ".ClockCheckpoint", "");
            if (string.IsNullOrEmpty(checkpointPath))
                audioClock = new OriginalLiveAudioClock(director, provider, () =>
                    director && director.liveMusic?.sourceList != null && director.liveMusic.sourceList.Count > 0 ? director.liveMusic.sourceList[0] : null);
            else
            {
                var checkpoint = JsonUtility.FromJson<ClockCheckpoint>(File.ReadAllText(checkpointPath));
                if (checkpoint == null || checkpoint.schema != "ExplicitClockCheckpoint-v1" || checkpoint.state == null ||
                    checkpoint.state.Length != 4 || checkpoint.parameters == null || checkpoint.parameters.Length != 5 ||
                    (checkpoint.smoothing != "Enabled" && checkpoint.smoothing != "Disabled"))
                    throw new ArgumentException("All checkpoint components and an explicit smoothing policy are required.");
                var s = checkpoint.state; var pClock = checkpoint.parameters;
                smoothingClock = new OriginalLiveSmoothingClock(director, provider,
                    () => director && director.liveMusic?.sourceList != null ? director.liveMusic.sourceList.FirstOrDefault() : null,
                    new OriginalLiveClockMath.State(s[0], s[1], s[2], s[3]),
                    new OriginalLiveClockMath.Parameters(checkpoint.smoothing == "Enabled", pClock[0], pClock[1], pClock[2], pClock[3], pClock[4]));
                audioClock = smoothingClock.Clock;
                result.smoothingRequested = true;
                result.clockCheckpoint = "ExplicitCallerCheckpoint_NotOriginalEpoch";
            }
            result.audioClockRequested = true; result.clockSource = "ExplicitCurrentViewerAudioTime";
            if (smoothingClock != null) result.clockSource = "ExplicitConsumedAudioMath_CommitAfterOwnerJoin";
            result.audioClockTailPolicy = SessionState.GetBool(Active + ".AudioTail", false) ? "ReleaseToExistingDirectorAtSourceStop" : "Hold";
        }
        provider.FrameReady += Observe;
        var canvas = new GameObject("Explicit Viewer preview").AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var image = new GameObject("Continuous output").AddComponent<RawImage>(); image.transform.SetParent(canvas.transform, false);
        image.texture = target; image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        result.timeLength = director.totalTime;
        result.modelCount = director.CharaContainerScript.Count;
        result.nativePhysicsSkipped = director.CharaContainerScript.All(x => !x.InitializeNativePhysicsOnLoad &&
            typeof(UmaContainerCharacter).GetField("_cySpringController", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(x) == null &&
            !(bool)typeof(UmaContainerCharacter).GetField("_cySpringLoaded", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(x));
        result.meshRenderers = director.CharaContainerScript.Sum(x => x.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length);
        result.status = "Playing";
        playbackStarted = EditorApplication.timeSinceStartup;
    }
    static void Observe(OriginalLiveContinuousProvider.Frame frame)
    {
        if (audioClock != null)
        {
            result.audioClockReads = audioClock.ReadCount; result.audioClockJoined = audioClock.JoinedFrames;
            result.audioClockHeld = audioClock.HeldReads; result.audioClockRejected = audioClock.RejectedFrames;
            UpdateSmoothingEvidence();
            if (audioClock.LastObservation.HasValue)
            {
                var observed = audioClock.LastObservation.Value;
                result.audioClipLength = observed.Clip.length; result.audioSourcePitch = observed.Source.pitch;
                if (!ReferenceEquals(observed.Selected, frame.Selected) || observed.SampledUnityFrame != Time.frameCount ||
                    preview.LastStatus != "RenderedContinuousViewerCameras") result.audioClockFailures++;
                if (smoothingClock != null && (smoothingClock.CommittedFrames != audioClock.JoinedFrames ||
                    smoothingClock.CommittedState.PreviousSourceTime != observed.SourceTime ||
                    smoothingClock.CommittedState.CurrentTime != observed.OwnerTime)) result.audioClockFailures++;
                if (pcmCapture != null)
                {
                    if (!pcmCapture.MatchesClip(observed.Clip.GetInstanceID()) || pcmSamples.Count >= 16384)
                        result.pcmFailures++;
                    else pcmSamples.Add(new PcmClockSample { serial = frame.Selected.Serial, unityFrame = observed.SampledUnityFrame,
                        readBeginTicks = observed.ReadBeginTicks, readEndTicks = observed.ReadEndTicks,
                        sourceId = observed.Source.GetInstanceID(), clipId = observed.Clip.GetInstanceID(),
                        audioSamples = observed.AudioSamples, sourceTime = observed.SourceTime, ownerTime = observed.OwnerTime,
                        completedCallbackSerial = pcmCapture.LastCompletedSerial });
                }
                if (result.audioClockSamples.Count < 192 && (result.audioClockSamples.Count == 0 || frame.Selected.Serial % 60 == 0))
                    result.audioClockSamples.Add(new AudioClockSample { serial = frame.Selected.Serial, unityFrame = observed.SampledUnityFrame,
                        sourceTime = observed.SourceTime, ownerTime = observed.OwnerTime, audioSamples = observed.AudioSamples, clipFrequency = observed.ClipFrequency,
                        clipName = observed.ClipName, clipSamples = observed.ClipSampleCount, clipChannels = observed.ClipChannels,
                        sourceId = observed.Source.GetInstanceID(), clipId = observed.Clip.GetInstanceID(), renderedCount = preview.RenderCount });
            }
        }
        else if (result.audioClockReleasedAtStop) result.legacyTailFrames++;
        if (result.channelCount == 2) { ObserveTwo(frame); return; }
        result.frames++;
        if (frame.Selections.Count != 1) { result.selectionFailures++; return; }
        var item = frame.Selections[0]; var s = item.Selection;
        if (!s.ComparisonClock.HasValue)
        {
            if (result.validOwnerFrames == 0) result.warmupLateOnlyFrames++;
            else result.clockFailures++;
            return;
        }
        var clock = s.ComparisonClock.Value;
        if (result.validOwnerFrames > 0 && clock.ComparisonOwnerTime != previousOwnerTime) result.clockLinkFailures++;
        result.validOwnerFrames++;
        previousOwnerTime = clock.CurrentOwnerTime;
        result.lastOwnerTime = previousOwnerTime;
        if (clock.CurrentOwnerTime < clock.ComparisonOwnerTime) result.backwardUpdates++;
        keys.Add(s.CurrentKeyFrame);
        bool keyChanged = previousKey != s.CurrentKeyFrame;
        if (keyChanged) { previousKey = s.CurrentKeyFrame; result.keyTransitions++; }
        if (!s.KeyEnabled) result.disabledFrames++;
        float? fade = item.Snapshot.Draw.Fade;
        if (!fade.HasValue) result.unresolvedFadeFrames++;
        else if (s.KeyEnabled && s.RawFadeDuration > 0 && fade.Value > 0 && fade.Value < 1) result.naturalFadeInteriorFrames++;
        if (preview.LastException != null) result.renderFailures++;
        if (provider.FrameCount <= 9000 && (samples.Count < 256) &&
            (samples.Count == 0 || keyChanged || frame.Selected.Serial % 60 == 0 ||
                (s.KeyEnabled && fade.HasValue && fade.Value > 0 && fade.Value < 1 && result.naturalFadeInteriorFrames % 8 == 1)))
            samples.Add(new Sample { serial = frame.Selected.Serial, keyFrame = s.CurrentKeyFrame,
                currentTime = clock.CurrentOwnerTime, comparisonTime = clock.ComparisonOwnerTime,
                enabled = s.KeyEnabled, fade = fade.GetValueOrDefault(), renderStatus = preview.LastStatus });
        if (result.validOwnerFrames == 1 || (result.naturalFadeInteriorFrames == 1) ||
            (result.lastOwnerTime > 30 && !result.middleImage) || (result.lastOwnerTime > result.timeLength - 2 && !result.finalImage))
        {
            SaveImage(result.validOwnerFrames == 1 ? "start" : result.naturalFadeInteriorFrames == 1 ? "natural-fade" :
                result.lastOwnerTime > result.timeLength - 2 ? "end" : "middle");
            if (result.lastOwnerTime > 30) result.middleImage = true;
            if (result.lastOwnerTime > result.timeLength - 2) result.finalImage = true;
        }
        if (result.frames % 60 == 0) Save();
    }
    static void ObserveTwo(OriginalLiveContinuousProvider.Frame frame)
    {
        result.frames++;
        if (frame.Selections.Count != 2) { result.selectionFailures++; return; }
        var r = frame.Selections.Single(x => x.Selection.TrackOrdinal == rightTrack);
        var l = frame.Selections.Single(x => x.Selection.TrackOrdinal == leftTrack);
        bool rc = r.Selection.ComparisonClock.HasValue, lc = l.Selection.ComparisonClock.HasValue;
        if (!rc || !lc)
        {
            if (!rc && !lc && result.validOwnerFrames == 0) result.warmupLateOnlyFrames++;
            else result.clockFailures++;
            return;
        }
        var clock = r.Selection.ComparisonClock.Value; var other = l.Selection.ComparisonClock.Value;
        if (clock.CurrentOwnerTime != other.CurrentOwnerTime || clock.ComparisonOwnerTime != other.ComparisonOwnerTime ||
            clock.Current != other.Current || clock.Comparison != other.Comparison) result.clockLinkFailures++;
        if (result.validOwnerFrames > 0 && clock.ComparisonOwnerTime != previousOwnerTime) result.clockLinkFailures++;
        result.validOwnerFrames++; previousOwnerTime = clock.CurrentOwnerTime; result.lastOwnerTime = previousOwnerTime;
        if (clock.CurrentOwnerTime < clock.ComparisonOwnerTime) result.backwardUpdates++;
        keys.Add(r.Selection.CurrentKeyFrame); leftKeys.Add(l.Selection.CurrentKeyFrame);
        bool changed = previousKey != r.Selection.CurrentKeyFrame || previousLeftKey != l.Selection.CurrentKeyFrame;
        if (previousKey != r.Selection.CurrentKeyFrame) { previousKey = r.Selection.CurrentKeyFrame; result.keyTransitions++; }
        if (previousLeftKey != l.Selection.CurrentKeyFrame) { previousLeftKey = l.Selection.CurrentKeyFrame; result.keyTransitions++; }
        if (!r.Selection.KeyEnabled || !l.Selection.KeyEnabled) result.disabledFrames++;
        if (!r.Snapshot.Draw.Fade.HasValue || !l.Snapshot.Draw.Fade.HasValue) result.unresolvedFadeFrames++;
        if (r.Selection.KeyEnabled && l.Selection.KeyEnabled)
        {
            result.bothEnabledFrames++;
            if (preview.LastStatus == "RenderedContinuousViewerCameras" && preview.LastTransparentChannels == 0) result.bothRenderedFrames++;
            if (channelEvidence == null && result.bothRenderedFrames == 24)
                channelEvidence = new OriginalLiveChannelEvidence(preview, rightTrack, leftTrack, resolveBaseCamera);
        }
        if (preview.LastTransparentChannels != 0 && preview.LastUnresolved != A.Unresolved.None) result.inactiveUnknownPreservedFrames++;
        if (preview.LastException != null) result.renderFailures++;
        if (channelEvidence != null && channelEvidence.Evidence.complete && !result.channelEvidenceCollected)
        { channelEvidence.Save(Path.Combine(output, "channels")); result.channelEvidenceCollected = true; }
        if (samples.Count < 256 && (samples.Count == 0 || changed || frame.Selected.Serial % 60 == 0))
            samples.Add(new Sample { serial = frame.Selected.Serial, keyFrame = r.Selection.CurrentKeyFrame, leftKeyFrame = l.Selection.CurrentKeyFrame,
                currentTime = clock.CurrentOwnerTime, comparisonTime = clock.ComparisonOwnerTime,
                enabled = r.Selection.KeyEnabled, leftEnabled = l.Selection.KeyEnabled, fade = r.Snapshot.Draw.Fade.GetValueOrDefault(),
                leftFade = l.Snapshot.Draw.Fade.GetValueOrDefault(), renderStatus = preview.LastStatus });
        if (result.validOwnerFrames == 1 || (result.lastOwnerTime > 30 && !result.middleImage) || (result.lastOwnerTime > result.timeLength - 2 && !result.finalImage))
        {
            SaveImage(result.validOwnerFrames == 1 ? "start" : result.lastOwnerTime > result.timeLength - 2 ? "end" : "middle");
            if (result.lastOwnerTime > 30) result.middleImage = true;
            if (result.lastOwnerTime > result.timeLength - 2) result.finalImage = true;
        }
        if (result.frames % 60 == 0) Save();
    }
    static void SaveImage(string name)
    {
        var previous = RenderTexture.active;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
        try { RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".private.png"), pixels.EncodeToPNG()); }
        finally { RenderTexture.active = previous; Object.Destroy(pixels); }
    }
    static void Log(string condition, string stack, LogType type)
    {
        if (result != null && type == LogType.Exception)
        { result.exceptions++; if (result.firstException == null) result.firstException = condition + "\n" + stack; }
    }
    static void Poll()
    {
        if (ending || !SessionState.GetBool(Active, false)) return;
        if (EditorApplication.timeSinceStartup - started > SessionState.GetInt(Active + ".MaximumWallSeconds", 240))
        { Finish("Bounded playback timeout"); return; }
        if (result != null && result.exceptions > 0) { Finish(result.firstException); return; }
        if (result == null || provider == null) return;
        if (audioClock != null)
        {
            // Poll counters even when no owner update is emitted during a hold.
            result.audioClockReads = audioClock.ReadCount; result.audioClockJoined = audioClock.JoinedFrames;
            result.audioClockHeld = audioClock.HeldReads; result.audioClockRejected = audioClock.RejectedFrames;
            if (SessionState.GetBool(Active + ".AudioTail", false) && audioClock.HeldReads > 0 &&
                audioClock.LastObservation.HasValue && director && !director.IsRecordVMD &&
                !director.sliderControl.is_Touched && !director.sliderControl.is_Outed)
            {
                var last = audioClock.LastObservation.Value; var source = last.Source;
                if (source && source.clip == last.Clip && !source.isPlaying && !source.loop)
                {
                    result.audioClockReleasedAtStop = true; result.sourceStopUnityFrame = Time.frameCount;
                    result.sourceStopTime = source.time; result.sourceStopSamples = source.timeSamples;
                    result.lastSourceSamples = last.AudioSamples; result.sourceClipSamples = last.Clip.samples;
                    result.tailStartOwnerTime = director._liveCurrentTime;
                    // Explicit test policy only: end this registration. The
                    // existing Director clock then owns the remaining timeline.
                    UpdateSmoothingEvidence(); smoothingClock?.Dispose(); smoothingClock = null;
                    audioClock.Dispose(); audioClock = null;
                    result.clockSource = "ViewerAudioWindowThenExistingDirectorTail_PreviewPolicy";
                }
            }
        }
        result.renderedFrames = preview.RenderCount;
        result.ownerKeys = keys.Count + leftKeys.Count;
        if (provider.ConsumerFaultCount > 0 || provider.Bridge.FaultCount > 0 || result.renderFailures > 0)
        { Finish("Continuous provider, rendering or evidence observation failed"); return; }
        result.audioSources = director ? director.liveMusic.sourceList.Count : result.audioSources;
        if (director && director.liveMusic.sourceList.Count > 0)
        {
            result.audioTime = Math.Max(result.audioTime, director.liveMusic.sourceList[0].time);
            result.audioPlayingObserved |= director.liveMusic.sourceList[0].isPlaying;
        }
        if (director && (bool)typeof(Director).GetField("isExit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director))
            result.naturalExitObserved = true;
        // Wait for the real end-of-song transition and destruction of the live
        // Director. No seek, early final-time assignment or synthetic clock.
        if (result.naturalExitObserved && !director && SceneManager.GetSceneByName("Version2").isLoaded && result.timeLength > 1)
        {
            bool pass = result.frames >= 100 && result.clockFailures == 0 && result.clockLinkFailures == 0 &&
                result.selectionFailures == 0 && result.renderFailures == 0 && result.exceptions == 0 &&
                result.modelCount > 0 && result.meshRenderers > 0 && result.nativePhysicsSkipped &&
                (result.channelCount == 1 ? result.naturalFadeInteriorFrames > 0 && result.ownerKeys >= 3 :
                    result.bothEnabledFrames > 0 && result.bothRenderedFrames == result.bothEnabledFrames &&
                    result.ownerKeys == result.expectedKeyStates && result.channelEvidenceCollected) && result.disabledFrames > 0 &&
                preview.RenderCount > 100 && result.audioPlayingObserved && result.audioTime > 1 &&
                provider.Bridge.FaultCount == 0 && provider.ConsumerFaultCount == 0 && result.lastOwnerTime >= result.timeLength &&
                result.warmupLateOnlyFrames <= 1 && preview.RenderCount == result.validOwnerFrames;
            pass &= frameInputs == null || frameInputs.Passed;
            pass &= !result.audioClockRequested || (result.audioClockJoined + result.legacyTailFrames == result.validOwnerFrames &&
                result.audioClockRejected == 0 && result.audioClockFailures == 0 && result.audioClockReads > 100);
            pass &= !result.smoothingRequested || (result.smoothingCommitted == result.audioClockJoined &&
                result.smoothingDiscarded == 0 && result.smoothingHeld == 0);
            Finish(pass ? null : "Natural playback acceptance predicate failed");
        }
    }
    static void Save()
    {
        if (result == null || string.IsNullOrEmpty(output)) return;
        File.WriteAllText(Path.Combine(output, "playback.private.json"), JsonUtility.ToJson(result, true));
        File.WriteAllText(Path.Combine(output, "samples.private.json"), JsonUtility.ToJson(new Samples { rows = samples.ToArray() }, true));
        frameInputs?.Save(output);
        if (pcmSnapshot != null)
        {
            File.WriteAllText(Path.Combine(output, "pcm-callbacks.private.json"), JsonUtility.ToJson(pcmSnapshot, true));
            File.WriteAllText(Path.Combine(output, "pcm-clock.private.json"), JsonUtility.ToJson(new PcmClockSamples { rows = pcmSamples.ToArray() }, true));
        }
    }
    static void UpdateSmoothingEvidence()
    {
        if (smoothingClock == null) return;
        result.smoothingCommitted = smoothingClock.CommittedFrames;
        result.smoothingDiscarded = smoothingClock.DiscardedCandidates;
        result.smoothingHeld = smoothingClock.HeldEvaluations;
    }
    static void Finish(string failure)
    {
        if (ending) return; ending = true;
        SessionState.SetBool(Active + ".Ending", true);
        if (result == null) result = new Result();
        result.failure = failure; result.status = failure == null ? "NaturalViewerPlaybackCompleted" : "Failed";
        result.playbackWallSeconds = playbackStarted > 0 ? EditorApplication.timeSinceStartup - playbackStarted : 0;
        if (provider != null) { result.providerFaults = provider.Bridge.FaultCount + provider.ConsumerFaultCount; }
        if (pcmCapture != null)
        {
            pcmSnapshot = pcmCapture.StopAndSnapshot();
            result.pcmCallbacks = pcmSnapshot.callbacks.Length; result.pcmClockSamples = pcmSamples.Count;
            bool complete = pcmSnapshot.clipId != 0 && pcmSnapshot.reads > 100 && pcmSnapshot.inFlight == 0 &&
                pcmSnapshot.decoderSamplesPerBlock > 0 && pcmSnapshot.decoderSampleCount == pcmSnapshot.clipSamples &&
                pcmSnapshot.errors == 0 && pcmSnapshot.overlaps == 0 && pcmSnapshot.overflow == 0 &&
                pcmSnapshot.duplicateAttachments == 0 && pcmSnapshot.callbacks.All(x => x.serial > 0 && !x.failed) &&
                pcmSamples.Count == result.audioClockJoined && result.pcmFailures == 0;
            result.pcmComplete = complete;
            if (!complete) { result.status = "Failed"; result.failure = failure ?? "Bounded streaming observation incomplete"; }
        }
        UpdateSmoothingEvidence(); smoothingClock?.Dispose();
        channelEvidence?.Dispose(); frameInputs?.Dispose(); audioClock?.Dispose(); preview?.Dispose(); provider?.Dispose();
        if (director && cameraHandler != null) director._liveTimelineControl.OnUpdateCameraSwitcher -= cameraHandler;
        Save(); Application.logMessageReceived -= Log;
        EditorApplication.ExitPlaymode();
    }
    [Serializable] class Profile { public float fixedThickness, minimumThickness, angleDivisor, secondaryOrigin, secondaryScale, durationMultiplier; public float[] kindBases; }
    [Serializable] class Samples { public Sample[] rows; }
    [Serializable] class Sample { public long serial; public int keyFrame, leftKeyFrame; public float currentTime, comparisonTime, fade, leftFade; public bool enabled, leftEnabled; public string renderStatus; }
    [Serializable] class ClockCheckpoint { public string schema, smoothing; public float[] state, parameters; }
    [Serializable] class AudioClockSample { public long serial; public int unityFrame, audioSamples, clipFrequency, clipSamples, clipChannels, sourceId, clipId, renderedCount; public string clipName; public float sourceTime, ownerTime; }
    [Serializable] class PcmClockSamples { public PcmClockSample[] rows; }
    [Serializable] class PcmClockSample { public long serial, readBeginTicks, readEndTicks; public int unityFrame, sourceId, clipId, audioSamples, completedCallbackSerial; public float sourceTime, ownerTime; }
    [Serializable] class Result
    {
        public string status = "Starting", unity, graphics, failure, firstException;
        public int preloadBundles, modelCount, meshRenderers, frames, validOwnerFrames, warmupLateOnlyFrames, selectionFailures, clockFailures, clockLinkFailures,
            backwardUpdates, keyTransitions, disabledFrames, unresolvedFadeFrames, naturalFadeInteriorFrames, renderFailures,
            renderedFrames, ownerKeys, audioSources, exceptions, providerFaults;
        public int channelCount, rightTrack, leftTrack, expectedKeyStates, bothEnabledFrames, bothRenderedFrames, inactiveUnknownPreservedFrames;
        public long preloadBytes;
        public float timeLength, lastOwnerTime, audioTime;
        public double playbackWallSeconds;
        public string[] missingPreloads;
        public bool nativePhysicsSkipped, audioPlayingObserved, finalImage, middleImage, naturalExitObserved;
        public bool channelEvidenceCollected;
        public bool audioClockRequested;
        public bool pcmRequested, pcmComplete;
        public string pcmAssetPath, pcmAssetSha256;
        public int pcmFailures, pcmCallbacks, pcmClockSamples;
        public long audioClockReads, audioClockJoined, audioClockHeld, audioClockRejected;
        public bool smoothingRequested;
        public string clockCheckpoint;
        public long smoothingCommitted, smoothingDiscarded, smoothingHeld;
        public int audioClockFailures;
        public float audioClipLength, audioSourcePitch;
        public string audioClockTailPolicy;
        public bool audioClockReleasedAtStop;
        public int legacyTailFrames, sourceStopUnityFrame, sourceStopSamples, lastSourceSamples, sourceClipSamples;
        public float sourceStopTime, tailStartOwnerTime;
        public List<AudioClockSample> audioClockSamples = new List<AudioClockSample>();
        public bool OriginalVerified = false, runtime_contract = false, SourceToViewerClockContract = false;
        public string Phase257cause = "Unknown", textureMapping = "ExplicitFullViewportViewerPreview", clockSource = "NaturalViewerDirectorOwnerUpdates";
    }
}
