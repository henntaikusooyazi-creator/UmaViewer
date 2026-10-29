using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Gallop.Live;
using Gallop.Live.Cutt;
using Gallop.Live.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;
using M = Gallop.Live.Rendering.OriginalCompositeProducerMath;
using A = Gallop.Live.Rendering.OriginalCompositeCueAdapter;

// Public artificial acceptance; no asset catalog, audio, native physics or private profile.
public static class OriginalLiveContinuousValidation
{
    public static int Exercise()
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
        var owned = new List<Object>();
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        GameObject Go(string name) { var go = Own(new GameObject(name)); go.SetActive(false); return go; }
        var directorField = typeof(Director).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
        var oldDirector = directorField.GetValue(null);
        try
        {
            var director = Go("Synthetic continuous Director").AddComponent<Director>();
            directorField.SetValue(null, director);
            var control = Go("Synthetic continuous Control").AddComponent<LiveTimelineControl>();
            var sheet = Own(ScriptableObject.CreateInstance<LiveTimelineWorkSheet>());
            foreach (var field in typeof(LiveTimelineWorkSheet).GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (field.GetValue(sheet) == null && ((field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>)) || typeof(ILiveTimelineKeyDataList).IsAssignableFrom(field.FieldType)))
                    field.SetValue(sheet, Activator.CreateInstance(field.FieldType));
            sheet.formationOffsetSet = new LiveTimelineFormationOffsetData(); sheet.facial1Set = null;
            var facial = typeof(LiveTimelineWorkSheet).GetField("other4FacialArray");
            facial.SetValue(sheet, Array.CreateInstance(facial.FieldType.GetElementType(), 0));
            var timeline = Own(ScriptableObject.CreateInstance<LiveTimelineData>());
            timeline.worksheetList = new List<LiveTimelineWorkSheet> { sheet }; control.data = timeline;
            var motion = typeof(LiveTimelineControl).GetField("_motionSequenceArray", BindingFlags.Instance | BindingFlags.NonPublic);
            motion.SetValue(control, Array.CreateInstance(motion.FieldType.GetElementType(), 0));
            Camera Camera(string name, Color color)
            {
                var camera = Go(name).AddComponent<Camera>(); camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = color; camera.cullingMask = 0;
                camera.nearClipPlane = .1f; camera.farClipPlane = 50; camera.allowHDR = false; camera.allowMSAA = false;
                return camera;
            }
            var background = Camera("Synthetic base", Color.green); control.SetTimelineCamera(background, 0);
            var multi = new MultiCamera[2];
            for (int i = 0; i < 2; i++)
            {
                multi[i] = Go("Synthetic authored camera").AddComponent<MultiCamera>(); multi[i].Initialize();
                multi[i].GetCamera().clearFlags = CameraClearFlags.SolidColor;
                multi[i].GetCamera().backgroundColor = i == 0 ? Color.red : Color.blue;
                multi[i].GetCamera().cullingMask = 0;
                var list = new LiveTimelineKeyMultiCameraPositionDataList();
                foreach (int frame in new[] { 0, 30, 120, 240 })
                    list.thisList.Add(new LiveTimelineKeyMultiCameraPositionData {
                        frame = frame, enableMultiCamera = frame == 30, fadeTime = frame == 0 || frame == 240 ? 0 : 1,
                        position = new Vector3(0, 0, -10), nearClip = .1f, farClip = 50, fov = 40,
                        maskType = (LiveTimelineKeyMultiCameraPositionData.MaskType)3,
                        maskOffset = Vector2.zero, lineThickness = .1f, LineColor = Color.white });
                sheet.multiCameraPosKeys.Add(new LiveTimelineMultiCameraPositionData { MultiCameraNo = i == 0 ? 17 : 4, keys = list });
            }
            control.SetMultiCamera(multi);
            var profile = new M.ConfirmedProfile(.1f, .02f, 720, 45, .25f, 64, new float[11]);
            var binding = new[] { OriginalLiveTimelineRegistration.TrackBinding.FromTrack(0, sheet.multiCameraPosKeys[0]) };
            bool rejected = false;
            try { new OriginalLiveContinuousProvider(control, profile, new[] { new OriginalLiveTimelineRegistration.TrackBinding(0, 0) }); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && control.OriginalCompositeBridge == null, "Mismatched authored binding rejected without registration.");
            foreach (float multiplier in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                rejected = false;
                try { new OriginalLiveContinuousProvider(control, new M.ConfirmedProfile(.1f, .02f, 720, 45, .25f, multiplier, new float[11]), binding); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected && control.OriginalCompositeBridge == null, "Invalid owner multiplier cannot register a clock.");
            }
            var target = Own(new RenderTexture(64, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
            target.Create();
            Color32[] Read(RenderTexture from = null)
            {
                var active = RenderTexture.active; var pixels = new Texture2D(64, 32, TextureFormat.RGBA32, false, true);
                try { RenderTexture.active = from ? from : target; pixels.ReadPixels(new Rect(0, 0, 64, 32), 0, 0); pixels.Apply(); return pixels.GetPixels32(); }
                finally { RenderTexture.active = active; Object.DestroyImmediate(pixels); }
            }
            bool supplyMapping = true; Camera selected = background;
            using (var provider = new OriginalLiveContinuousProvider(control, profile, binding, s =>
                new OriginalLiveDrawInputResolver.Inputs(default, supplyMapping ? (A.TextureMapping?)OriginalLiveContinuousRenderer.FullViewportMapping(32) : null)))
            using (var preview = new OriginalLiveContinuousRenderer(provider, 0, null, f => selected,
                Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), target))
            {
                rejected = false;
                try { new OriginalLiveContinuousProvider(control, profile, binding); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && ReferenceEquals(control.OriginalCompositeBridge, provider.Bridge), "Existing owner cannot be replaced.");
                control.AlterLateUpdate();
                Check(provider.LastFrame.Selections.Count == 1 && !provider.LastFrame.Selections[0].Selection.ComparisonClock.HasValue && preview.RenderCount == 0,
                    "Initial late-only update is unresolved; no original clock or pixels invented.");
                void Tick(float time) { control.AlterUpdate(time); control.AlterLateUpdate(); }
                Tick(0);
                Check(!provider.LastFrame.Selections[0].Selection.KeyEnabled && provider.LastFrame.Selections[0].Snapshot.Draw.Fade == 0 && preview.RenderCount == 1,
                    "Disabled authored key is supplied and clears the overlay to the base image.");
                var basePixels = Read();
                Check(basePixels.All(x => x.Equals(new Color32(0, 255, 0, 255))), "Disabled zero fade produces the exact base RGBA image.");
                Tick(.25f); Tick(.5f);
                var saved = provider.LastFrame;
                var item = saved.Selections[0];
                Check(item.Selection.CurrentKeyFrame == 30 && item.Selection.Cue.Source.CompositeIndex == 17 && item.Snapshot.Draw.Fade == 2f / 64f,
                    "Natural selection uses the explicit multiplier, distinct from Viewer FPS.");
                Check(item.Selection.ComparisonClock.Value.CurrentOwnerTime == .5f && item.Selection.ComparisonClock.Value.ComparisonOwnerTime == .25f,
                    "Both actual owner times come from adjacent AlterUpdate calls.");
                Check(saved.Selections.Count == 1 && saved.Selected.Selections.Count == 2, "Only the explicitly bound track is resolved.");
                Check(!Read().SequenceEqual(basePixels), "Current enabled fade changes actual output pixels.");
                int renders = preview.RenderCount;
                control.AlterLateUpdate();
                Check(!provider.LastFrame.Selections[0].Selection.ComparisonClock.HasValue && preview.RenderCount == renders,
                    "Repeated late update cannot reuse a consumed owner pair.");
                Tick(1); Check(provider.LastFrame.Selections[0].Snapshot.Draw.Fade == 34f / 64f && preview.RenderCount == renders + 1, "Next owner update resumes current fade.");
                Tick(1); Check(provider.LastFrame.Selections[0].Selection.ComparisonClock.Value.ComparisonOwnerTime == 1, "Paused owner times remain an explicit same-time pair.");
                Tick(2); Check(!provider.LastFrame.Selections[0].Selection.KeyEnabled && provider.LastFrame.Selections[0].Snapshot.Draw.Fade == .875f, "Disabled positive duration begins the authored falling transition.");
                Tick(3); Check(provider.LastFrame.Selections[0].Snapshot.Draw.Fade == 0, "Falling transition reaches zero without preserving enabled key state.");
                Tick(.5f); Check(provider.LastFrame.Selections[0].Selection.ComparisonClock.Value.ComparisonOwnerTime == 3 && provider.LastFrame.Selections[0].Snapshot.Draw.Fade == 2f / 64f,
                    "Backward seek retains the real previous owner time and reselects the key.");
                Check(saved.Selections[0].Selection.ComparisonClock.Value.ComparisonOwnerTime == .25f && saved.Selections[0].Snapshot.Draw.Fade == 2f / 64f,
                    "Earlier immutable snapshots remain unchanged.");
                supplyMapping = false; renders = preview.RenderCount; var preserved = Read(); Tick(1);
                Check(preview.RenderCount == renders && preview.LastStatus == "UnresolvedDrawInputs" && provider.LastFrame.Selections[0].Snapshot.Draw.Fade.HasValue,
                    "Missing mapping holds pixels while current owner fade remains known.");
                Check(Read().SequenceEqual(preserved), "Unresolved mapping preserves every RGBA pixel.");
                supplyMapping = true; selected = null; Tick(1.1f);
                Check(preview.RenderCount == renders && preview.LastStatus == "MissingSelectedCamera", "No camera fallback is invented.");
                Check(Read().SequenceEqual(preserved), "Missing camera preserves every RGBA pixel.");
                selected = background; multi[0].GetCamera().rect = new Rect(0, 0, .5f, 1); Tick(1.2f);
                Check(preview.RenderCount == renders && preview.LastStatus == "UnsupportedViewerViewport", "Full texture policy rejects an incompatible viewport.");
                multi[0].GetCamera().rect = new Rect(0, 0, 1, 1);
                int observed = 0;
                provider.FrameReady += f => throw new InvalidOperationException("Synthetic subscriber failure");
                provider.FrameReady += f => observed++;
                Tick(1.3f);
                Check(provider.ConsumerFaultCount == 1 && observed == 1 && provider.Bridge.FaultCount == 0 && preview.RenderCount == renders + 1,
                    "Consumer failure is isolated from rendering and later handlers.");
                Check(target.IsCreated() && !multi[0].GetCamera().enabled && multi[0].GetCamera().targetTexture == null,
                    "Source camera and caller target stay owned by their caller.");
            }
            Check(control.OriginalCompositeBridge == null && target.IsCreated(), "Disposal detaches only the owned registration and preserves caller output.");
            var both = new[] { binding[0], OriginalLiveTimelineRegistration.TrackBinding.FromTrack(1, sheet.multiCameraPosKeys[1]) };
            for (int i = 0; i < 2; i++)
            {
                sheet.multiCameraPosKeys[i].keys.thisList[0].maskType = 0;
                sheet.multiCameraPosKeys[i].keys.thisList[1].maskOffset = new Vector2(i == 0 ? -.25f : .25f, 0);
            }
            using (var provider = new OriginalLiveContinuousProvider(control, profile, both, s =>
                new OriginalLiveDrawInputResolver.Inputs(new A.DrawState(null, null, new Vector2(s.TrackOrdinal == 0 ? .25f : -.25f, 0)),
                    OriginalLiveContinuousRenderer.FullViewportMapping(32))))
            using (var preview = new OriginalLiveContinuousRenderer(provider, 0, 1, f => background,
                Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), target))
            {
                control.AlterUpdate(.5f); control.AlterLateUpdate();
                Check(provider.LastFrame.Selections.Count == 2 && preview.RenderCount == 1 && preview.LastException == null,
                    "Two explicitly bound artificial channels reach their sequential GPU submissions.");
                var preserved = Read(); control.AlterUpdate(0); control.AlterLateUpdate();
                Check(preview.RenderCount == 1 && preview.LastStatus == "UnresolvedDrawInputs" && Read().SequenceEqual(preserved),
                    "Existing constructor still holds unknown kind-0 thickness on an inactive key.");
                var optTarget = Own(new RenderTexture(64, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)); optTarget.Create();
                using (var opt = new OriginalLiveContinuousRenderer(provider, 0, 1, f => background,
                    Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), optTarget, true))
                {
                    control.AlterUpdate(0); control.AlterLateUpdate();
                    Check(opt.RenderCount == 1 && opt.LastTransparentChannels == 3 && (opt.LastUnresolved & A.Unresolved.EffectiveThickness) != 0 &&
                        !provider.LastFrame.Selections[0].Snapshot.Draw.EffectiveThickness.HasValue,
                        "Explicit inactive-zero policy renders transparently while preserving unknown thickness.");
                    Check(Read(optTarget).All(x => x.Equals(new Color32(0, 255, 0, 255))), "Two transparent inactive channels reveal the exact base RGBA image.");
                    var key = sheet.multiCameraPosKeys[0].keys.thisList[0]; key.enableMultiCamera = true;
                    control.AlterUpdate(0); control.AlterLateUpdate();
                    Check(opt.RenderCount == 1 && opt.LastStatus == "UnresolvedDrawInputs", "Unknown active kind-0 state is still rejected.");
                    key.enableMultiCamera = false; key.LineType = (MultiCameraComposite.DivideLineType)1;
                    control.AlterUpdate(0); control.AlterLateUpdate();
                    Check(opt.RenderCount == 1 && (opt.LastUnresolved & A.Unresolved.OtherPassRequired) != 0, "Inactive zero does not bypass another pass.");
                    key.LineType = 0; provider.Bridge.ClearConfirmedOwnerClock(); control.AlterUpdate(0); control.AlterLateUpdate();
                    Check(opt.RenderCount == 1 && (opt.LastUnresolved & A.Unresolved.Fade) != 0, "Unknown fade never becomes an assumed transparent channel.");
                    provider.Bridge.UseConfirmedOwnerClock(64); control.AlterUpdate(0); control.AlterLateUpdate();
                    Check(opt.RenderCount == 2 && opt.LastException == null, "Known inactive-zero owner input resumes the explicit policy.");
                    background.backgroundColor = new Color(0, 1, 0, 0);
                    using (var evidence = new OriginalLiveChannelEvidence(opt, 0, 1, f => background))
                    {
                        control.AlterUpdate(1.5f); control.AlterLateUpdate();
                        var e = evidence.Evidence;
                        Check(e.complete && e.failure == null && opt.LastException == null, "Bounded evidence capture completes inside one real GPU submission.");
                        Check(e.exactReplay && e.replayDifferentPixels == 0, "Saved same-frame channel inputs replay to every identical RGBA pixel.");
                        Check(e.rightContributionPixels > 0 && e.leftContributionPixels > 0 && e.baseContributionPixels > 0,
                            "Independent removal proves visible contributions from all three explicit camera roles.");
                        Check(e.rightOnlyCoveragePixels > 0 && e.leftOnlyCoveragePixels > 0 && e.baseOnlyCoveragePixels > 0,
                            "The two captured coverage maps leave three independently occupied regions.");
                        Check(e.baseContributionPixels < e.width * e.height,
                            "Destination-alpha changes are excluded from visible base contribution counts.");
                        Check(e.stages.SequenceEqual(Enumerable.Range(0, 7)) && e.sameOwnerPair && e.distinctCameras,
                            "Selection clocks, source camera references and ordered shared-input versions belong to one update.");
                        Check(e.exactRightAccumulationReplay && e.rightAccumulationDifferentPixels == 0,
                            "The copied post-right mask accumulation matches independent replay at that exact stage.");
                        Check(e.exactLeftAccumulationReplay && e.leftAccumulationDifferentPixels == 0,
                            "The copied post-left mask accumulation matches independent replay at that exact stage.");
                        Check(e.captures.Count == 6 && e.captures.Select(x => x.stage).SequenceEqual(new[] {
                            "RightInputUpdated", "RightMaskDrawn", "LeftInputUpdated", "LeftMaskDrawn", "BasePrepared", "FinalCompositeDrawn" }) &&
                            e.captures.Select(x => x.inputVersion).SequenceEqual(new[] { 1, 1, 2, 2, 2, 2 }) &&
                            e.captures.All(x => x.selectedSerial == e.selectedSerial && x.submissionSerial == e.submissionSerial),
                            "Each saved stage carries the same selected update, submission and exact input version.");
                        var rightMask = e.captures[1]; var leftMask = e.captures[3];
                        Check(rightMask.sourceId == leftMask.sourceId && rightMask.copyId != leftMask.copyId &&
                            e.captures.All(x => x.sourceId != x.copyId) && e.captures.Select(x => x.copyId).Distinct().Count() == 6,
                            "Two versions of the reused accumulation RT are copied into independently owned images.");
                        Check(e.captures.All(x => JsonUtility.ToJson(x.source) == JsonUtility.ToJson(x.copy) && !x.sRGBWriteAtCopy) &&
                            !string.IsNullOrEmpty(e.activeColorSpace),
                            "Source and copy descriptor fields, sRGB and observed color-space state remain explicit.");
                        Check(rightMask.source.format == "ARGB32" && leftMask.source.format == "ARGB32" &&
                            !rightMask.source.sRGB && !leftMask.source.sRGB && e.captures.All(x => x.readbackFormat == "RGBA32" && x.readbackLinear),
                            "Post-blend UNorm storage is separate from the explicitly declared 8-bit linear PNG export.");
                        string temporary = Path.Combine(Path.GetTempPath(), "OriginalLiveEvidence-" + Guid.NewGuid().ToString("N"));
                        try
                        {
                            evidence.Save(temporary);
                            bool hashes = true, dimensions = true;
                            foreach (var capture in e.captures)
                            {
                                byte[] png = File.ReadAllBytes(Path.Combine(temporary, capture.png));
                                using (var sha = SHA256.Create())
                                    hashes &= capture.pngSha256 == BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
                                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                                try { dimensions &= decoded.LoadImage(png) && decoded.width == capture.copy.width && decoded.height == capture.copy.height; }
                                finally { Object.DestroyImmediate(decoded); }
                            }
                            Check(hashes && File.Exists(Path.Combine(temporary, "channel-evidence.private.json")),
                                "The receipt binds every completed PNG to its actual file hash.");
                            Check(dimensions, "All six saved PNGs decode at their recorded descriptor dimensions.");
                            var rightPng = File.ReadAllBytes(Path.Combine(temporary, rightMask.png));
                            var leftPng = File.ReadAllBytes(Path.Combine(temporary, leftMask.png));
                            string capturesBefore = JsonUtility.ToJson(e);
                            int rendersBefore = opt.RenderCount;
                            control.AlterUpdate(0); control.AlterLateUpdate();
                            evidence.Save(temporary);
                            Check(opt.RenderCount == rendersBefore + 1 && opt.LastTransparentChannels == 3 &&
                                rightPng.SequenceEqual(File.ReadAllBytes(Path.Combine(temporary, rightMask.png))) &&
                                leftPng.SequenceEqual(File.ReadAllBytes(Path.Combine(temporary, leftMask.png))),
                                "Both saved mask images survive the source accumulation being cleared and reused by a later update.");
                            Check(!rightPng.SequenceEqual(leftPng) && JsonUtility.ToJson(e) == capturesBefore,
                                "Right and left post-blend images stay distinct and keep their original capture metadata after RT reuse.");
                        }
                        finally
                        {
                            foreach (var capture in e.captures) File.Delete(Path.Combine(temporary, capture.png));
                            File.Delete(Path.Combine(temporary, "channel-evidence.private.json"));
                            if (Directory.Exists(temporary)) Directory.Delete(temporary);
                        }
                    }
                    using (var deferred = new OriginalLiveChannelEvidence(opt, 0, 1, f => background, true))
                    {
                        rejected = false;
                        try { deferred.CompleteDeferredVerification(); } catch (InvalidOperationException) { rejected = true; }
                        Check(rejected && !deferred.Evidence.complete && deferred.Evidence.failure == null,
                            "Deferred verification cannot run before the stage copies exist.");
                        control.AlterUpdate(1.5f); control.AlterLateUpdate();
                        var e = deferred.Evidence; long selectedSerial = e.selectedSerial;
                        Check(e.captured && !e.complete && e.failure == null && e.verificationBeginTicks == 0 && e.captures.Count == 6,
                            "Deferred mode captures the real stages without doing GPU replay/readback inside the live submission.");
                        string unwritten = Path.Combine(Path.GetTempPath(), "UmaViewerUnverified-" + Guid.NewGuid().ToString("N")); rejected = false;
                        try { deferred.Save(unwritten); } catch (InvalidOperationException) { rejected = true; }
                        Check(rejected && !Directory.Exists(unwritten), "Unverified copied images cannot emit a success receipt.");
                        control.AlterUpdate(0); control.AlterLateUpdate();
                        Check(e.selectedSerial == selectedSerial && e.captured && !e.complete && opt.LastTransparentChannels == 3,
                            "The copied frame stays independent while later updates reuse and clear the original RTs.");
                        deferred.CompleteDeferredVerification();
                        Check(e.complete && e.exactReplay && e.exactRightAccumulationReplay && e.exactLeftAccumulationReplay,
                            "Explicit completion after source reuse reproduces the saved right, left, and final RGBA images.");
                        Check(e.verificationBeginTicks >= e.captureCompletedTicks && e.verificationEndTicks >= e.verificationBeginTicks,
                            "Copy completion and later verification have separate measured boundaries.");
                        long verified = e.verificationEndTicks; deferred.CompleteDeferredVerification();
                        Check(e.verificationEndTicks == verified && e.selectedSerial == selectedSerial,
                            "Repeated completion does not rerun or change the verified observation.");
                        deferred.Dispose(); rejected = false;
                        try { deferred.CompleteDeferredVerification(); } catch (ObjectDisposedException) { rejected = true; }
                        Check(rejected, "Released snapshots cannot be verified later.");
                    }
                }
            }
            var lifetime = new OriginalLiveContinuousProvider(control, profile, binding);
            var replacement = new OriginalLiveTimelineBridge(); control.OriginalCompositeBridge = replacement;
            lifetime.Dispose(); lifetime.Dispose();
            Check(ReferenceEquals(control.OriginalCompositeBridge, replacement), "Disposal never detaches a replacement registration.");
            control.OriginalCompositeBridge = null;
            director._liveTimelineControl = control;
            var advance = typeof(Director).GetMethod("TryAdvancePlaybackTime", BindingFlags.Instance | BindingFlags.NonPublic);
            bool Advance(float delta) => (bool)advance.Invoke(director, new object[] { delta });
            director._liveCurrentTime = 10;
            Check(Advance(.125f) && director._liveCurrentTime == 10.125f, "Unregistered Director retains the deltaTime arithmetic.");
            using (var clockProvider = new OriginalLiveContinuousProvider(control, profile, binding))
            {
                foreach (float supplied in new[] { 1.25f, 1.25f, .5f, 2f })
                {
                    int calls = 0; director.OriginalLiveTimeProvider = () => { calls++; return supplied; };
                    Check(Advance(99) && calls == 1 && director._liveCurrentTime == supplied, "Explicit sample replaces accumulation, including repeated time and backward seek.");
                    control.AlterUpdate(director._liveCurrentTime); control.AlterLateUpdate();
                    Check(clockProvider.LastFrame.Selections[0].Selection.ComparisonClock.Value.CurrentOwnerTime == supplied,
                        "Actual Control receives the supplied owner time in the same selected update.");
                }
                director.OriginalLiveTimeProvider = () => null;
                Check(!Advance(99) && director._liveCurrentTime == 2 &&
                    (bool)typeof(Director).GetField("_lateTimelineAppliedThisFrame", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director),
                    "Missing explicit sample holds owner and suppresses a late-only render.");
                foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
                {
                    director.OriginalLiveTimeProvider = () => invalid; rejected = false;
                    try { Advance(99); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
                    Check(rejected && director._liveCurrentTime == 2, "Invalid explicit time never mutates owner.");
                }
                var foreign = director.OriginalLiveTimeProvider; rejected = false;
                try { new OriginalLiveAudioClock(director, clockProvider, () => null); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && ReferenceEquals(director.OriginalLiveTimeProvider, foreign), "Audio registration cannot replace an existing provider.");
                director.OriginalLiveTimeProvider = null;
                var audioClock = new OriginalLiveAudioClock(director, clockProvider, () => null);
                Check(!Advance(99) && audioClock.HeldReads == 1 && audioClock.ReadCount == 0, "Missing explicit audio source does not synthesize zero time.");
                audioClock.Dispose(); audioClock.Dispose();
                Check(director.OriginalLiveTimeProvider == null, "Audio registration releases only its own provider.");
                audioClock = new OriginalLiveAudioClock(director, clockProvider, () => null);
                director.OriginalLiveTimeProvider = foreign; audioClock.Dispose();
                Check(ReferenceEquals(director.OriginalLiveTimeProvider, foreign), "Audio disposal preserves a replacement provider.");
                director.OriginalLiveTimeProvider = null;

                var startup = typeof(Director).GetMethod("TryConsumeStartupTime", BindingFlags.Instance | BindingFlags.NonPublic);
                var startupOwner = typeof(Director).GetField("OriginalLiveStartupTimeProvider", BindingFlags.Instance | BindingFlags.NonPublic);
                var sync = typeof(Director).GetField("_syncTime", BindingFlags.Instance | BindingFlags.Public);
                int StartClock() => (int)startup.Invoke(director, null);
                var savedSlider = director.sliderControl;
                var slider = Go("Synthetic startup slider").AddComponent<SliderControl>(); director.sliderControl = slider;
                int startupReads = 0;
                Func<float?> initial = () => { startupReads++; return .005f; };
                director.OriginalLiveTimeProvider = initial; sync.SetValue(director, false);
                Check(StartClock() == 0 && startupReads == 0, "Old registrations keep the legacy startup path without another read.");
                startupOwner.SetValue(director, initial); director.IsRecordVMD = true;
                Check(StartClock() == 0 && startupReads == 0, "Recording retains its existing startup path."); director.IsRecordVMD = false;
                slider.is_Touched = true;
                Check(StartClock() == 0 && startupReads == 0, "Paused startup does not consume an audio sample."); slider.is_Touched = false;
                slider.is_Outed = true;
                Check(StartClock() == 0 && startupReads == 0, "Seek startup retains the existing seek path."); slider.is_Outed = false;
                foreach (float early in new[] { 0f, .005f, .01f })
                {
                    int calls = 0; Func<float?> sample = () => { calls++; return early; };
                    director.OriginalLiveTimeProvider = sample; startupOwner.SetValue(director, sample); sync.SetValue(director, false);
                    Check(StartClock() == 2 && calls == 1 && director._liveCurrentTime == early && (bool)sync.GetValue(director),
                        "Explicit startup accepts real zero or early time without the legacy threshold or a second read.");
                    Check(StartClock() == 0 && calls == 1, "An accepted startup is not consumed again.");
                }
                foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
                {
                    Func<float?> sample = () => invalid;
                    director.OriginalLiveTimeProvider = sample; startupOwner.SetValue(director, sample); sync.SetValue(director, false);
                    float unchanged = director._liveCurrentTime; rejected = false;
                    try { StartClock(); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
                    Check(rejected && !(bool)sync.GetValue(director) && director._liveCurrentTime == unchanged,
                        "An invalid startup leaves owner and synchronization unchanged.");
                }
                Func<float?> changing = () => { director.OriginalLiveTimeProvider = initial; return 3f; };
                director.OriginalLiveTimeProvider = changing; startupOwner.SetValue(director, changing); sync.SetValue(director, false);
                float beforeReplacement = director._liveCurrentTime;
                Check(StartClock() == 1 && !(bool)sync.GetValue(director) && director._liveCurrentTime == beforeReplacement &&
                    ReferenceEquals(director.OriginalLiveTimeProvider, initial), "A replacement during evaluation holds startup without overwriting its owner.");
                director.OriginalLiveTimeProvider = null; startupOwner.SetValue(director, null); sync.SetValue(director, false);
                using (var startupClock = new OriginalLiveAudioClock(director, clockProvider, () => null))
                {
                    Check(startupOwner.GetValue(director) == null, "Audio construction alone does not opt into startup consumption.");
                    startupClock.EnableStartupConsumption(); startupClock.EnableStartupConsumption();
                    Check(ReferenceEquals(startupOwner.GetValue(director), director.OriginalLiveTimeProvider), "Startup ownership is explicit and idempotent.");
                    float held = director._liveCurrentTime;
                    Check(StartClock() == 1 && !(bool)sync.GetValue(director) && director._liveCurrentTime == held && startupClock.HeldReads == 1,
                        "Missing startup audio holds the original state and leaves startup pending.");
                }
                Check(startupOwner.GetValue(director) == null && director.OriginalLiveTimeProvider == null, "Disposal releases the owned startup token.");
                var detachedStartup = new OriginalLiveAudioClock(director, clockProvider, () => null);
                detachedStartup.EnableStartupConsumption(); startupOwner.SetValue(director, initial); rejected = false;
                try { detachedStartup.EnableStartupConsumption(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && ReferenceEquals(startupOwner.GetValue(director), initial), "Another startup owner is never overwritten.");
                director.OriginalLiveTimeProvider = initial; detachedStartup.Dispose();
                Check(ReferenceEquals(director.OriginalLiveTimeProvider, initial) && ReferenceEquals(startupOwner.GetValue(director), initial),
                    "Disposal preserves replacement playback and startup ownership.");
                rejected = false; try { detachedStartup.EnableStartupConsumption(); } catch (ObjectDisposedException) { rejected = true; }
                Check(rejected, "Disposed clocks cannot re-enable startup.");
                director.OriginalLiveTimeProvider = null; startupOwner.SetValue(director, null); director.sliderControl = savedSlider;
                sync.SetValue(director, false); director._liveCurrentTime = 2;

                var readDsp = typeof(OriginalLiveAudioClock).GetMethod("TryReadDsp", BindingFlags.Static | BindingFlags.NonPublic);
                foreach (Func<double> reader in new Func<double>[] { null, () => -1, () => double.NaN,
                    () => double.PositiveInfinity, () => double.NegativeInfinity, () => throw new InvalidOperationException("Artificial DSP failure") })
                {
                    var arguments = new object[] { reader, 0d };
                    Check(!(bool)readDsp.Invoke(null, arguments), "Missing, invalid, or throwing DSP diagnostics remain unavailable.");
                }
                var validDsp = new object[] { (Func<double>)(() => 1.25d), 0d };
                Check((bool)readDsp.Invoke(null, validDsp) && (double)validDsp[1] == 1.25d,
                    "Valid diagnostic DSP time retains its measured double value.");

                var limits = new OriginalLiveClockMath.Parameters(true, .02f, .001f, .002f, .1f, 100f);
                var clockState = new OriginalLiveClockMath.State(1, 1, 0, 1);
                Check(OriginalLiveClockMath.TryEvaluate(1.06f, clockState, limits, out var calculated) && calculated.Smoothed &&
                    calculated.Next.CurrentTime != calculated.SourceTime && calculated.Next.Correction == .002f,
                    "Explicit smoothing retains a distinct source value and bounded correction.");
                Check(calculated.PreviousTime == 1 && calculated.DeltaTime == calculated.Next.CurrentTime - 1 &&
                    clockState.CurrentTime == 1, "Clock calculation preserves its input state and explicit previous time.");
                Check(OriginalLiveClockMath.TryEvaluate(5, calculated.Next, limits, out var reset) && !reset.Smoothed &&
                    reset.Next.CurrentTime == 5 && reset.Next.Correction == 0, "Large discontinuity resets through explicit distance policy.");
                var disabled = new OriginalLiveClockMath.Parameters(false, .02f, .001f, .002f, .1f, 100);
                Check(OriginalLiveClockMath.TryEvaluate(1.06f, clockState, disabled, out reset) && !reset.Smoothed &&
                    reset.Next.CurrentTime == 1.06f, "Explicit disabled smoothing keeps source time.");
                foreach (float sample in new[] { -1f, 101f })
                    Check(OriginalLiveClockMath.TryEvaluate(sample, clockState, limits, out reset) &&
                        reset.Next.CurrentTime == (sample < 0 ? 0 : 100), "Current clock clamps while retaining the source value.");
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    Check(!OriginalLiveClockMath.TryEvaluate(invalid, clockState, limits, out reset), "Nonfinite source cannot produce a clock state.");
                Check(!OriginalLiveClockMath.TryEvaluate(1, clockState, default, out reset), "Missing explicit clock limits are rejected.");
                var saturated = new OriginalLiveClockMath.State(1, 1, .003f, 1);
                Check(OriginalLiveClockMath.TryEvaluate(1.06f, saturated, limits, out reset) &&
                    reset.Next.Correction == .003f && reset.Next.CurrentTime == 1.06f, "Saturated positive correction is not accumulated again.");
                saturated = new OriginalLiveClockMath.State(1, 1, -.003f, 1);
                Check(OriginalLiveClockMath.TryEvaluate(1, saturated, limits, out reset) && reset.Next.Correction == -.003f,
                    "Saturated negative correction remains bounded by the observed branch.");

                var audio = Go("Artificial clock source").AddComponent<AudioSource>();
                audio.clip = Own(AudioClip.Create("Artificial clock clip", 1024, 1, 8000, false));
                var pendingType = typeof(OriginalLiveAudioClock).GetNestedType("Pending", BindingFlags.NonPublic);
                var resolve = typeof(OriginalLiveAudioClock).GetMethod("Resolve", BindingFlags.Instance | BindingFlags.NonPublic);
                object Snapshot(float time) => Activator.CreateInstance(pendingType, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { audio, audio.clip, time, 17 }, null);
                var readySource = typeof(Director).GetField("OriginalLiveAudioReady", BindingFlags.Instance | BindingFlags.NonPublic);
                var afterOwner = typeof(Director).GetField("OriginalLiveAfterOwnerUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                var stagedRead = typeof(OriginalLiveAudioClock).GetMethod("ReadStagedSource", BindingFlags.Instance | BindingFlags.NonPublic);
                var stagedClock = new OriginalLiveAudioClock(director, clockProvider, () => audio);
                OriginalLiveAudioClock.Sample? produced = null;
                stagedClock.SourceCaptured += sample => produced = sample;
                stagedClock.EnableStagedSourceConsumption(); stagedClock.EnableStagedSourceConsumption();
                Check(readySource.GetValue(director) != null && afterOwner.GetValue(director) != null,
                    "Staged sampling explicitly owns the before-Play and after-owner callbacks.");
                ((Action)readySource.GetValue(director))();
                Check(stagedClock.StagedSourceCaptures == 1 && stagedClock.StagedSourceConsumes == 0 && stagedClock.ReadCount == 0,
                    "The actual stopped AudioSource is observed before Play without advancing any owner state.");
                Check(produced.HasValue && produced.Value.SourceReadSerial == 1 && produced.Value.SourceReadStage == 1 &&
                    produced.Value.SourceTime == audio.time && produced.Value.AudioSamples == audio.timeSamples && produced.Value.ConsumeBeginTicks == 0,
                    "An independent producer observation precedes consumption and contains the actual API values.");
                // Invoke the consumer directly: this fixture never starts audio.
                float primed = (float)stagedRead.Invoke(stagedClock, new object[] { audio, audio.clip });
                control.AlterUpdate(primed); control.AlterLateUpdate();
                var primedObservation = stagedClock.LastObservation.Value;
                Check(primed == audio.time && primedObservation.AudioSamples == audio.timeSamples && primedObservation.SourceReadStage == 1 &&
                    primedObservation.SourceReadSerial == 1 && primedObservation.SourceReadUnityFrame == Time.frameCount && stagedClock.JoinedFrames == 1,
                    "The captured actual value and producer serial join a real Control update without a fabricated initial sample.");
                Check(primedObservation.ReadEndTicks <= primedObservation.ConsumeBeginTicks &&
                    primedObservation.ConsumeBeginTicks <= primedObservation.ConsumeEndTicks,
                    "The producer read and later clock consumption retain separate actual timestamp brackets.");
                ((Action)afterOwner.GetValue(director))();
                Check(stagedRead.Invoke(stagedClock, new object[] { audio, audio.clip }) == null && stagedClock.StagedSourceConsumes == 1,
                    "A stopped source cannot refresh or reuse a consumed cache entry.");
                stagedClock.Dispose();
                Check(readySource.GetValue(director) == null && afterOwner.GetValue(director) == null && startupOwner.GetValue(director) == null,
                    "Staged disposal releases only the callbacks and startup token it owns.");
                rejected = false; try { stagedClock.EnableStagedSourceConsumption(); } catch (ObjectDisposedException) { rejected = true; }
                Check(rejected, "Disposed registrations cannot recreate a staged lifetime.");
                using (var diagnostics = new OriginalLiveAudioClock(director, clockProvider, () => audio))
                {
                    diagnostics.EnableStagedSourceConsumption(); diagnostics.SourceCaptured += sample => throw new InvalidOperationException("Artificial observer failure");
                    var ready = (Action)readySource.GetValue(director); ready();
                    Check(diagnostics.SourceCaptureObserverFailures == 1 && diagnostics.StagedSourceCaptures == 1 &&
                        stagedRead.Invoke(diagnostics, new object[] { audio, audio.clip }) != null,
                        "Diagnostic observer failure is recorded separately and does not replace the actual source value.");
                    diagnostics.Dispose(); ready();
                    Check(diagnostics.StagedSourceCaptures == 1, "A retained callback cannot capture new data after disposal.");
                }
                Action foreignSource = () => { };
                using (var foreignClock = new OriginalLiveAudioClock(director, clockProvider, () => audio))
                {
                    readySource.SetValue(director, foreignSource); rejected = false;
                    try { foreignClock.EnableStagedSourceConsumption(); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && ReferenceEquals(readySource.GetValue(director), foreignSource) && startupOwner.GetValue(director) == null,
                        "A foreign source callback is rejected without partially enabling startup.");
                }
                Check(ReferenceEquals(readySource.GetValue(director), foreignSource), "Disposal preserves a foreign source callback.");
                readySource.SetValue(director, null);
                using (var changedClip = new OriginalLiveAudioClock(director, clockProvider, () => audio))
                {
                    changedClip.EnableStagedSourceConsumption(); ((Action)readySource.GetValue(director))();
                    var otherClip = Own(AudioClip.Create("Unqualified replacement", 1024, 1, 8000, false));
                    Check(stagedRead.Invoke(changedClip, new object[] { audio, otherClip }) == null && changedClip.StagedSourceInvalidations == 1,
                        "A cached sample cannot cross a different clip lifetime.");
                    rejected = false; try { changedClip.EnableStagedSourceConsumption(); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "An invalidated staged lifetime requires a new registration.");
                }
                using (var duplicate = new OriginalLiveAudioClock(director, clockProvider, () => audio))
                {
                    duplicate.EnableStagedSourceConsumption(); var ready = (Action)readySource.GetValue(director); ready(); ready();
                    Check(duplicate.StagedSourceCaptures == 1 && duplicate.StagedSourceInvalidations == 1 &&
                        stagedRead.Invoke(duplicate, new object[] { audio, audio.clip }) == null,
                        "An unconsumed generation cannot be overwritten by a second producer callback.");
                }
                using (var unconsumed = new OriginalLiveAudioClock(director, clockProvider, () => audio))
                {
                    unconsumed.EnableStagedSourceConsumption(); ((Action)afterOwner.GetValue(director))();
                    Check(unconsumed.StagedSourceInvalidations == 1 && unconsumed.StagedSourceConsumes == 0,
                        "A pause or seek path without an accepted clock read invalidates the staged lifetime.");
                }
                using (var evaluatedClock = new OriginalLiveAudioClock(director, clockProvider, () => audio, sample =>
                {
                    Check(sample.Source == audio && sample.Clip == audio.clip && sample.AudioSamples == 17 &&
                        sample.SampledUnityFrame == Time.frameCount, "Evaluator receives one current source/clip/frame snapshot.");
                    Check(sample.ClipName == "Artificial clock clip" && sample.ClipSampleCount == 1024 &&
                        sample.ClipFrequency == 8000 && sample.ClipChannels == 1,
                        "Evaluator receives the sampled waveform metadata without converting its position into time.");
                    sample.Clip.name = "Renamed after source sampling";
                    return OriginalLiveClockMath.TryEvaluate(sample.SourceTime, clockState, limits, out var next)
                        ? next.Next.CurrentTime : (float?)null;
                }))
                {
                    float owner = (float)resolve.Invoke(evaluatedClock, new[] { Snapshot(1.06f) });
                    control.AlterUpdate(owner); control.AlterLateUpdate();
                    var observed = evaluatedClock.LastObservation.Value;
                    Check(!observed.HasDspTime && observed.ReadBeginDspTime == 0 && observed.ReadEndDspTime == 0,
                        "Legacy samples retain absent DSP metadata without fabricating a measured epoch.");
                    Check(observed.SourceReadStage == 0 && observed.SourceReadSerial == 0 && observed.SourceReadUnityFrame == observed.SampledUnityFrame,
                        "Existing immediate samples keep their original read stage and frame semantics.");
                    Check(evaluatedClock.JoinedFrames == 1 && observed.SourceTime == 1.06f && observed.OwnerTime == owner &&
                        observed.SourceTime != observed.OwnerTime, "Actual Control joins evaluated owner while preserving raw source time.");
                    Check(observed.ClipName == "Artificial clock clip" && observed.ClipSampleCount == 1024 &&
                        observed.ClipFrequency == 8000 && observed.ClipChannels == 1 &&
                        observed.Clip.name == "Renamed after source sampling",
                        "Owner observation retains the sampled metadata when the same clip is renamed during evaluation.");
                    audio.clip.name = "Artificial clock clip";
                    resolve.Invoke(evaluatedClock, new[] { Snapshot(1.06f) });
                    audio.clip = Own(AudioClip.Create("Replacement artificial clip", 1024, 1, 8000, false));
                    control.AlterUpdate(owner); control.AlterLateUpdate();
                    Check(evaluatedClock.RejectedFrames == 1 && !evaluatedClock.LastObservation.HasValue,
                        "Changing clip after evaluation rejects the pending correspondence.");
                }
                Check(director.OriginalLiveTimeProvider == null, "Evaluated clock releases its registration.");
                var session = new OriginalLiveSmoothingClock(director, clockProvider, audio, clockState, limits);
                float provisional = (float)resolve.Invoke(session.Clock, new[] { Snapshot(1.06f) });
                Check(session.CommittedState.CurrentTime == clockState.CurrentTime && session.CommittedFrames == 0,
                    "Smoothing evaluation alone never advances the checkpoint.");
                control.AlterUpdate(provisional + .1f); control.AlterLateUpdate();
                Check(session.CommittedState.CurrentTime == 1 && session.DiscardedCandidates == 1 && session.CommittedFrames == 0,
                    "A mismatched actual owner discards the provisional state.");
                resolve.Invoke(session.Clock, new[] { Snapshot(1.06f) });
                provisional = (float)resolve.Invoke(session.Clock, new[] { Snapshot(1.07f) });
                OriginalLiveClockMath.TryEvaluate(1.07f, clockState, limits, out var expectedCommit);
                Check(provisional == expectedCommit.Next.CurrentTime && session.DiscardedCandidates == 2,
                    "A missing Control update is discarded before recomputing from the committed checkpoint.");
                control.AlterUpdate(provisional); control.AlterLateUpdate();
                Check(session.CommittedFrames == 1 && session.CommittedState.PreviousSourceTime == 1.07f &&
                    session.CommittedState.CurrentTime == provisional, "The joined Control update commits exactly once.");
                control.AlterUpdate(provisional); control.AlterLateUpdate();
                Check(session.CommittedFrames == 1, "A frame without a new audio sample cannot commit again.");
                var committed = session.CommittedState;
                audio.clip = Own(AudioClip.Create("Explicit reset clip", 1024, 1, 8000, false));
                Check(resolve.Invoke(session.Clock, new[] { Snapshot(1.08f) }) == null && session.HeldEvaluations == 1 &&
                    session.CommittedState.CurrentTime == committed.CurrentTime, "A changed clip holds without guessing its initial state.");
                session.Reset(audio, clockState, limits);
                provisional = (float)resolve.Invoke(session.Clock, new[] { Snapshot(1.06f) });
                session.Reset(audio, committed, limits);
                control.AlterUpdate(provisional); control.AlterLateUpdate();
                Check(session.CommittedFrames == 1 && session.CommittedState.CurrentTime == committed.CurrentTime &&
                    session.DiscardedCandidates == 3, "Explicit reset cancels an already evaluated candidate.");
                rejected = false;
                try { session.Reset(audio, clockState, default); } catch (ArgumentException) { rejected = true; }
                Check(rejected && session.CommittedState.CurrentTime == committed.CurrentTime,
                    "Invalid reset preserves the existing checkpoint.");
                provisional = (float)resolve.Invoke(session.Clock, new[] { Snapshot(1.08f) });
                director.OriginalLiveTimeProvider = foreign;
                control.AlterUpdate(provisional); control.AlterLateUpdate();
                Check(session.CommittedFrames == 1 && session.CommittedState.CurrentTime == committed.CurrentTime,
                    "Replacing the time registration prevents a pending commit.");
                Check(resolve.Invoke(session.Clock, new[] { Snapshot(1.09f) }) == null,
                    "An unowned registration cannot evaluate another smoothing state.");
                session.Dispose(); session.Dispose();
                Check(ReferenceEquals(director.OriginalLiveTimeProvider, foreign), "Smoothing disposal preserves a replacement registration.");
                rejected = false;
                try { session.Reset(audio, clockState, limits); } catch (ObjectDisposedException) { rejected = true; }
                Check(rejected, "Disposed smoothing cannot be reset into a new lifetime.");
                director.OriginalLiveTimeProvider = null;
                using (var replacementSession = new OriginalLiveSmoothingClock(director, clockProvider, audio, clockState, limits))
                {
                    resolve.Invoke(replacementSession.Clock, new[] { Snapshot(1.06f) });
                    var otherSource = Go("Explicit replacement source").AddComponent<AudioSource>();
                    otherSource.clip = audio.clip;
                    replacementSession.Reset(otherSource, clockState, limits);
                    Check(resolve.Invoke(replacementSession.Clock, new[] { Snapshot(1.07f) }) == null,
                        "An explicitly replaced source rejects an old source even with the same clip.");
                }
                AudioSource lazySource = null;
                using (var delayed = new OriginalLiveSmoothingClock(director, clockProvider, () => lazySource, clockState, limits))
                {
                    Check(!Advance(99) && delayed.Clock.HeldReads == 1 && delayed.CommittedFrames == 0,
                        "A not-yet-loaded source holds the registered checkpoint without inventing time.");
                    lazySource = audio;
                    provisional = (float)resolve.Invoke(delayed.Clock, new[] { Snapshot(1.06f) });
                    control.AlterUpdate(provisional); control.AlterLateUpdate();
                    Check(delayed.CommittedFrames == 1, "The first valid source binds once after timeline registration.");
                    provisional = (float)resolve.Invoke(delayed.Clock, new[] { Snapshot(1.08f) });
                    var switchedSource = Go("Resolver replacement source").AddComponent<AudioSource>(); switchedSource.clip = audio.clip;
                    lazySource = switchedSource;
                    control.AlterUpdate(provisional); control.AlterLateUpdate();
                    Check(delayed.CommittedFrames == 1 && delayed.DiscardedCandidates == 1,
                        "Changing the current source resolver before commit invalidates the candidate.");
                    object switchedSample = Activator.CreateInstance(pendingType, BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new object[] { switchedSource, switchedSource.clip, 1.09f, 17 }, null);
                    Check(resolve.Invoke(delayed.Clock, new[] { switchedSample }) == null,
                        "A resolver does not automatically rebind a different source lifetime.");
                    delayed.Reset(switchedSource, clockState, limits);
                    provisional = (float)resolve.Invoke(delayed.Clock, new[] { switchedSample });
                    control.AlterUpdate(provisional); control.AlterLateUpdate();
                    Check(delayed.CommittedFrames == 2, "Explicit reset admits the current replacement source.");
                }
                foreach (float? invalid in new float?[] { null, -1, float.NaN, float.PositiveInfinity })
                    using (var evaluatedClock = new OriginalLiveAudioClock(director, clockProvider, () => audio, sample => invalid))
                        Check(resolve.Invoke(evaluatedClock, new[] { Snapshot(1) }) == null && evaluatedClock.HeldReads == 1 &&
                            evaluatedClock.ReadCount == 0, "Unresolved or invalid owner time does not publish a pending sample.");
                OriginalLiveAudioClock disposingClock = null;
                disposingClock = new OriginalLiveAudioClock(director, clockProvider, () => audio,
                    sample => { disposingClock.Dispose(); return 1; });
                Check(resolve.Invoke(disposingClock, new[] { Snapshot(1) }) == null && disposingClock.ReadCount == 0 &&
                    director.OriginalLiveTimeProvider == null, "Disposal during evaluation cannot publish a later pending sample.");
                using (var destroyingClock = new OriginalLiveAudioClock(director, clockProvider, () => audio,
                    sample => { Object.DestroyImmediate(sample.Clip); return 1; }))
                    Check(resolve.Invoke(destroyingClock, new[] { Snapshot(1) }) == null && destroyingClock.HeldReads == 1,
                        "A clip destroyed during evaluation cannot publish a pending correspondence.");
            }
            checks += OriginalObservedClockValidation.Exercise(control, background, target, profile, binding);
            var character = Go("Synthetic explicit physics opt out").AddComponent<UmaContainerCharacter>();
            Check(character.InitializeNativePhysicsOnLoad, "Existing physics initialization remains the default.");
            character.InitializeNativePhysicsOnLoad = false; character.LoadPhysics();
            Check(!character.EnablePhysics, "Explicit physics opt out returns before any native controller initialization.");
        }
        finally
        {
            directorField.SetValue(null, oldDirector);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
        }
        return checks;
    }
}
