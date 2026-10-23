using System;
using System.Collections.Generic;
using System.IO;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Artificial inputs only. Real captured programs and images stay external.
public static class OriginalPostProcessValidation
{
    [Serializable] sealed class Report { public string status = "Running", failure; public int checks, pixels; public bool OriginalVerified = false, runtime_contract = false; }
    sealed class Callback : IOriginalCameraPostProcessor
    { public Func<Camera, RenderTexture, RenderTexture> Apply; public RenderTexture Process(Camera c, RenderTexture t) => Apply(c, t); }
    static Report report;
    static readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    static T Own<T>(T x) where T : UnityEngine.Object { owned.Add(x); return x; }
    static void Check(bool value, string message) { report.checks++; if (!value) throw new InvalidOperationException(message); }
    static void Reject<T>(Action action) where T : Exception { bool rejected = false; try { action(); } catch (T) { rejected = true; } Check(rejected, typeof(T).Name); }
    static RenderTexture Target() { var t = Own(new RenderTexture(64, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)); t.Create(); return t; }
    static Color32[] Read(RenderTexture t) { var prior = RenderTexture.active; var image = Own(new Texture2D(t.width, t.height, TextureFormat.RGBA32, false, true)); try { RenderTexture.active = t; image.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0); image.Apply(); return image.GetPixels32(); } finally { RenderTexture.active = prior; } }
    static int Byte(float x) => Mathf.Clamp(Mathf.RoundToInt(x * 255), 0, 255);
    static void Pipeline(bool urp)
    {
        if (urp) { var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>()); var asset = Own(UniversalRenderPipelineAsset.Create(data)); asset.supportsHDR = false; asset.msaaSampleCount = 1; GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset; }
        else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
        var camera = Own(new GameObject("Synthetic.PostProcess.Camera")).AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.17f, .31f, .43f, 1);
        var shader = Shader.Find("Hidden/OriginalRendering/PostProcessValidation"); Check(shader && shader.isSupported, "Synthetic shader available.");
        var a = Own(new Material(shader)); var b = Own(new Material(shader)); a.SetVector("_Gain", new Vector4(.5f, .75f, .25f, 1)); b.SetVector("_Gain", new Vector4(.5f, .5f, .5f, 1)); b.SetVector("_OtherGain", new Vector4(.25f, .25f, .25f, 0)); b.SetVector("_Offset", new Vector4(.05f, .02f, .03f, 0));
        var sentinel = Target(); var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
        try
        {
            using (var producer = new OriginalCameraProducer(64, 32))
            using (var chain = new OriginalPostProcessChain(
                new OriginalPostProcessChain.Pass(a, 0, 32, 16, null, new OriginalPostProcessChain.Binding("_MainTex", -1)),
                new OriginalPostProcessChain.Pass(b, 0, 64, 32, null, new OriginalPostProcessChain.Binding("_MainTex", 0), new OriginalPostProcessChain.Binding("_OtherTex", -1))))
            {
                var raw = producer.RenderInput(camera); var original = Read(raw); Check(raw == producer.SharedInput, "Legacy input target retained without opt-in.");
                producer.SetInputPostProcessor(chain); RenderTexture.active = sentinel; GL.sRGBWrite = true;
                var processed = producer.RenderInput(camera); Check(processed != raw && processed == producer.SharedInput && chain.ProcessCount == 1, "Camera output is routed through the explicit chain.");
                Check(RenderTexture.active == sentinel && GL.sRGBWrite, "Camera and chain restore active target and sRGB write."); Check(camera.targetTexture == null && !camera.enabled, "Source camera state preserved.");
                var pixels = Read(processed); int max = 0; bool alpha = true; for (int i = 0; i < pixels.Length; i++) { var p = original[i]; int[] source = { p.r, p.g, p.b }; float[] gain = { .5f, .75f, .25f }, offset = { .05f, .02f, .03f }; int[] actual = { pixels[i].r, pixels[i].g, pixels[i].b }; for (int c = 0; c < 3; c++) { int first = Byte(source[c] / 255f * gain[c]); int expected = Byte(first / 255f * .5f + source[c] / 255f * .25f + offset[c]); max = Math.Max(max, Math.Abs(actual[c] - expected)); } alpha &= pixels[i].a == p.a; } report.pixels += pixels.Length; Check(alpha, "Synthetic alpha preserved."); Check(max <= 1, "Two-pass, two-input RGB including intermediate UNorm write.");
                int count = chain.ProcessCount; producer.RenderBase(camera); Check(chain.ProcessCount == count, "Base rendering does not inherit the input postprocessor.");
                Reject<ArgumentException>(() => chain.Process(camera, processed));
                var released = Target(); released.Release();
                using (var unavailable = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(a, 0, 64, 32, null, new OriginalPostProcessChain.Binding("_MainTex", -2, released))))
                    Reject<InvalidOperationException>(() => unavailable.Process(camera, raw));
                producer.SetInputPostProcessor(new Callback { Apply = (c, t) => producer.RenderInput(c) }); Reject<InvalidOperationException>(() => producer.RenderInput(camera)); Check(RenderTexture.active == sentinel && GL.sRGBWrite, "Fault and reentry restore render state.");
                producer.SetInputPostProcessor(new Callback { Apply = (c, t) => null }); Reject<InvalidOperationException>(() => producer.RenderInput(camera));
                producer.SetInputPostProcessor(null); Check(producer.RenderInput(camera) == raw, "Detaching restores the established path.");
                Reject<ArgumentException>(() => new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(a, 0, 64, 32, null, new OriginalPostProcessChain.Binding("_MainTex", 0))));
            }
            Check(a && b, "Caller-owned programs survive chain disposal.");
        }
        finally { RenderTexture.active = active; GL.sRGBWrite = srgb; }
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); string output = null; for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-integrationOutput") output = args[i + 1]; if (output == null) throw new ArgumentException("External output required."); Directory.CreateDirectory(output);
        report = new Report(); var oldG = GraphicsSettings.defaultRenderPipeline; var oldQ = QualitySettings.renderPipeline; int exit = 0;
        try { Pipeline(false); Pipeline(true); report.status = "OriginalPostProcessValidated_TestOnly"; }
        catch (Exception e) { report.status = "Failed"; report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally { QualitySettings.renderPipeline = oldQ; GraphicsSettings.defaultRenderPipeline = oldG; foreach (var x in owned) if (x) { if (x is RenderTexture rt) rt.Release(); UnityEngine.Object.DestroyImmediate(x); } owned.Clear(); File.WriteAllText(Path.Combine(output, "integration-summary.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit); }
    }
}
