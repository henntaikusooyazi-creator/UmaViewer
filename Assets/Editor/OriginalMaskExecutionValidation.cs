using System;
using System.Collections.Generic;
using System.IO;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Explicit pixel coordinates and evaluated mapping only. No native layout or frame inference.
public static class OriginalMaskExecutionValidation
{
    [Serializable] public sealed class Input
    {
        public int width, height;
        public Vector4 transform, scaleOffset;
        public float thickness, fade, texelSizeY;
        public int[] sampleX, sampleY, beforeAlpha, afterAlpha;
        public int[] compositeSource, compositeBase, compositeOutput;
    }
    [Serializable] sealed class Comparison
    {
        public string pipeline, source;
        public int maskSamples, alphaExact, alphaWithinOne, maximumAlphaError, compositePixels, rgbaExact;
    }
    [Serializable] sealed class Report
    {
        public string status = "Running", failure;
        public int checks;
        public List<Comparison> comparisons = new List<Comparison>();
        public bool OriginalVerified = false, runtime_contract = false, SourceToViewerClockContract = false;
        public bool originalViewerSameFrame = false, projectionConstructorEstablished = false;
        public bool currentVsProgramIdentityEstablished = false, naturalMappingProducerEstablished = false;
    }
    static Report report;
    static readonly List<Object> owned = new List<Object>();
    static T Own<T>(T item) where T : Object { owned.Add(item); return item; }
    static void Check(bool ok, string reason) { report.checks++; if (!ok) throw new InvalidOperationException(reason); }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static void Validate(Input input)
    {
        if (input == null || input.width <= 0 || input.height <= 0 || input.width > 4096 || input.height > 4096 ||
            input.sampleX == null || input.sampleX.Length == 0 || input.sampleX.Length > 256 ||
            input.sampleY == null || input.beforeAlpha == null || input.afterAlpha == null ||
            input.sampleY.Length != input.sampleX.Length || input.beforeAlpha.Length != input.sampleX.Length ||
            input.afterAlpha.Length != input.sampleX.Length || !Finite(input.thickness) || input.thickness <= 0 ||
            !Finite(input.fade) || input.fade < 0 || input.fade > 1 || !Finite(input.texelSizeY))
            throw new ArgumentException("Bounded, evaluated draw and exact sparse samples required.");
        for (int c = 0; c < 4; c++) if (!Finite(input.transform[c]) || !Finite(input.scaleOffset[c])) throw new ArgumentException("Finite mapping required.");
        var coordinates = new HashSet<long>();
        for (int i = 0; i < input.sampleX.Length; i++)
            if (input.sampleX[i] < 0 || input.sampleX[i] >= input.width || input.sampleY[i] < 0 || input.sampleY[i] >= input.height ||
                input.beforeAlpha[i] < 0 || input.beforeAlpha[i] > 255 || input.afterAlpha[i] < 0 || input.afterAlpha[i] > 255 ||
                !coordinates.Add((long)input.sampleY[i] * input.width + input.sampleX[i])) throw new ArgumentException("Unique in-bounds pixel coordinates required.");
        if (input.compositeSource == null || input.compositeSource.Length == 0 || input.compositeSource.Length % 4 != 0 || input.compositeSource.Length > 1024 ||
            input.compositeBase == null || input.compositeOutput == null || input.compositeBase.Length != input.compositeSource.Length || input.compositeOutput.Length != input.compositeSource.Length)
            throw new ArgumentException("Aligned bounded composite RGBA arrays required.");
        foreach (var values in new[] { input.compositeSource, input.compositeBase, input.compositeOutput }) foreach (int value in values)
            if (value < 0 || value > 255) throw new ArgumentException("RGBA bytes required.");
    }
    static Texture2D Texture(int width, int height, Color32[] pixels)
    {
        var texture = Own(new Texture2D(width, height, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
        texture.SetPixels32(pixels); texture.Apply(); return texture;
    }
    static RenderTexture Target(int width, int height)
    {
        var rt = Own(new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
        Check(rt.Create(), "Explicit target creation."); return rt;
    }
    static Color32[] Read(RenderTexture target)
    {
        var active = RenderTexture.active;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
        try { RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply(); return texture.GetPixels32(); }
        finally { RenderTexture.active = active; Object.DestroyImmediate(texture); }
    }
    static Color32[] Colors(int[] values)
    {
        var pixels = new Color32[values.Length / 4];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)values[i*4], (byte)values[i*4+1], (byte)values[i*4+2], (byte)values[i*4+3]);
        return pixels;
    }
    static void Exercise(Input input, bool urp, string source)
    {
        Validate(input);
        var result = new Comparison { pipeline = urp ? "URP14" : "BuiltIn", source = source, maskSamples = input.sampleX.Length };
        report.comparisons.Add(result);
        var white = Texture(1, 1, new[] { new Color32(255, 255, 255, 255) });
        var empty = Texture(1, 1, new[] { new Color32(0, 0, 0, 0) });
        var prior = new Color32[input.width * input.height];
        for (int i = 0; i < input.sampleX.Length; i++) prior[input.sampleY[i]*input.width+input.sampleX[i]].a = (byte)input.beforeAlpha[i];
        var seed = Texture(input.width, input.height, prior);
        var output = Target(input.width, input.height);
        using (var coverage = new OriginalMaskCoverageGenerator(Shader.Find(OriginalMaskCoverageGenerator.ShaderName)))
        using (var renderer = new OriginalMultiCameraRenderer(Shader.Find(OriginalMultiCameraRenderer.ShaderName), RenderTextureFormat.ARGBFloat))
        {
            var parameters = new OriginalMaskCoverageGenerator.Parameters(input.transform, input.thickness, input.fade, input.scaleOffset, input.texelSizeY);
            var draw = coverage.GenerateSubmission(white, input.width, input.height, parameters);
            renderer.Render(empty, new OriginalMultiCameraRenderer.Submission(seed, OriginalMultiCameraRenderer.InputEncoding.CapturedPostBlendLayer), draw, output, trace =>
            {
                if (trace.Step != OriginalMultiCameraRenderer.Stage.LeftMaskDrawn) return;
                var pixels = Read(trace.Accumulation);
                for (int i = 0; i < input.sampleX.Length; i++)
                {
                    int error = Math.Abs(pixels[input.sampleY[i]*input.width+input.sampleX[i]].a - input.afterAlpha[i]);
                    if (error == 0) result.alphaExact++; if (error <= 1) result.alphaWithinOne++; result.maximumAlphaError = Math.Max(result.maximumAlphaError, error);
                }
            });
            Check(result.alphaWithinOne == result.maskSamples, "Current mapping disagrees with sparse observed alpha.");
            int count = input.compositeSource.Length / 4;
            var mixed = Texture(count, 1, Colors(input.compositeSource)); var basis = Texture(count, 1, Colors(input.compositeBase));
            var final = Target(count, 1);
            renderer.Render(basis, new OriginalMultiCameraRenderer.Submission(mixed, OriginalMultiCameraRenderer.InputEncoding.CapturedPostBlendLayer),
                new OriginalMultiCameraRenderer.Submission(empty, OriginalMultiCameraRenderer.InputEncoding.StraightColorWithCoverage), final);
            var actual = Read(final); var expected = Colors(input.compositeOutput); result.compositePixels = count;
            for (int i = 0; i < count; i++) if (actual[i].Equals(expected[i])) result.rgbaExact++;
            Check(result.rgbaExact == count, "Same-draw M/D/O composite RGBA differs.");
        }
    }
    static Input Synthetic()
    {
        var input = new Input { width = 64, height = 32, scaleOffset = new Vector4(1, 1, 0, 0), thickness = .25f, fade = .5f, texelSizeY = 1f/32,
            sampleX = new[] { 4, 32, 60 }, sampleY = new[] { 2, 16, 30 }, beforeAlpha = new[] { 0, 32, 64 }, afterAlpha = new[] { 0, 104, 192 },
            compositeSource = new[] { 20, 80, 240, 0, 40, 160, 80, 255 }, compositeBase = new[] { 200, 30, 10, 55, 100, 30, 60, 77 },
            compositeOutput = new[] { 200, 30, 10, 55, 40, 160, 80, 77 } };
        return input;
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-integrationOutput");
        if (at < 0 || at+1 >= args.Length) throw new ArgumentException("External output required.");
        string repository = Directory.GetParent(Application.dataPath).FullName + Path.DirectorySeparatorChar;
        string output = Path.GetFullPath(args[at+1]);
        if (output.StartsWith(repository, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Keep output outside the repository.");
        Directory.CreateDirectory(output); report = new Report();
        int observedAt = Array.IndexOf(args, "-observedMaskExecution"); Input observed = null;
        if (observedAt >= 0) { if (observedAt+1 >= args.Length) throw new ArgumentException("Observation path required."); string path = Path.GetFullPath(args[observedAt+1]); if (path.StartsWith(repository, StringComparison.OrdinalIgnoreCase) || new FileInfo(path).Length > 65536) throw new ArgumentException("Bounded external observation required."); observed = JsonUtility.FromJson<Input>(File.ReadAllText(path)); Validate(observed); }
        var graphics = GraphicsSettings.defaultRenderPipeline; var quality = QualitySettings.renderPipeline; var active = RenderTexture.active; bool srgb = GL.sRGBWrite; int exit = 0;
        try
        {
            foreach (int mode in new[] { 0, 1, 2, 3, 4 })
            {
                var invalid = Synthetic();
                if (mode == 0) invalid.sampleX[0] = invalid.width;
                if (mode == 1) invalid.sampleY = new[] { 0 };
                if (mode == 2) invalid.sampleX[1] = invalid.sampleX[0];
                if (mode == 2) invalid.sampleY[1] = invalid.sampleY[0];
                if (mode == 3) invalid.thickness = float.NaN;
                if (mode == 4) invalid.compositeOutput[0] = 256;
                bool rejected = false; try { Validate(invalid); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Malformed sparse input rejected before GPU allocation.");
            }
            foreach (bool urp in new[] { false, true })
            {
                if (urp) { var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>()); var asset = Own(UniversalRenderPipelineAsset.Create(data)); GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset; }
                else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
                GL.sRGBWrite = false; Exercise(Synthetic(), urp, "Artificial"); if (observed != null) Exercise(observed, urp, "ExternalEvaluatedInputs");
            }
            report.status = "SparseMaskAndCompositeValidated_Limited";
        }
        catch (Exception e) { report.status = "Failed"; report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally { QualitySettings.renderPipeline = quality; GraphicsSettings.defaultRenderPipeline = graphics; RenderTexture.active = active; GL.sRGBWrite = srgb; foreach (var item in owned) if (item) { if (item is RenderTexture rt) rt.Release(); Object.DestroyImmediate(item); } owned.Clear(); File.WriteAllText(Path.Combine(output, "integration-summary.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit); }
    }
}
