using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Gallop.Live.Cutt;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Artificial key/payload inputs only. No original values or programs embedded.
public static class OriginalPostFilmValidation
{
    [Serializable] sealed class Report { public string status = "Running", failure; public int checks, gpuFrames; public bool OriginalVerified = false, runtime_contract = false; }
    static Report report;
    static readonly List<Object> owned = new List<Object>();
    static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Check(bool value, string message) { report.checks++; if (!value) throw new InvalidOperationException(message); }
    static void Reject<T>(Action action, string message) where T : Exception
    { bool rejected = false; try { action(); } catch (T) { rejected = true; } Check(rejected, message); }
    static readonly OriginalPostFilmMath.ConfirmedProfile Profile = new OriginalPostFilmMath.ConfirmedProfile(50, 1, 2, 2, new Vector2(17, 19), new Vector2(23, 29), true);
    static LiveTimelineKeyPostFilmData Key(int frame) => new LiveTimelineKeyPostFilmData
    { frame = frame, filmMode = PostFilmMode.VignetteLerp, filmPower = .25f, colorType = PostColorType.Color4,
      color0 = Color.red, color1 = Color.green, color2 = Color.blue, color3 = Color.white,
      filmOptionParam = new Vector4(1, 2, 3, 4), filmOffsetParam = new Vector2(.25f, .5f),
      FilmScale = new Vector2(2, 4), DepthClip = 2, movieSpeed = .5f };
    static void MathChecks()
    {
        var a = Key(100); var b = Key(200); b.filmPower = .75f; b.depthPower = 1; b.DepthClip = 0; b.RollAngle = 2;
        b.color0 = Color.blue; b.color1 = Color.white; b.color2 = Color.red; b.color3 = Color.green;
        var value = OriginalPostFilmMath.Evaluate(a, b, .25f, 125, Profile);
        Check(value.filmPower == .375f && value.depthPower == .25f && value.DepthClip == 1.5f, "Separate Film scalar interpolation.");
        Check(value.RollAngle == .5f && value.FilmScale == a.FilmScale && value.movieTime == .25f, "Roll, scale and key-relative time.");
        Check(value.color0 == new Color(.75f, 0, .25f, 1) && value.color2 == new Color(.25f, 0, .75f, 1), "Independent four-color interpolation.");
        foreach (PostColorType layout in Enum.GetValues(typeof(PostColorType)))
        {
            a.colorType = layout;
            foreach (float? ratio in new float?[] { null, .25f })
            {
                value = OriginalPostFilmMath.Evaluate(a, b, ratio, 125, Profile);
                if (layout == PostColorType.ColorAll) Check(value.color0 == value.color1 && value.color1 == value.color2 && value.color2 == value.color3, "Single color broadcast.");
                if (layout == PostColorType.Color2TopBottom) Check(value.color0 == value.color1 && value.color2 == value.color3 && value.color0 != value.color2, "Top/bottom layout.");
                if (layout == PostColorType.Color2LeftRight) Check(value.color0 == value.color2 && value.color1 == value.color3 && value.color0 != value.color1, "Left/right layout.");
                if (layout == PostColorType.Color4) Check(value.color0 != value.color1 && value.color2 != value.color3, "Four-color layout.");
            }
        }
        value = OriginalPostFilmMath.Evaluate(a, b, null, 125, Profile);
        Check(value.filmPower == a.filmPower && value.RollAngle == a.RollAngle, "No used ratio holds the selected key.");
        value = OriginalPostFilmMath.Evaluate(a, b, 2, 125, Profile);
        Check(value.filmPower == 1.25f, "Producer arithmetic does not add a second clamp.");
        a.attribute = (LiveTimelineKeyAttribute)0x100000;
        value = OriginalPostFilmMath.Evaluate(a, null, null, 125, Profile);
        var film = OriginalPostFilmMath.ToFilm(value, Profile);
        Check(film.InverseVignette && film.IsEnableDepth && film.RollParameter == new Vector4(0, 1, 17, 19), "Explicit flags and producer tail dependencies.");
        Check(film.ScaleParameter == new Vector4(.5f, .25f, 23, 29), "Reciprocal scale preserves explicit tail.");
        a.FilmScale = Vector2.zero; film = OriginalPostFilmMath.ToFilm(OriginalPostFilmMath.Evaluate(a, null, null, 125, Profile), Profile);
        Check(float.IsPositiveInfinity(film.ScaleParameter.x), "No invented scale epsilon.");
        Reject<ArgumentException>(() => OriginalPostFilmMath.Evaluate(a, null, .5f, 125, Profile), "Ratio requires next key.");
        Reject<ArgumentNullException>(() => OriginalPostFilmMath.Evaluate(a, null, null, 125, null), "Profile required.");
        a.layerMode = LiveTimelineKeyPostFilmData.LayerMode.UVMovie;
        Reject<NotSupportedException>(() => OriginalPostFilmMath.Evaluate(a, null, null, 125, Profile), "Movie has no fabricated provider.");
        a.layerMode = 0; a.attribute = (LiveTimelineKeyAttribute)0x200000;
        Reject<NotSupportedException>(() => OriginalPostFilmMath.Evaluate(a, null, null, 125, Profile), "BlinkLight dependency is separate.");
        a.attribute = 0; a.colorType = (PostColorType)99;
        Reject<NotSupportedException>(() => OriginalPostFilmMath.Evaluate(a, null, null, 125, Profile), "Unknown layout rejected.");
    }
    static void Pipeline(bool urp)
    {
        if (urp) { var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>()); var asset = Own(UniversalRenderPipelineAsset.Create(data)); asset.msaaSampleCount = 1; asset.supportsHDR = false; GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset; }
        else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
        var go = Own(new GameObject("Artificial.Film.Control")); go.SetActive(false);
        var control = go.AddComponent<LiveTimelineControl>();
        var camera = Own(new GameObject("Artificial.Film.Camera")).AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 0;
        var other = Own(new GameObject("Artificial.Film.Other")).AddComponent<Camera>(); other.enabled = false;
        var bridge = new OriginalLiveTimelineBridge(); bridge.UseConfirmedPostFilm(Profile); bridge.UseConfirmedOwnerClock(50); control.OriginalCompositeBridge = bridge;
        var keys = new LiveTimelineKeyPostFilmDataList(); keys.thisList.Add(Key(100)); var next = Key(200); next.filmPower = .75f; next.interpolateType = LiveCameraInterpolateType.Linear; keys.thisList.Add(next);
        var capture = typeof(LiveTimelineControl).GetMethod("CapturePostFilm", BindingFlags.NonPublic | BindingFlags.Instance);
        bridge.CaptureOwnerTimes(2.5f, 2.4f); bridge.BeginFrame(125);
        bridge.CaptureSelected(0, 125, new LiveTimelineKeyMultiCameraPositionData(), null, null, camera);
        bridge.CapturePostEffect(default); capture.Invoke(control, new object[] { keys, 125, 1, camera, 17 }); bridge.CompleteFrame();
        Check(bridge.LastFrame.TryGetFilm(camera, 1, out var selected) && selected.Value.HasValue && selected.Value.Value.filmPower == .375f && selected.UsedRatio == .25f, "Actual Control key search and interpolation reach current Film.");
        Check(!bridge.LastFrame.TryGetFilm(other, 1, out _) && !bridge.LastFrame.TryGetFilm(camera, 2, out _), "Camera and layer have independent membership.");
        var provider = new OriginalLivePostFilmProvider(() => bridge.LastFrame, Profile, 1); int draws = 0;
        var material = Own(new Material(Shader.Find("Hidden/OriginalRendering/PostProcessValidation")));
        using (var chain = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(material, 0, 64, 32, null, new OriginalPostProcessChain.Binding("_MainTex", -1))))
        using (var producer = new OriginalCameraProducer(64, 32))
        {
            var adapter = new OriginalLivePostProcessParameters(() => bridge.LastFrame, new OriginalPostProcessProducerMath.ConfirmedProfile(.75f, .03125f), (frame, values) =>
            { Check(values.Film.HasValue && values.Film.Value.Power == .375f, "Natural Film provider reaches current GPU conversion."); material.SetVector("_Gain", Vector4.one); material.SetVector("_Offset", new Vector4(values.Film.Value.Power, 0, 0, 0)); draws++; return chain.Process(frame.Source, frame.Color); }, provider.Resolve);
            producer.SetInputFramePostProcessor(adapter, Shader.Find("Hidden/OriginalRendering/DepthCopy"));
            var rt = producer.RenderInput(camera, bridge.LastFrame.Serial); var read = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32); read.WaitForCompletion();
            Check(!read.hasError && read.GetData<byte>()[0] >= 95 && draws == 1 && adapter.ProcessCount == 1, "Film-dependent GPU output completed."); report.gpuFrames++;
            Reject<InvalidOperationException>(() => producer.RenderInput(camera, bridge.LastFrame.Serial + 1), "Stale selected serial cannot render.");
            bridge.BeginFrame(126); bridge.CompleteFrame();
            Check(bridge.LastFrame.Films.Count == 0, "New update cannot retain an older Film.");
        }
        camera.gameObject.SetActive(false); other.gameObject.SetActive(false);
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); string output = null; for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-integrationOutput") output = args[i + 1]; if (output == null) throw new ArgumentException("External output required"); Directory.CreateDirectory(output); report = new Report();
        var oldG = GraphicsSettings.defaultRenderPipeline; var oldQ = QualitySettings.renderPipeline; var oldActive = RenderTexture.active; bool oldSrgb = GL.sRGBWrite; int exit = 0;
        try { MathChecks(); Pipeline(false); Pipeline(true); report.status = "PostFilmValidated_TestOnly"; }
        catch (Exception e) { report.status = "Failed"; report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally { QualitySettings.renderPipeline = oldQ; GraphicsSettings.defaultRenderPipeline = oldG; RenderTexture.active = oldActive; GL.sRGBWrite = oldSrgb; foreach (var item in owned) if (item) Object.DestroyImmediate(item); owned.Clear(); File.WriteAllText(Path.Combine(output, "integration-summary.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit); }
    }
}
