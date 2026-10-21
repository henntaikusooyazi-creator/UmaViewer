using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using A = Gallop.Live.Rendering.OriginalCompositeCueAdapter;
using R = Gallop.Live.Rendering.OriginalMultiCameraRenderer;

// Public artificial inputs only. No game captures, shader binaries or asset data.
public static class OriginalRenderingValidation
{
    [Serializable] sealed class Report
    {
        public string status = "Running", unity, graphics, failure;
        public int checks, coverageSamples;
        public bool TestOnly = true, OriginalVerified = false, runtime_contract = false;
        public string Phase257cause = "Unknown", fullViewerIntegration = "NotTested";
    }
    static readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    static readonly Vector4 Identity = new Vector4(1, 1, 0, 0);
    static Report report;
    static void Check(bool value, string message)
    { report.checks++; if (!value) throw new InvalidOperationException(message); }
    static T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
    static RenderTexture Target(int w = 32, int h = 16)
    {
        var rt = Own(new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
        rt.filterMode = FilterMode.Point; rt.wrapMode = TextureWrapMode.Clamp;
        if (!rt.Create()) throw new InvalidOperationException("Synthetic RT creation failed.");
        return rt;
    }
    static Texture2D Image(Color32 color, int w = 32, int h = 16)
    {
        var t = Own(new Texture2D(w, h, TextureFormat.RGBA32, false, true));
        t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp;
        t.SetPixels32(Enumerable.Repeat(color, w * h).ToArray()); t.Apply(); return t;
    }
    static Color[] Read(RenderTexture rt, bool floating = false)
    {
        var old = RenderTexture.active;
        var t = new Texture2D(rt.width, rt.height, floating ? TextureFormat.RGBAFloat : TextureFormat.RGBA32, false, true);
        try { RenderTexture.active = rt; t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply(); return t.GetPixels(); }
        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(t); }
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-validationOutput");
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("-validationOutput");
        string output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
        report = new Report { unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString() };
        var oldActive = RenderTexture.active; bool oldSrgb = GL.sRGBWrite;
        var oldGraphics = GraphicsSettings.defaultRenderPipeline; var oldQuality = QualitySettings.renderPipeline;
        int before = Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t => t.name.StartsWith("Original."));
        try
        {
            Check(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11, "D3D11 device required for this acceptance.");
            foreach (var name in new[] { R.ShaderName, OriginalMaskCoverageGenerator.ShaderName, LiveDiagonalCompositeTestOnly.ShaderName })
            {
                var shader = Shader.Find(name);
                Check(shader && shader.isSupported, "Shader supported: " + name);
                Check(!ShaderUtil.ShaderHasError(shader), "Shader compilation: " + name);
            }
            CueMath(); Sequential(); Coverage(); Cameras(false); Cameras(true); Legacy();
            report.status = "SyntheticGPUValidated_TestOnly";
        }
        catch (Exception e) { report.status = "Failed"; report.failure = e.Message; Debug.LogException(e); }
        finally
        {
            QualitySettings.renderPipeline = oldQuality; GraphicsSettings.defaultRenderPipeline = oldGraphics;
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) UnityEngine.Object.DestroyImmediate(owned[i]);
            owned.Clear(); RenderTexture.active = oldActive; GL.sRGBWrite = oldSrgb;
            try { Check(before == Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t => t.name.StartsWith("Original.")), "Owned RTs released."); }
            catch (Exception e) { report.status = "Failed"; report.failure = e.Message; }
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonUtility.ToJson(report, true));
        }
        EditorApplication.Exit(report.status == "SyntheticGPUValidated_TestOnly" ? 0 : 1);
    }
    static void CueMath()
    {
        var context = new A.Context(3, 17, 1, 2);
        var first = new A.CueValues(.1f, Color.red, .2f, new Vector2(-.4f, 0), 10, null, 5, 0x10000);
        var next = new A.CueValues(.3f, Color.blue, .6f, new Vector2(.4f, 0), 30, null, 9, 1);
        var selected = A.FromSelectedKeys(context, first, next, true, .25f, default, null);
        Check(selected.Source.TrackOrdinal == 3 && selected.Source.CompositeIndex == 17, "k and j are independent.");
        Check(selected.Values.RotationSource == 15 && selected.AppliedRatio == .25f, "Selected ratio preserved.");
        Check(selected.Values.PassRaw == 0x10000 && selected.WirePass == 0 && selected.Values.Kind == 5, "Current kind and raw/signed pass retained.");
        Check(!selected.TryCreatePass0Parameters(out _, out var missing) && missing.HasFlag(A.Unresolved.Fade) && missing.HasFlag(A.Unresolved.TextureMapping), "Unknown inputs are not invented.");
        var draw = new A.DrawState(.8f, .05f, new Vector2(.2f, 0));
        var resolved = selected.WithDrawState(draw);
        var keep = OriginalCompositeProducerMath.ApplyToSnapshot(resolved, default);
        Check(keep.Draw.Fade == .8f && keep.Draw.ShaderAngles == draw.ShaderAngles && keep.AppliedRatio == selected.AppliedRatio, "Evaluated values and selection metadata preserved.");
        var angle = OriginalCompositeProducerMath.EvaluateShaderAngles(selected.Values, new OriginalCompositeProducerMath.AngleInputs(5, 10));
        Check(angle.Z == 2 && angle.W == 0, "Explicit resolved angle dependencies only.");
        Check(!OriginalCompositeProducerMath.EvaluateShaderAngles(selected.Values, default).Value.HasValue, "Missing angle dependencies remain unresolved.");
        Check(float.IsNaN(OriginalCompositeProducerMath.ClampFadeSetter(float.NaN)), "NaN clamp semantics retained.");
        Check(BitConverter.SingleToInt32Bits(OriginalCompositeProducerMath.ClampFadeSetter(-0f)) == unchecked((int)0x80000000), "Signed zero retained.");
        Check(OriginalCompositeProducerMath.EvaluateFadeAfterResolvedDuration(15, 9, 10, 10, true, 1).Fade == .5f, "Resolved-duration fade arithmetic.");
    }
    static void Sequential()
    {
        var baseImage = Image(new Color32(20, 100, 40, 173)); var shared = Target(); var output = Target();
        var right = Image(new Color32(200, 30, 10, 0)); var left = Image(new Color32(10, 30, 200, 0));
        var rp = right.GetPixels32(); var lp = left.GetPixels32();
        for (int y = 0; y < 16; y++) for (int x = 0; x < 32; x++) { rp[y * 32 + x].a = (byte)(x >= 24 ? 255 : 0); lp[y * 32 + x].a = (byte)(x < 8 ? 255 : 0); }
        right.SetPixels32(rp); right.Apply(); left.SetPixels32(lp); left.Apply();
        var steps = new List<R.Trace>(); var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
        using (var renderer = new R(Shader.Find(R.ShaderName)))
        {
            Action render = () => renderer.RenderSequential(baseImage,
                () => { Graphics.Blit(right, shared); return new R.Submission(shared, R.InputEncoding.StraightColorWithCoverage); },
                () => { Graphics.Blit(left, shared); return new R.Submission(shared, R.InputEncoding.StraightColorWithCoverage); }, output, steps.Add);
            render(); var pixels = Read(output);
            Check(pixels.Select((p, i) => (Color32)p).Select((p, i) => p.Equals(i % 32 < 8 ? new Color32(10, 30, 200, 173) : i % 32 >= 24 ? new Color32(200, 30, 10, 173) : new Color32(20, 100, 40, 173))).All(v => v), "Sequential update keeps both sides and base alpha.");
            Check(steps.Select(t => t.Step).SequenceEqual((R.Stage[])Enum.GetValues(typeof(R.Stage))), "Right/update/left/final order.");
            Check(steps[2].InputVersion == 1 && steps[4].InputVersion == 2 && steps[2].SharedInput == steps[4].SharedInput, "Shared input versioning.");
            var rt = renderer.SharedInput; render();
            Check(renderer.SharedInput == rt && Read(output).SequenceEqual(pixels), "Next-frame RT reuse preserves output.");
            Check(RenderTexture.active == active && GL.sRGBWrite == srgb, "Render state restored.");
        }
    }
    static void Coverage()
    {
        var image = Image(new Color32(80, 120, 160, 0));
        using (var generator = new OriginalMaskCoverageGenerator(Shader.Find(OriginalMaskCoverageGenerator.ShaderName)))
        {
            // At angle zero q = u.y. The CPU oracle does not reuse shader evaluation.
            var p = new OriginalMaskCoverageGenerator.Parameters(new Vector4(0, 0, 0, 0), .2f, .75f, Identity, 1);
            var result = generator.Generate(image, 32, 16, p); var data = Read(result, true);
            double max = 0;
            for (int y = 0; y < 16; y++) for (int x = 0; x < 32; x++)
            {
                double q = (y + .5) / 16, expected = Math.Min(1, Math.Max(0, (q - .4) / .2)) * .75;
                max = Math.Max(max, Math.Abs(data[y * 32 + x].a - expected)); report.coverageSamples++;
            }
            Check(max < .00001, "GPU coverage matches independent alpha ramp.");
            Check(data.All(c => Math.Abs(c.r - 80f / 255) < .00001 && Math.Abs(c.b - 160f / 255) < .00001), "Input alpha ignored; source RGB preserved.");
            var flipped = generator.Generate(image, 32, 16, new OriginalMaskCoverageGenerator.Parameters(new Vector4(0, 0, 0, 0), .2f, .75f, Identity, -1));
            var reverse = Read(flipped, true);
            Check(data.Select((c, i) => Math.Abs(c.a - reverse[(15 - i / 32) * 32 + i % 32].a) < .00001).All(v => v), "Explicit Y flip.");
            Check(flipped == result && generator.GenerationSerial == 2, "Coverage RT updates in place.");
        }
    }
    static void Cameras(bool urp)
    {
        var oldG = GraphicsSettings.defaultRenderPipeline; var oldQ = QualitySettings.renderPipeline;
        try
        {
            if (urp)
            {
                var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>());
                var asset = Own(UniversalRenderPipelineAsset.Create(data)); asset.supportsHDR = false; asset.msaaSampleCount = 1;
                GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset;
            }
            else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
            var go = Own(new GameObject("Synthetic source camera")); var source = go.AddComponent<Camera>(); source.enabled = false;
            source.clearFlags = CameraClearFlags.SolidColor; source.cullingMask = 0; source.allowHDR = false; source.allowMSAA = false;
            source.backgroundColor = Color.red;
            var originalTarget = Target(); source.SetTargetBuffers(originalTarget.colorBuffer, originalTarget.depthBuffer);
            var active = RenderTexture.active; RenderTexture.active = originalTarget; GL.Clear(true, true, Color.magenta); RenderTexture.active = active;
            var before = Read(originalTarget);
            using (var producer = new OriginalCameraProducer(32, 16))
            {
                var first = producer.RenderInput(source); var right = Read(first);
                source.backgroundColor = Color.blue; var second = producer.RenderInput(source); var left = Read(second);
                Check(right.All(c => c.r > .99 && c.b < .01) && left.All(c => c.b > .99 && c.r < .01), "Actual copied-camera images: " + urp);
                Check(first == second && producer.RenderCount == 2 && producer.LastBackend == (urp ? "URP.SingleCameraRequest" : "BuiltIn.Camera.Render"), "Shared camera RT and backend: " + urp);
                Check(!source.enabled && source.targetTexture == null && source.cullingMask == 0 && Read(originalTarget).SequenceEqual(before), "Source state and buffer contents preserved: " + urp);
                if (!urp) { source.Render(); Check(Read(originalTarget).All(c => c.b > .99 && c.r < .01), "Source SetTargetBuffers binding preserved."); }
            }
        }
        finally { QualitySettings.renderPipeline = oldQ; GraphicsSettings.defaultRenderPipeline = oldG; }
    }
    static void Legacy()
    {
        var a = Target(); var b = Target(); var c = Target(); var output = Target();
        Graphics.Blit(Image(new Color32(255, 0, 0, 255)), a); Graphics.Blit(Image(new Color32(0, 255, 0, 255)), b); Graphics.Blit(Image(new Color32(0, 0, 255, 255)), c);
        using (var legacy = new LiveDiagonalCompositeTestOnly())
        {
            var settings = LiveDiagonalCompositeTestOnly.ThreeRegionSettings.Default;
            settings.LeftAnchor = .25f; settings.RightAnchor = .75f;
            settings.LeftSlope = 0; settings.RightSlope = 0;
            legacy.CompositeThree(a, b, c, output, settings);
            var pixels = Read(output);
            Check(pixels.All(p => p.a == 1) && pixels.Any(p => p.r > .99) && pixels.Any(p => p.g > .99) && pixels.Any(p => p.b > .99), "Existing CompositeThree GPU smoke.");
        }
    }
}
