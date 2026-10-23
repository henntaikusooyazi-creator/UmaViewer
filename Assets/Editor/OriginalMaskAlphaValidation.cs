using System;
using System.IO;
using System.Linq;
using Gallop.Live.Rendering;
using UnityEngine;
using A = Gallop.Live.Rendering.OriginalCompositeCueAdapter;

// Dedicated acceptance only. External observations stay outside the repository.
// This compares alpha; artificial RGB and sample bias do not establish a camera or sampler contract.
public static class OriginalMaskAlphaValidation
{
    [Serializable] public sealed class Draw
    {
        public Vector4 transform, scaleOffset, meshUvScaleOffset;
        public float texelSizeY, thickness, fade;
    }
    [Serializable] public sealed class Input
    {
        public int width, height, gridWidth, gridHeight;
        public bool flipGridY;
        public Draw right, left;
        public int[] rightAlpha, leftAlpha;
    }
    [Serializable] public sealed class Comparison
    {
        public int samples, exact, withinOne, maximumError;
    }
    [Serializable] public sealed class Receipt
    {
        public string source, pipeline;
        public Comparison rightFromClear, leftFromObservedRight;
        public bool alphaOnly = true, rightInitialAlphaZeroPolicy = true;
        public bool originalProjectionEstablished = false, originalMipBiasObserved = false;
        public bool OriginalVerified = false, runtime_contract = false, SourceToViewerClockContract = false;
    }
    sealed class Samples { public byte[] right, left; }
    static readonly Vector4 Identity = new Vector4(1, 1, 0, 0);

    public static Receipt Exercise(bool urp, string externalPath, Action<bool, string> check)
    {
        var synthetic = new Input { width = 97, height = 55, gridWidth = 13, gridHeight = 7,
            right = new Draw { transform = new Vector4(-.1f, 0, .13f, .02f), scaleOffset = Identity,
                meshUvScaleOffset = Identity, texelSizeY = 1f / 55, thickness = .21f, fade = .8f },
            left = new Draw { transform = new Vector4(.14f, -.03f, -.2f, .015f),
                scaleOffset = new Vector4(.9f, .85f, .05f, .025f), meshUvScaleOffset = new Vector4(.8f, .9f, .1f, .05f),
                texelSizeY = -1f / 55, thickness = .19f, fade = .65f } };
        Reference(synthetic);
        var generated = Render(synthetic, false, Color.red, 0, check);
        check(Compare(generated.right, synthetic.rightAlpha).withinOne == synthetic.rightAlpha.Length,
            "Exact source-pixel grid: synthetic right alpha.");
        check(Compare(generated.left, synthetic.leftAlpha).withinOne == synthetic.leftAlpha.Length,
            "Exact source-pixel grid: synthetic left accumulation alpha.");
        var seededSynthetic = Render(synthetic, true, Color.red, 0, null);
        check(Compare(seededSynthetic.left, synthetic.leftAlpha).withinOne == synthetic.leftAlpha.Length,
            "Observed-prior-alpha seeding preserves the synthetic left comparison.");
        var changedRgb = Render(synthetic, false, new Color(0, 1, 1, 0), 4, null);
        check(generated.right.SequenceEqual(changedRgb.right) && generated.left.SequenceEqual(changedRgb.left),
            "Mask alpha is independent of artificial source RGBA and explicit test sample bias.");
        synthetic.flipGridY = true;
        var wrongOrientation = Render(synthetic, false, Color.red, 0, null);
        check(Compare(wrongOrientation.right, synthetic.rightAlpha).withinOne < synthetic.rightAlpha.Length,
            "Wrong supplied grid orientation is detected, never automatically fitted.");
        synthetic.flipGridY = false;
        check(Enumerable.Range(0, synthetic.rightAlpha.Length).Any(i =>
            Math.Abs(synthetic.rightAlpha[i] - (int)Math.Round(255 * Alpha(synthetic.right,
                (i % synthetic.gridWidth + .5) / synthetic.gridWidth, (i / synthetic.gridWidth + .5) / synthetic.gridHeight))) > 1),
            "Rendering at grid resolution would change alpha; original integer source-pixel centers are required.");
        synthetic.width = 0;
        bool rejected = false;
        try { Validate(synthetic); } catch (ArgumentException) { rejected = true; }
        check(rejected, "Invalid sampling bounds are rejected before allocating GPU resources.");

        if (externalPath == null) return new Receipt { source = "SyntheticOnly", pipeline = urp ? "URP14" : "BuiltIn" };
        if (new FileInfo(externalPath).Length > 262144) throw new ArgumentException("Bounded external mask-alpha input required.");
        var input = JsonUtility.FromJson<Input>(File.ReadAllText(externalPath));
        Validate(input);
        // First comparison uses the declared clear policy. The second starts with actual
        // saved right alpha, so it does not infer the original right draw's initial contents.
        var actual = Render(input, false, Color.magenta, 0, check);
        var seeded = Render(input, true, Color.magenta, 0, null);
        var receipt = new Receipt { source = "ExplicitExternalSelectedDrawAlpha", pipeline = urp ? "URP14" : "BuiltIn",
            rightFromClear = Compare(actual.right, input.rightAlpha), leftFromObservedRight = Compare(seeded.left, input.leftAlpha) };
        check(receipt.rightFromClear.withinOne == receipt.rightFromClear.samples,
            "External right-stage alpha matches within one byte under explicit initial-clear/grid policy.");
        check(receipt.leftFromObservedRight.withinOne == receipt.leftFromObservedRight.samples,
            "External left-stage alpha matches within one byte using observed prior right alpha.");
        var alternate = Render(input, true, new Color(0, 1, 0, 0), -3, null);
        check(seeded.left.SequenceEqual(alternate.left), "External alpha comparison does not depend on invented original camera RGB or MipBias.");
        return receipt;
    }

