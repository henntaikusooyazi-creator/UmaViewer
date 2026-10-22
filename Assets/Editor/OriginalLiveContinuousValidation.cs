using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            Color32[] Read()
            {
                var active = RenderTexture.active; var pixels = new Texture2D(64, 32, TextureFormat.RGBA32, false, true);
                try { RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 64, 32), 0, 0); pixels.Apply(); return pixels.GetPixels32(); }
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
            using (var provider = new OriginalLiveContinuousProvider(control, profile, both, s =>
                new OriginalLiveDrawInputResolver.Inputs(default, OriginalLiveContinuousRenderer.FullViewportMapping(32))))
            using (var preview = new OriginalLiveContinuousRenderer(provider, 0, 1, f => background,
                Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), target))
            {
                control.AlterUpdate(.5f); control.AlterLateUpdate();
                Check(provider.LastFrame.Selections.Count == 2 && preview.RenderCount == 1 && preview.LastException == null,
                    "Two explicitly bound artificial channels reach their sequential GPU submissions.");
            }
            var lifetime = new OriginalLiveContinuousProvider(control, profile, binding);
            var replacement = new OriginalLiveTimelineBridge(); control.OriginalCompositeBridge = replacement;
            lifetime.Dispose(); lifetime.Dispose();
            Check(ReferenceEquals(control.OriginalCompositeBridge, replacement), "Disposal never detaches a replacement registration.");
            control.OriginalCompositeBridge = null;
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
