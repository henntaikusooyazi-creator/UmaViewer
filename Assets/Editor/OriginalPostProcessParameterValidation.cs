using System;
using System.Collections.Generic;
using System.IO;
using Gallop.Live.Cutt;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Artificial inputs only. Original profiles, addresses and captures are external.
public static class OriginalPostProcessParameterValidation
{
    [Serializable] sealed class Report { public string status = "Running", failure; public int checks, pixels, maxRgbError; public bool OriginalVerified = false, runtime_contract = false; }
    static readonly List<Object> owned = new List<Object>();
    static Report report;
    static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Check(bool value, string message) { report.checks++; if (!value) throw new InvalidOperationException(message); }
    static void Reject<T>(Action action, string message) where T : Exception
    { bool rejected = false; try { action(); } catch (T) { rejected = true; } Check(rejected, message); }
    static byte[] Read(RenderTexture value) { var request = AsyncGPUReadback.Request(value, 0, TextureFormat.RGBA32); request.WaitForCompletion(); if (request.hasError) throw new Exception("Readback"); return request.GetData<byte>().ToArray(); }
    static OriginalPostProcessProducerMath.ConfirmedProfile Profile => new OriginalPostProcessProducerMath.ConfirmedProfile(.75f, .03125f);
    static PostEffectUpdateInfo_BloomDiffusion Payload => new PostEffectUpdateInfo_BloomDiffusion
    { IsEnabledBloom = true, IsEnabledDiffusion = true, BloomBlurSize = 1.25f, threshold = .25f, intensity = .75f, bloomDofWeight = .375f,
      diffusionBlurSize = 1.5f, diffusionBright = .2f, diffusionSaturation = .4f, diffusionContrast = .6f, diffusionThreshold = .8f };
    static OriginalPostProcessProducerMath.FilmInputs Film => new OriginalPostProcessProducerMath.FilmInputs
    { PostFilmPower = .3f, DepthPower = .5f, DepthClip = .25f, PostFilmOffsetParam = new Vector2(.1f, -.2f),
      PostFilmOptionParam = new Vector4(1, 2, 3, 4), PostFilmColor0 = Color.red, PostFilmColor1 = Color.green,
      PostFilmColor2 = Color.blue, PostFilmColor3 = Color.white, RollParameter = new Vector4(.3f, .6f, 99, .4f), ScaleParameter = new Vector4(2, 3, 4, 5), IsEnableDepth = true };
    static void MathChecks()
    {
        var value = OriginalPostProcessProducerMath.EvaluateBloomDiffusion(Payload, 65, 33, Profile);
        Check(value.PixelSize == new Vector4(.0234375f, .046875f, 0, 0), "Scaled integer dimensions truncate before division.");
        Check(value.Color == new Vector4(.2f, .4f, .6f, .8f), "Brightness/saturation/contrast/threshold keep their component order.");
        Check(value.InitialBloom == new Vector4(.9375f, 0, .25f, .75f) && value.BlurBloom.y == .0390625f, "Initial and blur Bloom parameters remain distinct.");
        Check(value.BloomDofWeight == .375f && value.BloomIsScreenBlend == 1, "Weight and screen mode are independent scalars.");
        var add = Payload; add.BloomBlendMode = (Gallop.ImageEffect.DofDiffusionBloomOverlayParam.BloomScreenBlendMode)7;
        Check(OriginalPostProcessProducerMath.EvaluateBloomDiffusion(add, 65, 33, Profile).BloomIsScreenBlend == 0, "Every nonzero mode follows the observed non-screen branch.");
        Check(OriginalPostProcessProducerMath.SourcePrefilterParameter == new Vector4(0, 0, 0, 1), "Prefilter parameter is an explicit branch value.");
        Reject<ArgumentException>(() => new OriginalPostProcessProducerMath.ConfirmedProfile(0, 1), "Missing profile dependency cannot select a default.");
        Reject<ArgumentException>(() => new OriginalPostProcessProducerMath.ConfirmedProfile(1, float.NaN), "Nonfinite profile rejected.");
        Reject<ArgumentNullException>(() => OriginalPostProcessProducerMath.EvaluateBloomDiffusion(Payload, 1, 1, null), "Profile is mandatory.");
        Reject<ArgumentOutOfRangeException>(() => OriginalPostProcessProducerMath.EvaluateBloomDiffusion(Payload, 0, 33, Profile), "Zero width rejected.");
        Reject<ArgumentOutOfRangeException>(() => OriginalPostProcessProducerMath.EvaluateBloomDiffusion(Payload, 1, 1, Profile), "Subpixel sampling dimension rejected.");
        Reject<ArgumentOutOfRangeException>(() => OriginalPostProcessProducerMath.EvaluateBloomDiffusion(Payload, int.MaxValue, 33, new OriginalPostProcessProducerMath.ConfirmedProfile(2, 1)), "Integer conversion overflow rejected.");
        var source = Film; var film = OriginalPostProcessProducerMath.EvaluateFilm(source, 65, 33);
        Check(film.Power == .3f && film.DepthPower == .5f && film.DepthClip == .75f, "Film and depth scalars are evaluated separately.");
        Check(film.Offset == new Vector4(.1f, -.2f, 0, 0) && film.Option == source.PostFilmOptionParam, "Film offset padding and option vector.");
        Check(film.Color0 == (Vector4)Color.red && film.Color1 == (Vector4)Color.green && film.Color2 == (Vector4)Color.blue && film.Color3 == (Vector4)Color.white, "Film colors preserve all four inputs.");
        Check(film.Roll.x == .3f && film.Roll.y == .6f && film.Roll.z == 65f / 33f && film.Roll.w == .4f && source.RollParameter.z == 99, "Only the output roll aspect changes; input is immutable.");
        Check(film.Scale == source.ScaleParameter && film.IsWithoutDepth == 0 && film.IsInverseVignette == 0, "Film scale and explicit depth/vignette flags.");
        source.DepthClip = 2; source.IsEnableDepth = false; source.InverseVignette = true;
        film = OriginalPostProcessProducerMath.EvaluateFilm(source, 65, 33);
        Check(film.DepthClip == 0 && film.IsWithoutDepth == 1 && film.IsInverseVignette == 1, "Depth bypass is not a guessed depth transformation.");
        source.DepthClip = -.25f; Check(OriginalPostProcessProducerMath.EvaluateFilm(source, 65, 33).DepthClip == 1.25f, "Depth clip is not clamped to a new range.");
        source.LayerMode = 1;
        Reject<NotSupportedException>(() => OriginalPostProcessProducerMath.EvaluateFilm(source, 65, 33), "Unqualified movie preparation is not inferred.");
    }
    static void Pipeline(bool urp)
    {
        if (urp) { var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>()); var asset = Own(UniversalRenderPipelineAsset.Create(data)); asset.msaaSampleCount = 1; asset.supportsHDR = false; GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset; }
        else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
        var camera = Own(new GameObject("Artificial.Parameter.Camera")).AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f, .4f, .6f, 1);
        var other = Own(new GameObject("Artificial.Other.Camera")).AddComponent<Camera>(); other.enabled = false;
        var bridge = new OriginalLiveTimelineBridge(); bridge.UseConfirmedOwnerClock(64); bridge.CaptureOwnerTimes(1, .5f); bridge.BeginFrame(60); bridge.CaptureSelected(0, 60, new LiveTimelineKeyMultiCameraPositionData(), null, null, camera); bridge.CapturePostEffect(Payload); bridge.CompleteFrame();
        var material = Own(new Material(Shader.Find("Hidden/OriginalRendering/PostProcessValidation")));
        OriginalLivePostProcessParameters.FilmObservation? film = null; int calls = 0;
        using (var chain = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(material, 0, 64, 32, null, new OriginalPostProcessChain.Binding("_MainTex", -1))))
        using (var producer = new OriginalCameraProducer(64, 32))
        {
            var adapter = new OriginalLivePostProcessParameters(() => bridge.LastFrame, Profile, (frame, values) =>
            {
                calls++; Check(values.Input.intensity == Payload.intensity && frame.SelectedSerial == bridge.LastFrame.Serial, "Typed payload and current frame reach producer conversion together.");
                if (!film.HasValue) { Check(!values.Film.HasValue, "Absent film remains Unknown."); return frame.Color; }
                Check(values.Film.HasValue && values.Film.Value.Power == .3f, "Explicit same-update film reaches conversion.");
                var before = Read(frame.Color); var gain = new Vector4(.5f, .75f, .25f, 1); var offset = new Vector4(values.BloomDiffusion.PixelSize.x, values.BloomDiffusion.BlurBloom.y, values.Film.Value.Power * .1f, 0);
                material.SetVector("_Gain", gain); material.SetVector("_OtherGain", Vector4.zero); material.SetVector("_Offset", offset);
                var target = chain.Process(frame.Source, frame.Color); var actual = Read(target); int maximum = 0;
                for (int i = 0; i < before.Length; i += 4) for (int c = 0; c < 3; c++) maximum = Math.Max(maximum, Math.Abs(actual[i + c] - Mathf.Clamp(Mathf.RoundToInt(before[i + c] * gain[c] + offset[c] * 255), 0, 255)));
                report.pixels += before.Length / 4; report.maxRgbError = Math.Max(report.maxRgbError, maximum); Check(maximum <= 1, "Producer-generated vectors reach real GPU arithmetic."); return target;
            }, frame => film);
            producer.SetInputFramePostProcessor(adapter, Shader.Find("Hidden/OriginalRendering/DepthCopy"));
            producer.RenderInput(camera, bridge.LastFrame.Serial); Check(adapter.ProcessCount == 1 && chain.ProcessCount == 0, "Unknown film does not schedule the film-dependent test draw.");
            film = new OriginalLivePostProcessParameters.FilmObservation(bridge.LastFrame.Serial, camera, Film); producer.RenderInput(camera, bridge.LastFrame.Serial);
            Check(adapter.ProcessCount == 2 && chain.ProcessCount == 1, "Confirmed conversion and GPU draw complete once.");
            film = new OriginalLivePostProcessParameters.FilmObservation(bridge.LastFrame.Serial - 1, camera, Film);
            Reject<InvalidOperationException>(() => producer.RenderInput(camera, bridge.LastFrame.Serial), "Stale film serial rejected before rendering.");
            film = new OriginalLivePostProcessParameters.FilmObservation(bridge.LastFrame.Serial, other, Film);
            Reject<InvalidOperationException>(() => producer.RenderInput(camera, bridge.LastFrame.Serial), "Foreign-camera film rejected before rendering.");
            Check(adapter.ProcessCount == 2 && calls == 2 && chain.ProcessCount == 1, "Rejected film cannot advance success counts.");
            Reject<InvalidOperationException>(() => producer.RenderInput(camera, bridge.LastFrame.Serial + 1), "Existing selected-update gate stays in force.");
            bridge.BeginFrame(120); bridge.CaptureOwnerTimes(2, 1); bridge.CaptureSelected(0, 120, new LiveTimelineKeyMultiCameraPositionData(), null, null, camera); bridge.CompleteFrame();
            Reject<InvalidOperationException>(() => producer.RenderInput(camera, bridge.LastFrame.Serial), "Missing Bloom payload cannot reuse old conversion.");
        }
        camera.gameObject.SetActive(false); other.gameObject.SetActive(false);
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); string output = null; for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-integrationOutput") output = args[i + 1]; if (output == null) throw new ArgumentException("External output required"); Directory.CreateDirectory(output); report = new Report();
        var oldG = GraphicsSettings.defaultRenderPipeline; var oldQ = QualitySettings.renderPipeline; var oldActive = RenderTexture.active; bool oldSrgb = GL.sRGBWrite; int exit = 0;
        try { MathChecks(); Pipeline(false); Pipeline(true); report.status = "PostProcessParametersValidated_TestOnly"; }
        catch (Exception e) { report.status = "Failed"; report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally { QualitySettings.renderPipeline = oldQ; GraphicsSettings.defaultRenderPipeline = oldG; RenderTexture.active = oldActive; GL.sRGBWrite = oldSrgb; foreach (var item in owned) if (item) Object.DestroyImmediate(item); owned.Clear(); File.WriteAllText(Path.Combine(output, "integration-summary.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit); }
    }
}
