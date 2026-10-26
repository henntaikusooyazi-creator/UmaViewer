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
            }
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