    static void Validate(Input input)
    {
        if (input == null || input.width < 1 || input.height < 1 || input.width > 2048 || input.height > 1152 ||
            input.gridWidth < 1 || input.gridHeight < 1 || input.gridWidth > 128 || input.gridHeight > 80 ||
            input.gridWidth > input.width || input.gridHeight > input.height || input.right == null || input.left == null ||
            input.rightAlpha == null || input.leftAlpha == null || input.rightAlpha.Length != input.gridWidth * input.gridHeight ||
            input.leftAlpha.Length != input.rightAlpha.Length || input.rightAlpha.Concat(input.leftAlpha).Any(x => x < 0 || x > 255))
            throw new ArgumentException("Bounded dimensions, two explicit draws and byte-alpha grids required.");
        foreach (var d in new[] { input.right, input.left })
        {
            var values = new[] { d.transform, d.scaleOffset, d.meshUvScaleOffset };
            if (values.Any(v => Enumerable.Range(0, 4).Any(i => !Finite(v[i]))) || !Finite(d.texelSizeY) ||
                !Finite(d.thickness) || d.thickness <= 0 || !Finite(d.fade) || d.fade < 0 || d.fade > 1)
                throw new ArgumentException("Finite explicit mapping and nondegenerate alpha inputs required.");
        }
    }
    static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    static int GridPixel(int index, int size, int grid) => (2 * index + 1) * size / (2 * grid);
    static Comparison Compare(byte[] actual, int[] expected)
    {
        var c = new Comparison { samples = expected.Length };
        for (int i = 0; i < expected.Length; i++)
        {
            int error = Math.Abs(actual[i] - expected[i]);
            if (error == 0) c.exact++;
            if (error <= 1) c.withinOne++;
            c.maximumError = Math.Max(c.maximumError, error);
        }
        return c;
    }
    static OriginalMaskCoverageGenerator.Parameters Parameters(Draw d, float testBias, Action<bool, string> check)
    {
        var mapping = new A.TextureMapping(d.scaleOffset, d.texelSizeY, d.meshUvScaleOffset, testBias);
        var snapshot = A.FromEvaluatedProducer(default,
            new A.CueValues(null, null, null, new Vector2(d.transform.x, d.transform.y), null, null, null, 0),
            new A.DrawState(d.fade, d.thickness, new Vector2(d.transform.z, d.transform.w)), mapping);
        bool ready = snapshot.TryCreatePass0Parameters(out var p, out var unresolved);
        check?.Invoke(ready && unresolved == A.Unresolved.None && p.TextureScaleOffset == d.scaleOffset &&
            p.MeshUvScaleOffset == d.meshUvScaleOffset && p.TextureTexelSizeY == d.texelSizeY,
            "Explicit selected-draw UV mapping reaches the existing adapter and coverage generator.");
        if (!ready) throw new InvalidOperationException("Explicit alpha draw was unresolved.");
        return p;
    }
    static Samples Render(Input input, bool seedObservedRight, Color color, float testBias, Action<bool, string> check)
    {
        Validate(input);
        var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
        Texture2D source = null, seed = null; RenderTexture output = null;
        try
        {
            source = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            source.SetPixel(0, 0, color); source.Apply();
            output = new RenderTexture(input.width, input.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            output.Create();
            var right = Parameters(input.right, testBias, check); var left = Parameters(input.left, testBias, check);
            if (seedObservedRight)
            {
                seed = new Texture2D(input.gridWidth, input.gridHeight, TextureFormat.RGBA32, false, true)
                    { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[input.rightAlpha.Length];
                for (int y = 0; y < input.gridHeight; y++) for (int x = 0; x < input.gridWidth; x++)
                    pixels[(input.flipGridY ? input.gridHeight - 1 - y : y) * input.gridWidth + x] =
                        new Color32(0, 0, 0, (byte)input.rightAlpha[y * input.gridWidth + x]);
                seed.SetPixels32(pixels); seed.Apply();
            }
            var samples = new Samples();
            using (var coverage = new OriginalMaskCoverageGenerator(Shader.Find(OriginalMaskCoverageGenerator.ShaderName)))
            using (var renderer = new OriginalMultiCameraRenderer(Shader.Find(OriginalMultiCameraRenderer.ShaderName), RenderTextureFormat.ARGBFloat))
                renderer.RenderSequential(source,
                    () => seedObservedRight ? new OriginalMultiCameraRenderer.Submission(seed, OriginalMultiCameraRenderer.InputEncoding.CapturedPostBlendLayer) :
                        coverage.GenerateSubmission(source, input.width, input.height, right),
                    () => coverage.GenerateSubmission(source, input.width, input.height, left), output, trace =>
                    {
                        if (trace.Step == OriginalMultiCameraRenderer.Stage.RightMaskDrawn) samples.right = ReadGrid(trace.Accumulation, input);
                        if (trace.Step == OriginalMultiCameraRenderer.Stage.LeftMaskDrawn) samples.left = ReadGrid(trace.Accumulation, input);
                    });
            return samples;
        }
        finally
        {
            RenderTexture.active = active; GL.sRGBWrite = srgb;
            if (output) { output.Release(); UnityEngine.Object.DestroyImmediate(output); }
            if (source) UnityEngine.Object.DestroyImmediate(source);
            if (seed) UnityEngine.Object.DestroyImmediate(seed);
        }
    }
    static byte[] ReadGrid(RenderTexture rt, Input input)
    {
        var active = RenderTexture.active;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
        try
        {
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            var pixels = texture.GetPixels32(); var result = new byte[input.gridWidth * input.gridHeight];
            for (int y = 0; y < input.gridHeight; y++) for (int x = 0; x < input.gridWidth; x++)
            {
                int sy = GridPixel(y, input.height, input.gridHeight);
                if (input.flipGridY) sy = input.height - 1 - sy;
                result[y * input.gridWidth + x] = pixels[sy * input.width + GridPixel(x, input.width, input.gridWidth)].a;
            }
            return result;
        }
        finally { RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(texture); }
    }
    static void Reference(Input input)
    {
        int count = input.gridWidth * input.gridHeight;
        input.rightAlpha = new int[count]; input.leftAlpha = new int[count];
        for (int y = 0; y < input.gridHeight; y++) for (int x = 0; x < input.gridWidth; x++)
        {
            double u = (GridPixel(x, input.width, input.gridWidth) + .5) / input.width;
            double v = (GridPixel(y, input.height, input.gridHeight) + .5) / input.height;
            int i = y * input.gridWidth + x;
            input.rightAlpha[i] = (int)Math.Round(255 * Alpha(input.right, u, v));
            input.leftAlpha[i] = Math.Min(255, (int)Math.Round(255 * Alpha(input.left, u, v) + input.rightAlpha[i]));
        }
    }
    static double Alpha(Draw d, double u, double v)
    {
        u = (u * d.meshUvScaleOffset.x + d.meshUvScaleOffset.z) * d.scaleOffset.x + d.scaleOffset.z;
        v = (v * d.meshUvScaleOffset.y + d.meshUvScaleOffset.w) * d.scaleOffset.y + d.scaleOffset.w;
        if (d.texelSizeY < 0) v = 1 - v;
        u += d.transform.x; v += d.transform.y;
        double Boundary(int sign)
        {
            double a = (d.transform.z + sign * d.transform.w) * (double)6.283185005187988f;
            return (u - .5) * Math.Sin(a) + (v - .5) * Math.Cos(a) + .5;
        }
        double q = Math.Min(Boundary(1), Boundary(-1)), h = .5 * d.thickness + .5;
        double alpha = q >= h ? 1 : (d.thickness - Math.Max(0, Math.Min(h - q, d.thickness))) / d.thickness;
        return alpha * (u >= 0 && u <= 1 && v >= 0 && v <= 1 ? 1 : 0) * d.fade;
    }
}
