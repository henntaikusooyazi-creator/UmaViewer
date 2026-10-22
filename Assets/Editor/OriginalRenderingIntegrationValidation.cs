using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gallop.Live;
using Gallop.Live.Cutt;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using A = Gallop.Live.Rendering.OriginalCompositeCueAdapter;
using M = Gallop.Live.Rendering.OriginalCompositeProducerMath;

// Opt-in batch acceptance with artificial geometry and default typed fixtures.
// Optional caller-supplied cue inputs stay separate from natural song playback.
// Uses the Fork's real Control/Director in a transient scene; no saved scene or game asset is changed.
public static class OriginalRenderingIntegrationValidation
{
    [Serializable] sealed class Result
    {
        public string status = "Running", unity, graphics, failure, controlAssembly;
        public int checks, baselineChecks, builtinFrames, urpFrames, providerCalls;
        public int cameraSelectionChecks, cameraSelectionBuiltinFrames, cameraSelectionUrpFrames;
        public int thicknessMathChecks, drawProviderChecks, drawProviderBuiltinFrames, drawProviderUrpFrames, drawProviderCalls;
        public int confirmedProducerChecks;
        public int ownerClockChecks, ownerClockRows;
        public string ownerClockSource;
        public string confirmedProfileSource;
        public int naturalCueChecks, naturalCueRows, naturalCueKeys, naturalCuePositiveDurationKeys;
        public string naturalCueSource = "NotProvided";
        public int naturalFadeChecks, naturalFadeRows, naturalFadeBuiltinFrames, naturalFadeUrpFrames;
        public string naturalFadeSource = "NotProvided";
        public List<CameraSelectionSample> cameraSelection = new List<CameraSelectionSample>();
        public List<DrawProviderSample> drawProvider = new List<DrawProviderSample>();
        public bool TestOnly = true, OriginalVerified = false, runtime_contract = false;
        public string Phase257cause = "Unknown", naturalSongPlayback = "NotTested";
    }
    [Serializable] sealed class CameraSelectionSample
    {
        public string pipeline, requested, selected, directorAtRender, directorAfterUpdate, center, status;
    }
    [Serializable] sealed class DrawProviderSample
    {
        public string pipeline, scenario;
        public float rawRight, rawLeft, effectiveRight, effectiveLeft;
        public bool matchesExplicitReference;
    }
    static Result result;
    [Serializable] sealed class ProducerProfileData
    {
        public float fixedThickness, minimumThickness, angleDivisor, secondaryOrigin, secondaryScale, durationMultiplier;
        public float[] kindBases;
    }
    static ProducerProfileData profileData;
    static M.ConfirmedProfile confirmedProfile;
    [Serializable] sealed class OwnerClockSample
    {
        public int currentTimeBits, comparisonTimeBits, currentFrame, comparisonFrame;
        public int keyFrame, rawDurationBits, kind, inputFadeBits;
        public bool enabled;
    }
    [Serializable] sealed class OwnerClockData
    {
        public float multiplier;
        public OwnerClockSample[] samples;
    }
    static OwnerClockData ownerClockData;
    [Serializable] sealed class NaturalCueTrack
    {
        public int ordinal, compositeIndex;
        public LiveTimelineKeyMultiCameraPositionData[] keys;
    }
    [Serializable] sealed class NaturalCueSample
    {
        public int frame, compositeIndex, pass, fadeBits, angleZBits, angleWBits;
        public int[] rawBits;
    }
    [Serializable] sealed class NaturalCueData
    {
        public NaturalCueTrack[] tracks;
        public NaturalCueSample[] samples;
        public int gpuFrame;
    }
    static NaturalCueData naturalCueData;
    [Serializable] sealed class NaturalFadeData
    {
        public float multiplier;
        public int trackOrdinal, compositeIndex;
        public LiveTimelineKeyMultiCameraPositionData[] keys;
        public OwnerClockSample[] samples;
    }
    static NaturalFadeData naturalFadeData;
    static float FromBits(int bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
    static int FloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
    static readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Vector4 Identity = new Vector4(1, 1, 0, 0);
    static void Check(bool value, string reason)
    { result.checks++; if (!value) throw new InvalidOperationException(reason); }
    static T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
    static GameObject Go(string name, bool active = true)
    { var go = Own(new GameObject(name)); go.SetActive(active); return go; }
    static FieldInfo Field(string name) => typeof(LiveTimelineControl).GetField(name, InstancePrivate);
    static void Tick(LiveTimelineControl control, float frame, bool urp)
    {
        Field("_currentFrame").SetValue(control, frame); control.AlterLateUpdate();
        if (urp) result.urpFrames++; else result.builtinFrames++;
    }
    static RenderTexture Target(int width, int height)
    {
        var rt = Own(new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
        rt.filterMode = FilterMode.Point; rt.wrapMode = TextureWrapMode.Clamp;
        if (!rt.Create()) throw new InvalidOperationException("Synthetic RT creation failed.");
        return rt;
    }
    static Color32[] Read(RenderTexture rt)
    {
        var active = RenderTexture.active; var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
        try { RenderTexture.active = rt; t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply(); return t.GetPixels32(); }
        finally { RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(t); }
    }
    static Camera Configure(Camera camera, int layer)
    {
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1; camera.aspect = 2;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << layer; camera.nearClipPlane = .1f; camera.farClipPlane = 50;
        camera.allowHDR = false; camera.allowMSAA = false;
        camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity); return camera;
    }
    static MultiCamera Multi(int layer)
    { var m = Go("Synthetic multi-camera " + layer).AddComponent<MultiCamera>(); m.Initialize(); Configure(m.GetCamera(), layer); return m; }
    static void Geometry(int layer, Color32 color)
    {
        var go = Own(GameObject.CreatePrimitive(PrimitiveType.Quad)); go.layer = layer;
        go.transform.localScale = new Vector3(4, 2, 1);
        var material = Own(new Material(Shader.Find("Hidden/TestOnly/OriginalRenderingGeometry")));
        material.SetVector("_TestColor", (Color)color); go.GetComponent<Renderer>().sharedMaterial = material;
    }
    static LiveTimelineKeyMultiCameraPositionData Key(int frame, int index, bool next)
    {
        return new LiveTimelineKeyMultiCameraPositionData
        {
            frame = frame, enableMultiCamera = true, position = new Vector3(0, 0, -10),
            nearClip = .1f, farClip = 50, fov = next ? 60 : 40,
            maskOffset = new Vector2(index == 0 ? -.15f : .15f, 0), maskRoll = next ? 30 : 10,
            lineThickness = .1f, LineColor = Color.white, LineAntialiasing = .1f,
            maskType = LiveTimelineKeyMultiCameraPositionData.MaskType.Right,
            LineType = (MultiCameraComposite.DivideLineType)(next ? 1 : 0),
            interpolateType = next ? LiveCameraInterpolateType.Linear : LiveCameraInterpolateType.None
        };
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-integrationOutput");
        if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("-integrationOutput");
        string output = Path.GetFullPath(args[i + 1]); Directory.CreateDirectory(output);
        result = new Result { unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(), controlAssembly = typeof(LiveTimelineControl).Assembly.GetName().Name };
        try
        {
            Check(Application.isBatchMode, "Run acceptance only in its dedicated batch Editor.");
            Check(result.controlAssembly == "umamusume", "Actual Viewer assembly, not a Control stub.");
            Check(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11, "D3D11 device.");
            int profileArg = Array.IndexOf(args, "-confirmedProducerProfile");
            profileData = profileArg >= 0 ? JsonUtility.FromJson<ProducerProfileData>(File.ReadAllText(args[profileArg + 1])) :
                new ProducerProfileData { fixedThickness = -.5f, minimumThickness = .03125f, angleDivisor = 720,
                    secondaryOrigin = 45, secondaryScale = .25f, durationMultiplier = 32,
                    kindBases = new float[] { 0, 0, 0, 45, -45, 90, 135, -135, 225, -225, 0 } };
            result.confirmedProfileSource = profileArg >= 0 ? "ExplicitLocalProfile" : "SyntheticDependencies";
            confirmedProfile = new M.ConfirmedProfile(profileData.fixedThickness, profileData.minimumThickness,
                profileData.angleDivisor, profileData.secondaryOrigin, profileData.secondaryScale,
                profileData.durationMultiplier, profileData.kindBases);
            int clockArg = Array.IndexOf(args, "-confirmedOwnerClock");
            if (clockArg >= 0)
            {
                string clockPath = args[clockArg + 1];
                if (new FileInfo(clockPath).Length > 65536) throw new InvalidOperationException("Bounded owner clock input required.");
                ownerClockData = JsonUtility.FromJson<OwnerClockData>(File.ReadAllText(clockPath));
                result.ownerClockSource = "ExplicitLocalOwnerClock";
            }
            else
            {
                int duration = (int)Math.Floor((double)(.5f * profileData.durationMultiplier));
                ownerClockData = new OwnerClockData { multiplier = 10, samples = new[] {
                    new OwnerClockSample { currentTimeBits = FloatBits(.7f), comparisonTimeBits = FloatBits(.6f),
                        currentFrame = 7, comparisonFrame = 6, keyFrame = 5, rawDurationBits = FloatBits(.5f),
                        kind = 3, enabled = true, inputFadeBits = FloatBits(2f / duration) } } };
                result.ownerClockSource = "SyntheticOwnerInputs";
            }
            if (ownerClockData?.samples == null || ownerClockData.samples.Length < 1 || ownerClockData.samples.Length > 128)
                throw new InvalidOperationException("One to 128 explicit owner-clock samples required.");
            int naturalArg = Array.IndexOf(args, "-confirmedNaturalCue");
            if (naturalArg >= 0)
            {
                string path = args[naturalArg + 1];
                if (new FileInfo(path).Length > 262144) throw new InvalidOperationException("Bounded natural cue input required.");
                naturalCueData = JsonUtility.FromJson<NaturalCueData>(File.ReadAllText(path));
                if (profileArg < 0 || naturalCueData?.tracks == null || naturalCueData.tracks.Length != 2 ||
                    naturalCueData.tracks.Any(t => t == null || t.keys == null || t.keys.Length < 1 || t.keys.Length > 64 ||
                        t.keys.Any(k => k == null || (k.interpolateType == LiveCameraInterpolateType.Curve &&
                            (k.curve == null || k.curve.length == 0)))) ||
                    !naturalCueData.tracks.Select(t => t.ordinal).OrderBy(x => x).SequenceEqual(new[] { 0, 1 }) ||
                    !naturalCueData.tracks.Select(t => t.compositeIndex).OrderBy(x => x).SequenceEqual(new[] { 0, 1 }) ||
                    naturalCueData.samples == null || naturalCueData.samples.Length < 2 || naturalCueData.samples.Length > 256 ||
                    naturalCueData.samples.Any(s => s == null || s.rawBits == null || s.rawBits.Length != 8))
                    throw new InvalidOperationException("Two explicitly bound tracks with nonempty authored curves, bounded samples and a confirmed profile required.");
                result.naturalCueSource = "ExplicitLocalTypedKeysAndObservations";
                result.naturalCueRows = naturalCueData.samples.Length;
                result.naturalCueKeys = naturalCueData.tracks.Sum(t => t.keys.Length);
                result.naturalCuePositiveDurationKeys = naturalCueData.tracks.Sum(t => t.keys.Count(k =>
                    k.enableMultiCamera && M.ResolveDurationCount(k.fadeTime, confirmedProfile.DurationMultiplier) > 0));
            }
            int fadeArg = Array.IndexOf(args, "-confirmedNaturalFade");
            if (fadeArg >= 0)
            {
                string path = args[fadeArg + 1];
                if (new FileInfo(path).Length > 131072) throw new InvalidOperationException("Bounded natural fade input required.");
                naturalFadeData = JsonUtility.FromJson<NaturalFadeData>(File.ReadAllText(path));
                if (profileArg < 0 || naturalFadeData == null || naturalFadeData.trackOrdinal != 0 ||
                    naturalFadeData.compositeIndex < 0 || naturalFadeData.compositeIndex > 1 ||
                    naturalFadeData.keys == null || naturalFadeData.keys.Length < 1 || naturalFadeData.keys.Length > 64 ||
                    naturalFadeData.keys.Any(k => k == null || (k.interpolateType == LiveCameraInterpolateType.Curve &&
                        (k.curve == null || k.curve.length == 0))) ||
                    naturalFadeData.samples == null || naturalFadeData.samples.Length < 2 || naturalFadeData.samples.Length > 128 ||
                    naturalFadeData.samples.Any(s => s == null || !s.enabled || s.kind == 0 ||
                        M.ResolveDurationCount(FromBits(s.rawDurationBits), confirmedProfile.DurationMultiplier) <= 0))
                    throw new InvalidOperationException("Explicit single-track keys, positive duration samples and confirmed dependencies required.");
                result.naturalFadeSource = "ExplicitLocalNaturalKeyAndOwnerObservations";
            }
            else
            {
                // Public artificial dependencies exercise this path without any
                // game data. Owner projection is independent of Viewer frame math.
                int duration = (int)System.Math.Floor((double)profileData.durationMultiplier);
                if (duration < 4) throw new InvalidOperationException("Synthetic positive fade needs a positive duration.");
                var current = Key(8, 0, false); current.fadeTime = 1;
                current.maskType = (LiveTimelineKeyMultiCameraPositionData.MaskType)3;
                var next = Key(1000, 0, false); next.enableMultiCamera = false;
                naturalFadeData = new NaturalFadeData { multiplier = 64, trackOrdinal = 0, compositeIndex = 0,
                    keys = new[] { current, next }, samples = new[] { 2, duration / 2, duration }.Select(delta =>
                        new OwnerClockSample { currentTimeBits = FloatBits((8 + delta) / 64f),
                            comparisonTimeBits = FloatBits((7 + delta) / 64f), currentFrame = 8 + delta,
                            comparisonFrame = 7 + delta, keyFrame = 8, rawDurationBits = FloatBits(1),
                            enabled = true, kind = 3, inputFadeBits = FloatBits((float)delta / duration) }).ToArray() };
                result.naturalFadeSource = "SyntheticPositiveFadeInputs";
            }
            result.naturalFadeRows = naturalFadeData.samples.Length;
            ExerciseConfirmedMath(); ExerciseOwnerClockMath(); ExerciseThicknessMath(); Exercise(false); Exercise(true);
            result.baselineChecks = result.checks - result.cameraSelectionChecks - result.thicknessMathChecks - result.drawProviderChecks - result.confirmedProducerChecks - result.ownerClockChecks - result.naturalCueChecks - result.naturalFadeChecks;
            result.status = "ActualControlIntegrationGPUValidated_TestOnly";
        }
        catch (Exception e) { result.status = "Failed"; result.failure = e.ToString(); Debug.LogException(e); }
        File.WriteAllText(Path.Combine(output, "integration-summary.json"), JsonUtility.ToJson(result, true));
        EditorApplication.Exit(result.status == "ActualControlIntegrationGPUValidated_TestOnly" ? 0 : 1);
    }
    static void Exercise(bool urp)
    {
        var oldScene = SceneManager.GetActiveScene();
        // Unity cannot add a scene beside an untitled startup scene. This process is
        // a dedicated batch Editor, so replace only that transient startup scene.
        bool restoreScene = !string.IsNullOrEmpty(oldScene.path);
        var fixtureScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            restoreScene ? NewSceneMode.Additive : NewSceneMode.Single);
        SceneManager.SetActiveScene(fixtureScene);
        var oldG = GraphicsSettings.defaultRenderPipeline; var oldQ = QualitySettings.renderPipeline;
        var oldActive = RenderTexture.active; bool oldSrgb = GL.sRGBWrite;
        var directorField = typeof(Director).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldDirector = directorField.GetValue(null);
        int before = Resources.FindObjectsOfTypeAll<RenderTexture>().Count(r => r.name.StartsWith("Original."));
        OriginalLiveTimelineRegistration registration = null;
        try
        {
            if (urp)
            {
                var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>());
                var pipeline = Own(UniversalRenderPipelineAsset.Create(data)); pipeline.supportsHDR = false; pipeline.msaaSampleCount = 1;
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            }
            else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
            var director = Go("Synthetic inactive Director", false).AddComponent<Director>(); director.characterCount = 0; directorField.SetValue(null, director);
            var control = Go("Synthetic inactive Control", false).AddComponent<LiveTimelineControl>();
            var sheet = Own(ScriptableObject.CreateInstance<LiveTimelineWorkSheet>());
            foreach (var field in typeof(LiveTimelineWorkSheet).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (field.GetValue(sheet) == null && ((field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>)) || typeof(ILiveTimelineKeyDataList).IsAssignableFrom(field.FieldType)))
                    field.SetValue(sheet, Activator.CreateInstance(field.FieldType));
            sheet.formationOffsetSet = new LiveTimelineFormationOffsetData();
            var timeline = Own(ScriptableObject.CreateInstance<LiveTimelineData>());
            timeline.worksheetList = new List<LiveTimelineWorkSheet> { sheet }; control.data = timeline;
            var baseCamera = Configure(Go("Synthetic base camera").AddComponent<Camera>(), 10); control.SetTimelineCamera(baseCamera, 0);
            var cameras = new[] { Multi(8), Multi(9) }; control.SetMultiCamera(cameras);
            for (int k = 0; k < 2; k++)
            {
                var keys = new LiveTimelineKeyMultiCameraPositionDataList(); keys.thisList.Add(Key(100, k, false)); keys.thisList.Add(Key(110, k, true));
                sheet.multiCameraPosKeys.Add(new LiveTimelineMultiCameraPositionData { MultiCameraNo = k == 0 ? 17 : 4, keys = keys });
            }
            var red = new Color32(220, 40, 10, 255); var blue = new Color32(10, 50, 220, 255); var green = new Color32(20, 140, 50, 255);
            Geometry(8, red); Geometry(9, blue); Geometry(10, green);
            var sourceTarget = Target(16, 8); cameras[0].GetCamera().SetTargetBuffers(sourceTarget.colorBuffer, sourceTarget.depthBuffer);
            var active = RenderTexture.active; RenderTexture.active = sourceTarget; GL.Clear(true, true, Color.magenta); RenderTexture.active = active;
            var sourcePixels = Read(sourceTarget);
            Tick(control, 105, urp);
            Check(control.OriginalCompositeBridge == null && cameras.All(c => c.GetCamera().fieldOfView == 50), "Default-null path preserves actual timeline interpolation.");
            var states = cameras.Select(c => CameraState(c.GetCamera())).ToArray();
            float fade = 1; int calls = 0;
            var resolver = new OriginalLiveDrawInputResolver(selection =>
            {
                calls++; result.providerCalls++;
                return new OriginalLiveDrawInputResolver.Inputs(new A.DrawState(fade, .1f, new Vector2(selection.TrackOrdinal == 0 ? .25f : -.25f, 0)),
                    new A.TextureMapping(Identity, 1, Identity, 0));
            });
            var output = Target(64, 32);
            registration = new OriginalLiveTimelineRegistration(control, 0, 1, _ => baseCamera,
                Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), output, resolver.Resolve);
            bool afterLateUpdate = false; int notifications = 0;
            registration.Bridge.FrameReady += _ => { afterLateUpdate = !(bool)Field("_isNowAlterUpdate").GetValue(control); };
            registration.Bridge.FrameReady += _ => throw new InvalidOperationException("Synthetic subscriber failure");
            registration.Bridge.FrameReady += _ => notifications++;
            Tick(control, 105, urp);
            var session = registration.Session; var frame = registration.Bridge.LastFrame;
            Check(session.RenderCount == 1 && session.LastException == null, "Actual Control -> bridge -> resolver -> camera -> coverage -> compositor.");
            Check(afterLateUpdate && frame.Selections.Count == 2, "Notification after actual late-update completion.");
            Check(frame.Selections[0].Cue.Source.CompositeIndex == 17 && frame.Selections[1].Cue.Source.CompositeIndex == 4, "MultiCameraNo retained independently from k.");
            Check(frame.Selections.All(s => s.CurrentKeyFrame == 100 && s.NextKeyFrame == 110 && s.Cue.AppliedRatio == .5f && s.Cue.Values.RotationSource == 20 && s.Cue.Values.PassRaw == 0), "Actual selected keys, used ratio, interpolation, current raw pass.");
            Check(calls == 2 && frame.Selections.All(s => !s.Cue.TryCreatePass0Parameters(out _, out _)), "Provider called per selection; raw snapshot does not invent draw inputs.");
            Check(cameras.Select(c => CameraState(c.GetCamera())).SequenceEqual(states) && Read(sourceTarget).SequenceEqual(sourcePixels), "Source camera state and buffer contents unchanged.");
            var first = Read(output);
            Check(first[16 * 64 + 3].Equals(blue) && first[16 * 64 + 32].Equals(green) && first[16 * 64 + 60].Equals(red),
                "Explicit geometry reaches left/base/right output regions. Actual=" + first[16 * 64 + 3] + "/" + first[16 * 64 + 32] + "/" + first[16 * 64 + 60] +
                "; expected=" + blue + "/" + green + "/" + red + "; camera=" + CameraState(cameras[0].GetCamera()));
            Check(registration.Bridge.FaultCount == 1 && notifications == 1, "Subscriber failure isolated from Control and later handlers.");
            bool refused = false;
            try { using (var other = new OriginalLiveTimelineRegistration(control, 0, 1, _ => baseCamera, Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), output, resolver.Resolve)) { } }
            catch (InvalidOperationException) { refused = true; }
            Check(refused && ReferenceEquals(control.OriginalCompositeBridge, registration.Bridge), "Existing registration ownership retained.");
            fade = .5f; sheet.multiCameraPosKeys[0].keys.thisList[0].maskOffset += new Vector2(.04f, 0);
            Tick(control, 105, urp);
            Check(calls == 4 && session.RenderCount == 2 && !Read(output).SequenceEqual(first), "New provider state reaches next frame without caching.");
            Check(frame.Selections[0].Cue.Values.Offset != registration.Bridge.LastFrame.Selections[0].Cue.Values.Offset && frame.Selections[0].Cue.Source.CurrentKeyToken == registration.Bridge.LastFrame.Selections[0].Cue.Source.CurrentKeyToken, "Immutable snapshots and stable key identity.");
            var replacements = new[] { Multi(8), Multi(9) }; control.SetMultiCamera(replacements); Tick(control, 105, urp);
            Check(ReferenceEquals(registration.Bridge.LastFrame.Selections[0].ViewerCamera, replacements[0].GetCamera()) && session.RenderCount == 3, "Actual SetMultiCamera replacement reaches registered session.");
            if (!urp) { cameras[0].GetCamera().Render(); Check(Read(sourceTarget).All(p => p.Equals(red)), "Original SetTargetBuffers binding still works."); }
            var preserved = Read(output); int rendered = session.RenderCount;
            Tick(control, 110, urp);
            Check(session.RenderCount == rendered && session.LastStatus == "UnresolvedDrawInputs" && session.LastUnresolved.HasFlag(A.Unresolved.OtherPassRequired) && Read(output).SequenceEqual(preserved), "Unsupported requested pass is preserved; output not silently replaced.");
            registration.Dispose(); registration = null;
            Check(control.OriginalCompositeBridge == null && output.IsCreated(), "Dispose releases only owned session resources.");
            Tick(control, 105, urp); Check(session.RenderCount == rendered && Read(output).SequenceEqual(preserved), "Disposed session no longer renders.");
            using (var unknown = new OriginalLiveTimelineRegistration(control, 0, 1, _ => baseCamera,
                Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), output, null))
            { Tick(control, 105, urp); Check(unknown.Session.RenderCount == 0 && unknown.Session.LastStatus == "UnresolvedDrawInputs" && Read(output).SequenceEqual(preserved), "Missing values remain Unknown without affecting existing output."); }
            Check(control.OriginalCompositeBridge == null && !(bool)Field("_isNowAlterUpdate").GetValue(control), "Existing Control finishes after registration disposal.");
            ExerciseCameraSelection(control, director, sheet, urp);
            ExerciseDrawProvider(control, sheet, urp);
            ExerciseConfirmedProvider(control, sheet, urp);
            ExerciseOwnerClockProvider(control, sheet, urp);
            if (naturalCueData != null) ExerciseNaturalCueAngles(control, sheet, urp);
            if (naturalFadeData != null) ExerciseNaturalFade(control, sheet, urp);
        }
        finally
        {
            registration?.Dispose(); directorField.SetValue(null, oldDirector);
            QualitySettings.renderPipeline = oldQ; GraphicsSettings.defaultRenderPipeline = oldG;
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) UnityEngine.Object.DestroyImmediate(owned[i]);
            owned.Clear(); RenderTexture.active = oldActive; GL.sRGBWrite = oldSrgb;
            if (restoreScene) { SceneManager.SetActiveScene(oldScene); EditorSceneManager.CloseScene(fixtureScene, true); }
            Check(before == Resources.FindObjectsOfTypeAll<RenderTexture>().Count(r => r.name.StartsWith("Original.")) && !Resources.FindObjectsOfTypeAll<Camera>().Any(c => c.name == "Original.ExplicitCamera"), "Owned camera/RT cleanup: " + urp);
        }
    }
    static void ExerciseNaturalFade(LiveTimelineControl control, LiveTimelineWorkSheet sheet, bool urp)
    {
        int start = result.checks;
        var bridge = new OriginalLiveTimelineBridge();
        int renders = 0;
        try
        {
            Check(control.OriginalCompositeBridge == null, "Natural fade fixture owns only its explicit bridge.");
            sheet.multiCameraPosKeys = new List<LiveTimelineMultiCameraPositionData> {
                new LiveTimelineMultiCameraPositionData { MultiCameraNo = naturalFadeData.compositeIndex,
                    keys = new LiveTimelineKeyMultiCameraPositionDataList { thisList = naturalFadeData.keys.ToList() } } };
            bridge.BindTrack(naturalFadeData.trackOrdinal, naturalFadeData.compositeIndex);
            control.OriginalCompositeBridge = bridge;
            bridge.UseConfirmedOwnerClock(naturalFadeData.multiplier);
            Texture2D Solid(Color value)
            {
                var texture = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false, true));
                texture.SetPixel(0, 0, value); texture.Apply(); return texture;
            }
            var input = Solid(new Color(1, .125f, .25f, 1));
            var background = Solid(new Color(.125f, .5f, .75f, 1));
            var empty = Solid(Color.clear); var output = Target(64, 32);
            bool mapping = false, reference = false;
            OwnerClockSample sample = naturalFadeData.samples[0];
            A.Snapshot resolved = default;
            A.Unresolved missing = A.Unresolved.None;
            using (var coverage = new OriginalMaskCoverageGenerator(Shader.Find(OriginalMaskCoverageGenerator.ShaderName)))
            using (var renderer = new OriginalMultiCameraRenderer(Shader.Find(OriginalMultiCameraRenderer.ShaderName), RenderTextureFormat.ARGBFloat))
            {
                var resolver = new OriginalLiveDrawInputResolver(selection =>
                    new OriginalLiveDrawInputResolver.Inputs(
                        new A.DrawState(reference ? (float?)FromBits(sample.inputFadeBits) : null, null, null),
                        mapping ? (A.TextureMapping?)new A.TextureMapping(Identity, 1, Identity, 0) : null)
                    .WithConfirmedProfile(confirmedProfile));
                bridge.FrameReady += frame =>
                {
                    if (!frame.TryGetTrack(naturalFadeData.trackOrdinal, out var selection)) return;
                    resolved = resolver.Resolve(selection);
                    if (!resolved.TryCreatePass0Parameters(out var parameters, out missing)) return;
                    // A single natural Selection supplies the active mask. The
                    // second channel is explicitly transparent synthetic input.
                    renderer.Render(background, coverage.GenerateSubmission(input, 64, 32, parameters),
                        new OriginalMultiCameraRenderer.Submission(empty,
                            OriginalMultiCameraRenderer.InputEncoding.StraightColorWithCoverage), output);
                    renders++;
                };
                void OwnerTick(OwnerClockSample value)
                {
                    sample = value;
                    Field("_currentLiveTime").SetValue(control, FromBits(value.comparisonTimeBits));
                    control.AlterUpdate(FromBits(value.currentTimeBits)); control.AlterLateUpdate();
                    Check(bridge.FaultCount == 0, "Natural owner update completes without bridge faults.");
                }
                OwnerTick(sample);
                Check(renders == 0 && missing == A.Unresolved.TextureMapping &&
                    FloatBits(resolved.Draw.Fade.Value) == sample.inputFadeBits,
                    "Natural owner/key fade resolves while missing mapping still withholds GPU output.");
                mapping = true;
                A.Snapshot firstSnapshot = default; Color32[] firstPixels = null, lastPixels = null;
                foreach (var value in naturalFadeData.samples)
                {
                    reference = false; OwnerTick(value);
                    var selection = bridge.LastFrame.Selections.Single();
                    var clock = selection.ComparisonClock.Value;
                    Check(selection.CurrentKeyFrame == value.keyFrame && selection.KeyEnabled == value.enabled &&
                        FloatBits(selection.RawFadeDuration) == value.rawDurationBits && selection.Cue.Values.Kind == value.kind &&
                        selection.Cue.Source.CompositeIndex == naturalFadeData.compositeIndex,
                        "Actual Control selects the independently sampled natural key and explicit composite binding.");
                    Check(clock.Current == value.currentFrame && clock.Comparison == value.comparisonFrame &&
                        FloatBits(clock.CurrentOwnerTime) == value.currentTimeBits &&
                        FloatBits(clock.ComparisonOwnerTime) == value.comparisonTimeBits,
                        "The same Selection preserves both exact native owner times and comparison integers.");
                    Check(resolved.Draw.Fade.HasValue && FloatBits(resolved.Draw.Fade.Value) == value.inputFadeBits &&
                        missing == A.Unresolved.None,
                        "Natural key duration and owner pair generate the exact original setter fade bits.");
                    var actualPixels = Read(output);
                    if (firstPixels == null) { firstPixels = actualPixels; firstSnapshot = resolved; }
                    lastPixels = actualPixels;
                    reference = true; OwnerTick(value);
                    Check(Read(output).SequenceEqual(actualPixels),
                        "Generated natural fade and independently saved setter fade match every GPU RGBA pixel: " + urp);
                }
                Check(renders == naturalFadeData.samples.Length * 2 && !firstPixels.SequenceEqual(lastPixels),
                    "Natural fade progresses through distinct GPU images to its sampled endpoint: " + urp);
                Check(FloatBits(firstSnapshot.Draw.Fade.Value) == naturalFadeData.samples[0].inputFadeBits,
                    "Earlier natural fade snapshots remain immutable.");
                reference = false; bridge.ClearConfirmedOwnerClock(); var preserved = Read(output); int before = renders;
                OwnerTick(naturalFadeData.samples.Last());
                Check(renders == before && missing == A.Unresolved.Fade && !resolved.Draw.Fade.HasValue &&
                    Read(output).SequenceEqual(preserved), "Cleared natural owner clock preserves output without a Viewer-frame fallback.");
            }
        }
        finally
        {
            if (ReferenceEquals(control.OriginalCompositeBridge, bridge)) control.OriginalCompositeBridge = null;
            result.naturalFadeChecks += result.checks - start;
            if (urp) result.naturalFadeUrpFrames += renders; else result.naturalFadeBuiltinFrames += renders;
        }
    }

    static void ExerciseNaturalCueAngles(LiveTimelineControl control, LiveTimelineWorkSheet sheet, bool urp)
    {
        int start = result.checks;
        try
        {
            var samples = naturalCueData.samples.ToDictionary(s => (s.frame, s.compositeIndex));
            var frames = naturalCueData.samples.Select(s => s.frame).Distinct().OrderBy(f => f).ToArray();
            Check(frames.All(f => samples.ContainsKey((f, 0)) && samples.ContainsKey((f, 1))) &&
                samples.ContainsKey((naturalCueData.gpuFrame, 0)), "Every observed frame has both explicit composite bindings.");
            foreach (var track in naturalCueData.tracks)
            {
                var target = sheet.multiCameraPosKeys[track.ordinal];
                target.MultiCameraNo = track.compositeIndex;
                target.keys.thisList = track.keys.ToList();
            }
            // Flat source colors and geometry remain artificial; only typed cue
            // inputs and independent receiver observations come from the caller.
            var sources = new[] { Multi(8), Multi(9) };
            sources[0].GetCamera().backgroundColor = new Color32(220, 30, 40, 255);
            sources[1].GetCamera().backgroundColor = new Color32(30, 40, 220, 255);
            foreach (var source in sources) source.GetCamera().cullingMask = 0;
            control.SetMultiCamera(sources);
            var baseCamera = Configure(Go("Synthetic natural cue base").AddComponent<Camera>(), 10);
            baseCamera.cullingMask = 0; baseCamera.backgroundColor = new Color32(20, 180, 40, 255);
            bool render = false, reference = false;
            int frame = 0;
            var resolved = new Dictionary<int, A.Snapshot>();
            var resolver = new OriginalLiveDrawInputResolver(selection =>
            {
                if (!render) return new OriginalLiveDrawInputResolver.Inputs(default, null).WithConfirmedProfile(confirmedProfile);
                var sample = samples[(frame, selection.Cue.Source.CompositeIndex.Value)];
                var draw = new A.DrawState(FromBits(sample.fadeBits), null,
                    reference ? (Vector2?)new Vector2(FromBits(sample.angleZBits), FromBits(sample.angleWBits)) : null);
                return new OriginalLiveDrawInputResolver.Inputs(draw, new A.TextureMapping(Identity, 1, Identity, 0))
                    .WithConfirmedProfile(confirmedProfile);
            });
            var output = Target(64, 32);
            using (var registration = new OriginalLiveTimelineRegistration(control, 0, 1, _ => baseCamera,
                Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), output,
                s => { var value = resolver.Resolve(s); resolved[s.Cue.Source.CompositeIndex.Value] = value; return value; }))
            {
                void CueTick(int value)
                { frame = value; Field("_currentFrame").SetValue(control, (float)value); control.AlterLateUpdate(); }
                foreach (int value in frames)
                {
                    resolved.Clear(); CueTick(value);
                    Check(resolved.Count == 2 && registration.Bridge.FaultCount == 0,
                        "Real Control selects both supplied natural cue tracks.");
                    foreach (int j in new[] { 0, 1 })
                    {
                        var actual = resolved[j]; var sample = samples[(value, j)]; var cue = actual.Values;
                        var color = cue.LineColor.Value; var offset = cue.Offset.Value;
                        var raw = new[] { cue.Thickness.Value, color.r, color.g, color.b, color.a,
                            cue.LineAntialiasing.Value, offset.x, offset.y };
                        Check(raw.Select(FloatBits).SequenceEqual(sample.rawBits) && actual.WirePass == sample.pass,
                            "Selected raw cue components and pass match the independent saved receiver.");
                        Check(actual.Draw.ShaderAngles.HasValue && FloatBits(actual.Draw.ShaderAngles.Value.x) == sample.angleZBits &&
                            FloatBits(actual.Draw.ShaderAngles.Value.y) == sample.angleWBits,
                            "Selected natural cue -> confirmed producer -> shader angles matches both receiver components bit for bit.");
                        Check(!actual.Draw.Fade.HasValue && !actual.Texture.HasValue && !registration.Bridge.LastFrame.Selections[j].ComparisonClock.HasValue,
                            "Observed cue F does not synthesize owner time, fade or TextureMapping.");
                    }
                }
                Check(registration.Session.RenderCount == 0, "Missing natural clock and mapping preserve the GPU output.");
                render = true; CueTick(naturalCueData.gpuFrame); var actualPixels = Read(output);
                Check(registration.Session.RenderCount == 1 && registration.Session.LastException == null &&
                    actualPixels.Distinct().Count() > 1, "Real cue angles reach a nonuniform synthetic GPU image: " + urp);
                reference = true; CueTick(naturalCueData.gpuFrame);
                Check(registration.Session.RenderCount == 2 && Read(output).SequenceEqual(actualPixels),
                    "Producer angles and independent receiver angles match at every GPU RGBA pixel: " + urp);
            }
        }
        finally { result.naturalCueChecks += result.checks - start; }
    }

    static void ExerciseOwnerClockMath()
    {
        int start = result.checks;
        try
        {
            var seek = M.ResolveOwnerComparisonFrames(.2f, 1.5f, 10);
            Check(seek.Current == 2 && seek.Comparison == 15, "Owner comparison time is independent during a backward seek.");
            var paused = M.ResolveOwnerComparisonFrames(.7f, .7f, 10);
            Check(paused.Current == 7 && paused.Comparison == 7, "Repeated owner time is preserved without a synthetic frame increment.");
            var negative = M.ResolveOwnerComparisonFrames(-.01f, 0, 10);
            Check(negative.Current == -1 && negative.Comparison == 0, "Owner time projection uses floor without a nonnegative clamp.");
            foreach (var sample in ownerClockData.samples)
            {
                var projected = M.ResolveOwnerComparisonFrames(FromBits(sample.currentTimeBits),
                    FromBits(sample.comparisonTimeBits), ownerClockData.multiplier);
                Check(projected.Current == sample.currentFrame && projected.Comparison == sample.comparisonFrame,
                    "Saved independent owner integers match both projected owner-time fields.");
                int duration = M.ResolveDurationCount(FromBits(sample.rawDurationBits), confirmedProfile.DurationMultiplier);
                var fade = M.EvaluateFadeAfterResolvedDuration(projected.Current, projected.Comparison,
                    sample.keyFrame, duration, sample.enabled, sample.kind);
                Check(FloatBits(fade.Fade) == sample.inputFadeBits, "Saved original setter input matches the owner-clock fade calculation bit for bit.");
            }
            result.ownerClockRows = ownerClockData.samples.Length;
        }
        finally { result.ownerClockChecks += result.checks - start; }
    }

    static void ExerciseOwnerClockProvider(LiveTimelineControl control, LiveTimelineWorkSheet sheet, bool urp)
    {
        int start = result.checks;
        try
        {
            var sample = ownerClockData.samples[0];
            // Saved owner inputs drive the real AlterUpdate. Geometry and keys in
            // this GPU acceptance remain explicit artificial fixtures.
            int keyFrame = unchecked(sample.currentFrame - 5);
            for (int k = 0; k < 2; k++)
            {
                var keys = sheet.multiCameraPosKeys[k].keys.thisList;
                keys[0].frame = keyFrame; keys[0].enableMultiCamera = true; keys[0].fadeTime = .5f;
                keys[0].maskType = (LiveTimelineKeyMultiCameraPositionData.MaskType)3;
                keys[1].frame = unchecked(sample.currentFrame + 50); keys[1].interpolateType = 0;
            }
            Field("_motionSequenceArray").SetValue(control,
                Array.CreateInstance(Field("_motionSequenceArray").FieldType.GetElementType(), 0));
            sheet.facial1Set = null;
            var otherFacial = typeof(LiveTimelineWorkSheet).GetField("other4FacialArray");
            otherFacial.SetValue(sheet, Array.CreateInstance(otherFacial.FieldType.GetElementType(), 0));
            int duration = (int)System.Math.Floor((double)(.5f * profileData.durationMultiplier));
            float expectedFade = 5f / duration;
            bool reference = false, partial = false, explicitPair = false, evaluated = false;
            var resolved = new Dictionary<int, A.Snapshot>();
            var resolver = new OriginalLiveDrawInputResolver(s =>
            {
                var draw = new A.DrawState(evaluated ? (float?).75f : reference ? expectedFade : (float?)null,
                    .1f, new Vector2(.25f, 0));
                return new OriginalLiveDrawInputResolver.Inputs(draw, new A.TextureMapping(Identity, 1, Identity, 0))
                    .WithConfirmedProfile(confirmedProfile,
                        partial || explicitPair ? (int?)keyFrame : null,
                        explicitPair ? (int?)sample.comparisonFrame : null);
            });
            var output = Target(64, 32);
            using (var registration = new OriginalLiveTimelineRegistration(control, 0, 1, _ => control.GetCamera(0).camera,
                Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), output,
                s => { var value = resolver.Resolve(s); resolved[s.TrackOrdinal] = value; return value; }))
            {
                void OwnerTick()
                {
                    Field("_currentLiveTime").SetValue(control, FromBits(sample.comparisonTimeBits));
                    control.AlterUpdate(FromBits(sample.currentTimeBits));
                    control.AlterLateUpdate();
                }
                registration.Bridge.UseConfirmedOwnerClock(ownerClockData.multiplier);
                Field("_currentFrame").SetValue(control, (float)sample.currentFrame);
                control.AlterLateUpdate();
                Check(registration.Session.RenderCount == 0 && !registration.Bridge.LastFrame.Selections[0].ComparisonClock.HasValue,
                    "Opt in waits for an owner update; Viewer frame and serial do not supply missing clocks.");
                OwnerTick();
                var selected = registration.Bridge.LastFrame.Selections[0];
                var clock = selected.ComparisonClock.Value;
                Check(clock.Current == sample.currentFrame && clock.Comparison == sample.comparisonFrame &&
                    FloatBits(clock.CurrentOwnerTime) == sample.currentTimeBits && FloatBits(clock.ComparisonOwnerTime) == sample.comparisonTimeBits,
                    "Real AlterUpdate supplies both exact owner-time fields to the same Selection.");
                Check(registration.Session.RenderCount == 1 && registration.Session.LastException == null &&
                    FloatBits(resolved[0].Draw.Fade.Value) == FloatBits(expectedFade),
                    "Owner update -> comparison pair -> resolver -> GPU: " + urp);
                var actual = Read(output); reference = true; OwnerTick(); reference = false;
                Check(Read(output).SequenceEqual(actual), "Owner-clock GPU matches independent evaluated fade at every RGBA pixel: " + urp);
                partial = true; int rendered = registration.Session.RenderCount; var prior = Read(output);
                OwnerTick();
                Check(registration.Session.RenderCount == rendered && registration.Session.LastUnresolved.HasFlag(A.Unresolved.Fade) && Read(output).SequenceEqual(prior),
                    "A partial explicit pair never borrows its other half from the owner snapshot.");
                partial = false; explicitPair = true; OwnerTick();
                Check(FloatBits(resolved[0].Draw.Fade.Value) == FloatBits(0), "A complete explicit comparison pair keeps priority over owner time.");
                explicitPair = false; evaluated = true; OwnerTick();
                Check(resolved[0].Draw.Fade == .75f, "Existing evaluated fade keeps priority over owner clock.");
                evaluated = false; registration.Bridge.ClearConfirmedOwnerClock();
                rendered = registration.Session.RenderCount; prior = Read(output); OwnerTick();
                Check(registration.Session.RenderCount == rendered && !registration.Bridge.LastFrame.Selections[0].ComparisonClock.HasValue && Read(output).SequenceEqual(prior),
                    "Clearing the opt-in clock restores the missing-input path and preserves output.");
                Check(selected.ComparisonClock.Value.Current == sample.currentFrame && selected.ComparisonClock.Value.Comparison == sample.comparisonFrame,
                    "Previously selected clock snapshots remain immutable across later updates and clearing.");
            }
        }
        finally { result.ownerClockChecks += result.checks - start; }
    }

    static void ExerciseConfirmedMath()
    {
        int start = result.checks;
        try
        {
            Check(M.ResolveDurationCount(.7f, 10) == 7, "Duration multiplication rounds to float32 before widening and floor.");
            Check(M.ResolveDurationCount(-.01f, 10) == -1, "Negative duration floors toward minus infinity.");
            Check(M.ResolveDurationCount(0, 60) == 0 && M.ResolveDurationCount(-0f, 60) == 0, "Signed-zero durations convert to integer zero.");
            Check(M.ResolveDurationCount(float.NaN, 1) == int.MinValue, "NaN uses x64 indefinite integer.");
            Check(M.ResolveDurationCount(float.PositiveInfinity, 1) == int.MinValue && M.ResolveDurationCount(float.NegativeInfinity, 1) == int.MinValue, "Infinite durations use x64 indefinite integer.");
            Check(M.ResolveDurationCount(2147483648f, 1) == int.MinValue && M.ResolveDurationCount(-2147483648f, 1) == int.MinValue, "Conversion upper/lower endpoints preserve x64 behavior.");
            Check(BitConverter.ToInt32(BitConverter.GetBytes(M.EvaluateFadeAfterResolvedDuration(102, 101, 100, 3, false, 5).Fade), 0) == 0x3eaaaaaa,
                "Falling fade rounds the scalar division before subtraction, matching the saved SSE order.");
            Check(confirmedProfile.KindBase(-1) == 0 && confirmedProfile.KindBase(11) == 0 && !confirmedProfile.KindBase(null).HasValue, "Unsigned helper bounds and missing kind are separate.");
            var table = (float[])profileData.kindBases.Clone();
            var cloned = new M.ConfirmedProfile(1, 2, 3, 4, 5, 6, table);
            float first = table[3]; table[3] += 1;
            Check(cloned.KindBase(3) == first, "Caller table mutation cannot change a profile.");
            bool rejected = false;
            try { new M.ConfirmedProfile(1, 2, 3, 4, 5, 6, new float[10]); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Incomplete kind table is rejected.");
            var angles = confirmedProfile.Angles(3, new M.AngleInputs(17, 23, 29, 31));
            Check(angles.KindBase == 17 && angles.Divisor == 23 && angles.SecondaryOrigin == 29 && angles.SecondaryScale == 31, "Explicit angle dependencies retain priority.");
        }
        finally { result.confirmedProducerChecks += result.checks - start; }
    }

    static void ExerciseConfirmedProvider(LiveTimelineControl control, LiveTimelineWorkSheet sheet, bool urp)
    {
        int start = result.checks;
        try
        {
            bool reference = false, clock = true, evaluated = false;
            var resolved = new Dictionary<int, A.Snapshot>();
            for (int k = 0; k < 2; k++)
            {
                var keys = sheet.multiCameraPosKeys[k].keys.thisList;
                keys[0].maskType = (LiveTimelineKeyMultiCameraPositionData.MaskType)(3 + k);
                keys[0].lineThickness = .0001f; keys[1].lineThickness = .0003f;
                keys[0].fadeTime = .5f; keys[1].fadeTime = 9;
                keys[0].enableMultiCamera = true;
            }
            int count = (int)Math.Floor((double)(.5f * profileData.durationMultiplier));
            float expectedFade = 5f / count;
            float expectedThickness = .0002f > profileData.minimumThickness ? .0002f : profileData.minimumThickness;
            int FloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            var resolver = new OriginalLiveDrawInputResolver(s =>
            {
                var expectedAngles = new Vector2((profileData.kindBases[s.Cue.Values.Kind.Value] + 20) / profileData.angleDivisor, 0);
                if (reference) return new OriginalLiveDrawInputResolver.Inputs(
                    new A.DrawState(evaluated ? .75f : expectedFade, evaluated ? .25f : expectedThickness,
                        evaluated ? new Vector2(.125f, 0) : expectedAngles), new A.TextureMapping(Identity, 1, Identity, 0));
                var supplied = evaluated ? new A.DrawState(.75f, .25f, new Vector2(.125f, 0)) : default;
                return new OriginalLiveDrawInputResolver.Inputs(supplied, new A.TextureMapping(Identity, 1, Identity, 0))
                    .WithConfirmedProfile(confirmedProfile, clock ? (int?)105 : null, clock ? (int?)104 : null);
            });
            var output = Target(64, 32);
            using (var registration = new OriginalLiveTimelineRegistration(control, 0, 1, _ => control.GetCamera(0).camera,
                Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), output,
                s => { var value = resolver.Resolve(s); if (!reference) resolved[s.TrackOrdinal] = value; return value; }))
            {
                void TickConfirmed() { Field("_currentFrame").SetValue(control, 105f); control.AlterLateUpdate(); }
                TickConfirmed();
                Check(registration.Session.RenderCount == 1 && registration.Session.LastException == null, "Selected keys -> confirmed profile -> coverage -> GPU output: " + urp);
                Check(FloatBits(resolved[0].Draw.Fade.Value) == FloatBits(expectedFade) &&
                    FloatBits(resolved[0].Draw.EffectiveThickness.Value) == FloatBits(expectedThickness) &&
                    FloatBits(resolved[0].Draw.ShaderAngles.Value.x) == FloatBits((profileData.kindBases[3] + 20) / profileData.angleDivisor),
                    "Natural producer dependencies fill all three draw components with identical float32 bits.");
                var selected = registration.Bridge.LastFrame.Selections[0];
                Check(selected.RawFadeDuration == .5f && selected.KeyEnabled && selected.CurrentKeyFrame == 100 && selected.Cue.AppliedRatio == .5f,
                    "Fade uses immutable current key state rather than the interpolated next key.");
                var actual = Read(output); reference = true; TickConfirmed(); reference = false;
                Check(Read(output).SequenceEqual(actual), "Profile output matches independent evaluated input at every RGBA pixel: " + urp);
                clock = false; int rendered = registration.Session.RenderCount; var previous = Read(output);
                TickConfirmed();
                Check(registration.Session.RenderCount == rendered && registration.Session.LastUnresolved.HasFlag(A.Unresolved.Fade) && Read(output).SequenceEqual(previous),
                    "Missing explicit comparison clock preserves output; no ViewerFrame conversion.");
                evaluated = true; TickConfirmed();
                Check(registration.Session.RenderCount == rendered + 1 && resolved[0].Draw.Fade == .75f && resolved[0].Draw.EffectiveThickness == .25f && resolved[0].Draw.ShaderAngles == new Vector2(.125f, 0),
                    "Existing evaluated values override the confirmed profile without a clock.");
            }
        }
        finally { result.confirmedProducerChecks += result.checks - start; }
    }

    static void ExerciseThicknessMath()
    {
        int firstCheck = result.checks;
        try
        {
            A.CueValues Values(float? thickness, int? kind) => new A.CueValues(thickness, null, null, Vector2.zero, null, null, kind, 0);
            var inputs = new M.ThicknessInputs(.5f, .125f);
            Check(M.EvaluateEffectiveThickness(Values(.25f, 5), inputs) == .25f, "Nonzero kind uses cue thickness above the explicit minimum.");
            Check(M.EvaluateEffectiveThickness(Values(.0625f, 4), inputs) == .125f, "Cue below minimum uses the explicit minimum.");
            Check(M.EvaluateEffectiveThickness(Values(null, 1), new M.ThicknessInputs(.5f, null)) == .5f, "Fixed branch needs neither raw thickness nor minimum.");
            Check(!M.EvaluateEffectiveThickness(Values(.25f, 0), inputs).HasValue && !M.EvaluateEffectiveThickness(Values(.25f, null), inputs).HasValue,
                "Time-dependent or absent kind is unresolved without explicit receiver state.");
            Check(M.EvaluateEffectiveThickness(Values(.25f, 0), new M.ThicknessInputs(null, .125f, true)) == .25f &&
                M.EvaluateEffectiveThickness(Values(.25f, 5), new M.ThicknessInputs(.5f, null, false)) == .5f, "Explicit receiver branch takes priority over the kind path.");
            Check(!M.EvaluateEffectiveThickness(Values(null, 5), inputs).HasValue &&
                !M.EvaluateEffectiveThickness(Values(.25f, 5), new M.ThicknessInputs(.5f, null)).HasValue &&
                !M.EvaluateEffectiveThickness(Values(.25f, 1), new M.ThicknessInputs(null, .125f)).HasValue, "Only dependencies used by the selected branch are required.");
            float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000000)), 0);
            int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            Check(Bits(M.EvaluateEffectiveThickness(Values(0, 5), new M.ThicknessInputs(null, negativeZero)).Value) == Bits(negativeZero) &&
                Bits(M.EvaluateEffectiveThickness(Values(negativeZero, 5), new M.ThicknessInputs(null, 0)).Value) == 0, "Equal signed zeros select the second operand.");
            float nan = BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x7fc12345)), 0);
            Check(M.EvaluateEffectiveThickness(Values(nan, 5), inputs) == .125f &&
                Bits(M.EvaluateEffectiveThickness(Values(.25f, 5), new M.ThicknessInputs(null, nan)).Value) == Bits(nan), "Unordered max selects the second operand without a finite-value guard.");
            Check(M.EvaluateEffectiveThickness(Values(float.PositiveInfinity, 5), inputs) == float.PositiveInfinity &&
                M.EvaluateEffectiveThickness(Values(float.NegativeInfinity, 5), inputs) == .125f, "Infinity follows the existing scalar selection.");
            var context = new A.Context(7, 2, 11, 12, 13);
            var selected = A.FromEvaluatedProducer(context, Values(.25f, 5), default, null);
            var legacy = OriginalLiveDrawInputResolver.Complete(selected, new OriginalLiveDrawInputResolver.Inputs(default, null));
            Check(!legacy.Draw.EffectiveThickness.HasValue, "Old constructor does not enable raw-thickness fallback.");
            var filled = OriginalLiveDrawInputResolver.Complete(selected, new OriginalLiveDrawInputResolver.Inputs(default, null, default, null, inputs));
            Check(filled.Draw.EffectiveThickness == .25f && !filled.Draw.Fade.HasValue && !filled.Draw.ShaderAngles.HasValue && !filled.Texture.HasValue &&
                filled.Source.TrackOrdinal == 7 && filled.Source.CompositeIndex == 2 && filled.Source.UpdateToken == 13, "Partial completion keeps other Unknown inputs and identity.");
            var evaluated = new OriginalLiveDrawInputResolver.Inputs(new A.DrawState(null, .375f, null), null, default, null, inputs);
            Check(OriginalLiveDrawInputResolver.Complete(selected, evaluated).Draw.EffectiveThickness == .375f &&
                OriginalLiveDrawInputResolver.Complete(selected.WithDrawState(new A.DrawState(null, .75f, null)), evaluated).Draw.EffectiveThickness == .75f,
                "Snapshot and supplied evaluated thickness retain priority over new arithmetic.");
        }
        finally { result.thicknessMathChecks += result.checks - firstCheck; }
    }

    static void ExerciseDrawProvider(LiveTimelineControl control, LiveTimelineWorkSheet sheet, bool urp)
    {
        int firstCheck = result.checks;
        try
        {
            sheet.cameraSwitcherKeys.thisList.Clear();
            void Keys(float current, float next, int rightKind = 5, int leftKind = 4)
            {
                for (int k = 0; k < 2; k++)
                {
                    var keys = sheet.multiCameraPosKeys[k].keys.thisList;
                    keys[0].lineThickness = current; keys[1].lineThickness = next;
                    keys[0].maskType = (LiveTimelineKeyMultiCameraPositionData.MaskType)(k == 0 ? rightKind : leftKind);
                }
            }
            Keys(.0625f, .1875f);
            bool reference = false, supplyFade = true, supplyAngles = true, explicitBranches = false;
            float expectedRight = .125f, expectedLeft = .125f;
            float? suppliedThickness = null, minimum = .03125f, fixedValue = .375f;
            var resolved = new Dictionary<int, A.Snapshot>();
            var resolver = new OriginalLiveDrawInputResolver(selection =>
            {
                result.drawProviderCalls++;
                bool right = selection.TrackOrdinal == 0;
                if (reference) return new OriginalLiveDrawInputResolver.Inputs(
                    new A.DrawState(1, right ? expectedRight : expectedLeft, new Vector2(right ? .25f : -.25f, 0)), new A.TextureMapping(Identity, 1, Identity, 0));
                // All constants are synthetic, explicitly supplied dependencies.
                // Angles use the actual interpolated RotationSource; no kind table or unit is inferred.
                var angles = supplyAngles ? new M.AngleInputs(right ? 5 : -45, 100) : default;
                var thickness = new M.ThicknessInputs(fixedValue, minimum, explicitBranches ? (bool?)right : null);
                return new OriginalLiveDrawInputResolver.Inputs(new A.DrawState(supplyFade ? (float?)1 : null, suppliedThickness, null),
                    new A.TextureMapping(Identity, 1, Identity, 0), angles, null, thickness);
            });
            var output = Target(64, 32);
            using (var registration = new OriginalLiveTimelineRegistration(control, 0, 1, _ => control.GetCamera(0).camera,
                Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), output, selection =>
                {
                    var snapshot = resolver.Resolve(selection);
                    if (!reference) resolved[selection.TrackOrdinal] = snapshot;
                    return snapshot;
                }))
            {
                void TickProvider()
                {
                    Field("_currentFrame").SetValue(control, 105f); control.AlterLateUpdate();
                    if (urp) result.drawProviderUrpFrames++; else result.drawProviderBuiltinFrames++;
                }
                Color32[] MatchReference(string scenario, float right, float left)
                {
                    expectedRight = right; expectedLeft = left; resolved.Clear(); reference = false;
                    int renders = registration.Session.RenderCount, calls = result.drawProviderCalls;
                    TickProvider();
                    Check(registration.Session.RenderCount == renders + 1 && registration.Session.LastException == null && result.drawProviderCalls == calls + 2,
                        "Actual Control selection calls provider per track and renders: " + scenario);
                    Check(resolved[0].Draw.EffectiveThickness == right && resolved[1].Draw.EffectiveThickness == left &&
                        resolved[0].Draw.ShaderAngles == new Vector2(.25f, 0) && resolved[1].Draw.ShaderAngles == new Vector2(-.25f, 0) &&
                        resolved[0].AppliedRatio == .5f && resolved[0].Source.CompositeIndex == 17 && resolved[1].Source.CompositeIndex == 4,
                        "Selected cue math reaches draw inputs with original ratio and independent k/j: " + scenario);
                    var actual = Read(output); reference = true; TickProvider(); reference = false;
                    bool matches = Read(output).SequenceEqual(actual);
                    Check(registration.Session.RenderCount == renders + 2 && matches, "All output pixels match the old evaluated-input route: " + scenario);
                    result.drawProvider.Add(new DrawProviderSample { pipeline = urp ? "URP14" : "Built-in", scenario = scenario,
                        rawRight = resolved[0].Values.Thickness.Value, rawLeft = resolved[1].Values.Thickness.Value,
                        effectiveRight = right, effectiveLeft = left, matchesExplicitReference = matches });
                    return actual;
                }
                void Preserve(A.Unresolved missing, string scenario)
                {
                    reference = false; var previous = Read(output); int renders = registration.Session.RenderCount;
                    TickProvider();
                    Check(registration.Session.RenderCount == renders && registration.Session.LastStatus == "UnresolvedDrawInputs" &&
                        registration.Session.LastUnresolved.HasFlag(missing) && Read(output).SequenceEqual(previous), "Incomplete provider preserves output: " + scenario);
                }
                var first = MatchReference("interpolated cue thickness", .125f, .125f);
                Keys(.1875f, .3125f);
                var changed = MatchReference("updated selected keys", .25f, .25f);
                Check(!changed.SequenceEqual(first), "Current update thickness changes GPU output without a provider cache.");
                Keys(.03125f, .0625f); minimum = .125f;
                MatchReference("explicit minimum", .125f, .125f);
                Keys(.03125f, .0625f, 1, 1);
                MatchReference("fixed branch", .375f, .375f);
                Keys(.03125f, .0625f, 0, 0);
                Preserve(A.Unresolved.EffectiveThickness, "time-dependent branch not supplied");
                explicitBranches = true;
                MatchReference("explicit receiver branches", .125f, .375f);
                explicitBranches = false; suppliedThickness = .0625f; minimum = null; fixedValue = null;
                MatchReference("evaluated thickness priority", .0625f, .0625f);
                suppliedThickness = null; Keys(.0625f, .1875f);
                Preserve(A.Unresolved.EffectiveThickness, "minimum missing");
                minimum = .03125f; supplyFade = false;
                Preserve(A.Unresolved.Fade, "fade remains explicit");
                supplyFade = true; supplyAngles = false;
                Preserve(A.Unresolved.ShaderAngles, "angle dependencies missing");
            }
        }
        finally { result.drawProviderChecks += result.checks - firstCheck; }
    }
    static void ExerciseCameraSelection(LiveTimelineControl control, Director director, LiveTimelineWorkSheet sheet, bool urp)
    {
        int firstCheck = result.checks;
        Action<int> rememberSwitch = null;
        try
        {
            // Execute the real Director subscription and index mapping. Reflection
            // supplies scene references only; no replacement switch handler is used.
            var colors = new[] { new Color32(180, 90, 20, 255), new Color32(20, 140, 50, 255),
                new Color32(210, 180, 30, 255), new Color32(20, 180, 180, 255) };
            var main = new Camera[4];
            for (int i = 0; i < main.Length; i++)
            {
                int layer = i == 1 ? 10 : 11 + i;
                main[i] = Configure(Go("Synthetic main camera " + i).AddComponent<Camera>(), layer);
                if (i != 1) Geometry(layer, colors[i]);
            }
            typeof(Director).GetField("_cameraNodes", InstancePrivate).SetValue(director, main.Select(c => c.gameObject).ToArray());
            typeof(Director).GetField("_enableMirrorReflection", InstancePrivate).SetValue(director, false);
            director._liveTimelineControl = control; director.charaObjs = new List<Transform>(); director.charaAnims = new List<Animation>();
            control.data.characterSettings = new LiveTimelineCharacterSettings { motionSequenceIndices = new int[0] };
            control.data.multiCameraSettings = new LiveTimelineMultiCameraSettings { cameraNum = 2 };
            director.InitializeTimeline(new List<LiveCharacterLoadData>(), 1);
            var multi = control.GetComponentsInChildren<MultiCamera>(true);
            Configure(multi[0].GetCamera(), 8); Configure(multi[1].GetCamera(), 9);
            Check(Enumerable.Range(0, 3).All(i => ReferenceEquals(control.GetCamera(i).camera, main[i + 1])) &&
                ReferenceEquals(director.MainCameraTransform, main[1].transform), "Real Director registers Control indices 0/1/2 as main cameras 1/2/3.");

            int? selectedIndex = null; int eventVersion = 0, resolverVersion = 0;
            bool switchDuringLateUpdate = false, resolverDuringLateUpdate = true;
            Camera explicitFreeCamera = main[0], selected = null;
            Transform directorAtRender = null;
            rememberSwitch = index =>
            {
                selectedIndex = index; eventVersion++;
                switchDuringLateUpdate = (bool)Field("_isNowAlterUpdate").GetValue(control);
            };
            control.OnUpdateCameraSwitcher += rememberSwitch;
            var resolver = new OriginalLiveDrawInputResolver(selection =>
                new OriginalLiveDrawInputResolver.Inputs(new A.DrawState(1, .1f, new Vector2(selection.TrackOrdinal == 0 ? .25f : -.25f, 0)),
                    new A.TextureMapping(Identity, 1, Identity, 0)));
            var output = Target(64, 32);
            using (var registration = new OriginalLiveTimelineRegistration(control, 0, 1, _ =>
            {
                resolverVersion = eventVersion; directorAtRender = director.MainCameraTransform;
                resolverDuringLateUpdate = (bool)Field("_isNowAlterUpdate").GetValue(control);
                selected = !selectedIndex.HasValue ? null : selectedIndex.Value < 0 ? explicitFreeCamera : control.GetCamera(selectedIndex.Value)?.camera;
                return selected;
            }, Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), output, resolver.Resolve))
            {
                var updateMain = typeof(Director).GetMethod("UpdateMainCamera", InstancePrivate);
                Action tick = () =>
                {
                    Field("_currentFrame").SetValue(control, 105f); control.AlterLateUpdate();
                    if (urp) result.cameraSelectionUrpFrames++; else result.cameraSelectionBuiltinFrames++;
                };
                var preserved = Read(output);
                tick();
                Check(eventVersion == 0 && selected == null && registration.Session.RenderCount == 0 &&
                    registration.Session.LastStatus == "MissingSelectedCamera" && Read(output).SequenceEqual(preserved),
                    "Before a switch notification, no default camera is invented and output is retained.");

                var key = new LiveTimelineKeyCameraSwitcherData { frame = 100, cameraIndex = 0 };
                sheet.cameraSwitcherKeys.thisList.Add(key);
                foreach (int index in new[] { 0, 1, 2, -1 })
                {
                    key.cameraIndex = index;
                    Camera expected = index < 0 ? main[0] : main[index + 1];
                    Transform previousMain = director.MainCameraTransform;
                    var states = main.Select(CameraState).ToArray();
                    int renders = registration.Session.RenderCount, events = eventVersion;
                    tick();
                    Check(eventVersion == events + 1 && resolverVersion == eventVersion && switchDuringLateUpdate && !resolverDuringLateUpdate,
                        "Switch notification precedes rendering after Control late-update completion: " + index);
                    Check(ReferenceEquals(selected, expected) && registration.Session.RenderCount == renders + 1 && registration.Session.LastException == null,
                        "Same-update selection uses exact Control camera or explicit free camera: " + index);
                    var center = Read(output)[16 * 64 + 32];
                    Check(center.Equals(colors[index < 0 ? 0 : index + 1]), "Selected base camera geometry reaches the GPU output in the same update: " + index +
                        "; actual=" + center + "; expected=" + colors[index < 0 ? 0 : index + 1]);
                    Check(ReferenceEquals(directorAtRender, previousMain) && ReferenceEquals(director.MainCameraTransform, previousMain) &&
                        main.Select(CameraState).SequenceEqual(states), "Rendering leaves source cameras and the still-old Director main reference unchanged: " + index);
                    updateMain.Invoke(director, null);
                    Check(ReferenceEquals(director.MainCameraTransform, expected.transform) && main.All(c => c.gameObject.activeSelf == ReferenceEquals(c, expected)),
                        "Real later UpdateMainCamera applies the switch, without moving the bridge notification: " + index);
                    result.cameraSelection.Add(new CameraSelectionSample { pipeline = urp ? "URP14" : "Built-in", requested = index.ToString(),
                        selected = selected.name, directorAtRender = directorAtRender.name, directorAfterUpdate = director.MainCameraTransform.name,
                        center = center.ToString(), status = registration.Session.LastStatus });
                }
                // Invalid indices retain the Director's existing choice, but are
                // unresolved for this explicitly supplied renderer selection.
                foreach (int index in new[] { 99, -1 })
                {
                    key.cameraIndex = index;
                    if (index < 0) explicitFreeCamera = null;
                    preserved = Read(output); int renders = registration.Session.RenderCount;
                    tick();
                    Check(selected == null && registration.Session.RenderCount == renders && registration.Session.LastStatus == "MissingSelectedCamera" &&
                        Read(output).SequenceEqual(preserved), "Invalid index or missing explicit free camera preserves output without fallback: " + index);
                    updateMain.Invoke(director, null);
                    Check(ReferenceEquals(director.MainCameraTransform, main[0].transform), "Original Director behavior is retained when explicit selection is unresolved: " + index);
                }
            }
        }
        finally
        {
            if (rememberSwitch != null) control.OnUpdateCameraSwitcher -= rememberSwitch;
            result.cameraSelectionChecks += result.checks - firstCheck;
        }
    }
    static string CameraState(Camera c) => string.Join("|", c.enabled, c.targetTexture, c.fieldOfView, c.nearClipPlane, c.farClipPlane, c.cullingMask, c.rect, c.aspect, c.transform.position, c.transform.rotation);
}
