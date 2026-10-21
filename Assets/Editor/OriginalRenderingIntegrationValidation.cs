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

// Opt-in batch acceptance with artificial typed keys and generated geometry.
// Uses the Fork's real Control/Director in a transient scene; no saved scene or game asset is changed.
public static class OriginalRenderingIntegrationValidation
{
    [Serializable] sealed class Result
    {
        public string status = "Running", unity, graphics, failure, controlAssembly;
        public int checks, baselineChecks, builtinFrames, urpFrames, providerCalls;
        public int cameraSelectionChecks, cameraSelectionBuiltinFrames, cameraSelectionUrpFrames;
        public List<CameraSelectionSample> cameraSelection = new List<CameraSelectionSample>();
        public bool TestOnly = true, OriginalVerified = false, runtime_contract = false;
        public string Phase257cause = "Unknown", naturalSongPlayback = "NotTested";
    }
    [Serializable] sealed class CameraSelectionSample
    {
        public string pipeline, requested, selected, directorAtRender, directorAfterUpdate, center, status;
    }
    static Result result;
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
            Exercise(false); Exercise(true);
            result.baselineChecks = result.checks - result.cameraSelectionChecks;
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
