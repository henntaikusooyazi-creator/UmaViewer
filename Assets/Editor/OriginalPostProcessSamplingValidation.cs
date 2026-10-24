using System;
using System.Collections.Generic;
using System.IO;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Artificial dimensions, temporal policy and deliberately different mip colors.
public static class OriginalPostProcessSamplingValidation
{
    [Serializable] sealed class Report { public string status = "Running", failure; public int checks, pixels, rgbaExact; public bool OriginalVerified = false, runtime_contract = false; }
    static Report report;
    static readonly List<Object> owned = new List<Object>();
    static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Check(bool ok, string message) { report.checks++; if (!ok) throw new InvalidOperationException(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid sampling inputs must be rejected."); }
    sealed class Current : IOriginalCameraFramePostProcessor
    {
        public int Calls;
        public bool Urp;
        public RenderTexture Process(OriginalCameraFrame frame)
        {
            Check(frame.PixelWidth == frame.Color.width, "Private camera width captured while target is bound.");
            Check(frame.DynamicWidthScale == 1f, "Disabled dynamic resolution has identity scale.");
            Check(Urp ? frame.TemporalMipBias.HasValue && frame.TemporalMipBias.Value == 0 : !frame.TemporalMipBias.HasValue, "Temporal value belongs to the private render camera; absent settings stay absent.");
            Check(!frame.StereoEnabled, "Current render is qualified as non-stereo.");
            var sampling = OriginalPostProcessSamplingMath.EvaluateMipBias(frame.PixelWidth, frame.Color.width, frame.DynamicWidthScale, 0);
            Check(sampling == new Vector4(0, 1, 0, 0), "Explicit unscaled no-temporal preview policy."); Calls++; return frame.Color;
        }
    }
    static void MathChecks()
    {
        Check(OriginalPostProcessSamplingMath.EvaluateMipBias(100, 100, 1, 0) == new Vector4(0, 1, 0, 0), "Unscaled producer.");
        Check(OriginalPostProcessSamplingMath.EvaluateMipBias(100, 50, 1, 0) == new Vector4(-1, .5f, 0, 0), "Half-resolution producer.");
        Check(OriginalPostProcessSamplingMath.EvaluateMipBias(100, 100, .25f, 0) == new Vector4(-2, .25f, 0, 0), "Dynamic width multiplication precedes division.");
        Check(OriginalPostProcessSamplingMath.EvaluateMipBias(100, 200, 1, 3) == new Vector4(0, 1, 0, 0), "Upscaling and positive temporal bias are capped.");
        Check(OriginalPostProcessSamplingMath.EvaluateMipBias(100, 50, 1, -2) == new Vector4(-2, .25f, 0, 0), "Temporal bias can dominate spatial bias.");
        var nonPower = OriginalPostProcessSamplingMath.EvaluateMipBias(101, 73, 1, 0);
        Check(BitConverter.ToInt32(BitConverter.GetBytes(nonPower.x), 0) == unchecked((int)0xbeefd068), "Artificial non-power-of-two width ratio rounds before double log.");
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            Reject(() => OriginalPostProcessSamplingMath.EvaluateMipBias(invalid, 1, 1, 0));
            Reject(() => OriginalPostProcessSamplingMath.EvaluateMipBias(1, invalid, 1, 0));
            Reject(() => OriginalPostProcessSamplingMath.EvaluateMipBias(1, 1, invalid, 0));
        }
        Reject(() => OriginalPostProcessSamplingMath.EvaluateMipBias(1, 1, 1, float.NaN));
        Reject(() => OriginalPostProcessSamplingMath.EvaluateMipBias(1, float.MaxValue, 2, 0));
        Reject(() => OriginalPostProcessSamplingMath.EvaluateMipBias(float.Epsilon, float.MaxValue, 1, 0));
    }
    static void Pipeline(bool urp)
    {
        if (urp) { var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>()); var asset = Own(UniversalRenderPipelineAsset.Create(data)); asset.renderScale = 1; GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset; }
        else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
        var camera = Own(new GameObject("Artificial.Sampling.Camera")).AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 0; camera.allowDynamicResolution = false;
        if (urp) camera.GetUniversalAdditionalCameraData().taaSettings.mipBias = -.625f;
        var consumer = new Current { Urp = urp };
        using (var producer = new OriginalCameraProducer(32, 16))
        {
            producer.SetInputFramePostProcessor(consumer, Shader.Find("Hidden/OriginalRendering/DepthCopy"));
            producer.RenderInput(camera, 1); producer.RenderInput(camera, 2); Check(consumer.Calls == 2, "Current frame metadata is supplied on both updates.");
        }
        var source = Own(new Texture2D(16, 16, TextureFormat.RGBA32, true, true));
        var colors = new[] { new Color32(220, 20, 40, 255), new Color32(30, 200, 60, 255), new Color32(20, 40, 220, 255), new Color32(190, 190, 10, 255), new Color32(255, 255, 255, 255) };
        for (int level = 0; level < source.mipmapCount; level++) { int size = Math.Max(1, 16 >> level); var pixels = new Color32[size * size]; for (int i = 0; i < pixels.Length; i++) pixels[i] = colors[level]; source.SetPixels32(pixels, level); } source.Apply(false, false);
        var target = Own(new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)); Check(target.Create(), "Mip test target.");
        var material = Own(new Material(Shader.Find("Hidden/OriginalRendering/PostProcessSamplingValidation")));
        foreach (bool sharpen in new[] { false, true })
        {
            var sampling = OriginalPostProcessSamplingMath.EvaluateMipBias(100, sharpen ? 50 : 100, 1, 0); material.SetVector("_Sampling", sampling); Graphics.Blit(source, target, material);
            var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); request.WaitForCompletion(); Check(!request.hasError, "Mip readback."); var bytes = request.GetData<byte>(); var expected = colors[sharpen ? 0 : 1];
            for (int i = 0; i < bytes.Length; i += 4) { report.pixels++; if (bytes[i] != expected.r || bytes[i + 1] != expected.g || bytes[i + 2] != expected.b || bytes[i + 3] != expected.a) throw new Exception($"Generated mip selection mismatch: {bytes[i]},{bytes[i + 1]},{bytes[i + 2]},{bytes[i + 3]}; expected {expected}; sharpen={sharpen}; urp={urp}."); report.rgbaExact++; }
            Check(sampling.y == (sharpen ? .5f : 1f), "Generated mip gain.");
        }
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-integrationOutput"); if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("External output required."); string output = args[at + 1]; Directory.CreateDirectory(output); report = new Report();
        var g = GraphicsSettings.defaultRenderPipeline; var q = QualitySettings.renderPipeline; var active = RenderTexture.active; bool srgb = GL.sRGBWrite; int exit = 0;
        try { GL.sRGBWrite = false; MathChecks(); Pipeline(false); Pipeline(true); report.status = "PostProcessSamplingValidated_TestOnly"; }
        catch (Exception e) { report.status = "Failed"; report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally { QualitySettings.renderPipeline = q; GraphicsSettings.defaultRenderPipeline = g; RenderTexture.active = active; GL.sRGBWrite = srgb; foreach (var item in owned) if (item) { if (item is RenderTexture rt) rt.Release(); Object.DestroyImmediate(item); } owned.Clear(); File.WriteAllText(Path.Combine(output, "integration-summary.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit); }
    }
}
