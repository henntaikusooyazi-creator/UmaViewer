using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Artificial transforms/textures only; no original constant buffers.
public static class OriginalPostProcessDrawValidation
{
    [Serializable] sealed class Report { public string status = "Running", failure; public int checks, pixels, rgbaExact; public bool OriginalVerified = false, runtime_contract = false; }
    static Report report;
    static readonly List<Object> owned = new List<Object>();
    static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Check(bool ok, string message) { report.checks++; if (!ok) throw new InvalidOperationException(message); }
    static void Reject<T>(Action action, string message) where T : Exception
    { bool rejected = false; try { action(); } catch (T) { rejected = true; } Check(rejected, message); }
    static OriginalCameraFrame Frame(Camera camera, RenderTexture color, long render = 1, long selected = 1)
        => (OriginalCameraFrame)Activator.CreateInstance(typeof(OriginalCameraFrame), BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { camera, camera, color, null, render, selected }, null);
    static RenderTexture Target(int width, int height)
    { var rt = Own(new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)); Check(rt.Create(), "Target creation."); return rt; }
    static Mesh Quad()
    {
        var mesh = Own(new Mesh()); mesh.vertices = new[] { new Vector3(0, 0, .25f), new Vector3(1, 0, .25f), new Vector3(0, 1, .25f), new Vector3(1, 1, .25f) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one }; mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 }; return mesh;
    }
    static void Pipeline(bool urp)
    {
        if (urp) { var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>()); var asset = Own(UniversalRenderPipelineAsset.Create(data)); GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset; }
        else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
        var camera = Own(new GameObject("Artificial.Draw.Camera")).AddComponent<Camera>(); camera.enabled = false;
        var other = Own(new GameObject("Artificial.Draw.Other")).AddComponent<Camera>(); other.enabled = false;
        var color = Target(16, 8); var frame = Frame(camera, color);
        var projection = Matrix4x4.Ortho(0, 1, 0, 1, -2, 20);
        var material = Own(new Material(Shader.Find("Hidden/OriginalRendering/PostProcessDrawValidation"))); var mesh = Quad();
        material.SetVector("_DrawMaterialVector", Vector4.one);
        foreach (int width in new[] { 16, 8 })
        {
            int height = width / 2; var input = Own(new Texture2D(width, height, TextureFormat.RGBA32, false, true));
            var expected = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) expected[y * width + x] = new Color32((byte)(x * 11), (byte)(y * 23), 97, (byte)(160 + x));
            input.SetPixels32(expected); input.Apply();
            foreach (bool flip in new[] { false, true })
            foreach (int uvCase in new[] { 0, 1, 2 })
            {
                var scale = uvCase == 1 ? new Vector2(-1, 1) : uvCase == 2 ? new Vector2(.5f, .5f) : Vector2.one;
                var offset = uvCase == 1 ? new Vector2(1, 0) : uvCase == 2 ? new Vector2(.5f, 0) : Vector2.zero;
                var draw = uvCase == 0
                    ? OriginalPostProcessDrawState.Capture(frame, input, Matrix4x4.identity, Matrix4x4.identity, projection, flip)
                    : OriginalPostProcessDrawState.Capture(frame, input, Matrix4x4.identity, Matrix4x4.identity, projection, flip, scale, offset);
                draw.ValidateCurrent(frame, input);
                Check(!draw.MaterialVector.HasValue, "Legacy draw does not infer a material vector.");
                Check(draw.UvScaleOffset == new Vector4(scale.x, scale.y, offset.x, offset.y), "Current command UV arguments and legacy identity.");
                Check(draw.TexelSize == new Vector4(1f / width, (flip ? -1f : 1f) / height, width, height), "Current descriptor and explicit sign.");
                Check(draw.GpuViewProjection == GL.GetGPUProjectionMatrix(projection, true), "Fullscreen projection is separate from camera projection.");
                using (var chain = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(material, 0, width, height, mesh, new OriginalPostProcessChain.Binding("_MainTex", -2, input))))
                {
                    var target = chain.Process(camera, color, (stage, m) => { draw.ValidateCurrent(frame, input); m.SetMatrix("_DrawModel", draw.ObjectToWorld); m.SetMatrix("_DrawVP", draw.GpuViewProjection); m.SetVector("_DrawTexelSize", draw.TexelSize); m.SetVector("_DrawUvScaleOffset", draw.UvScaleOffset); }, null);
                    var read = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); read.WaitForCompletion(); Check(!read.hasError, "Draw readback."); var bytes = read.GetData<byte>();
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float u = ((x + .5f) / width) * scale.x + offset.x;
                        float v = ((y + .5f) / height) * scale.y + offset.y;
                        if (flip) v = 1 - v;
                        int sx = Mathf.Clamp(Mathf.FloorToInt(u * width), 0, width - 1);
                        int sy = Mathf.Clamp(Mathf.FloorToInt(v * height), 0, height - 1);
                        var p = expected[sy * width + sx]; int at = (y * width + x) * 4; report.pixels++;
                        if (bytes[at] != p.r || bytes[at + 1] != p.g || bytes[at + 2] != p.b || bytes[at + 3] != p.a) throw new Exception("Fullscreen UV/transform RGBA mismatch."); report.rgbaExact++;
                    }
                    Check(chain.ProcessCount == 1, "Current draw completed.");
                }
                Reject<InvalidOperationException>(() => draw.ValidateCurrent(Frame(camera, color, 2), input), "Changed render rejected.");
                Reject<InvalidOperationException>(() => draw.ValidateCurrent(Frame(camera, color, 1, 2), input), "Changed selection rejected.");
                Reject<InvalidOperationException>(() => draw.ValidateCurrent(Frame(other, color), input), "Foreign camera rejected.");
                Reject<InvalidOperationException>(() => draw.ValidateCurrent(frame, color), "Foreign texture rejected.");
            }
        }
        MaterialCases(frame, color, projection, mesh);
        PropertyCases(frame, color, projection, mesh);
        var snapshot = OriginalPostProcessDrawState.Capture(frame, color, Matrix4x4.identity, Matrix4x4.identity, projection, false);
        color.Release(); Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(frame, color), "Released RT rejected."); color.Create();
        var bad = Matrix4x4.identity; bad[0] = float.NaN;
        Reject<ArgumentException>(() => OriginalPostProcessDrawState.Capture(frame, color, bad, Matrix4x4.identity, projection, false), "Non-finite transform rejected.");
        Reject<ArgumentException>(() => OriginalPostProcessDrawState.Capture(frame, color, Matrix4x4.identity, Matrix4x4.identity, projection, false, new Vector2(float.NaN, 1), Vector2.zero), "Non-finite UV scale rejected.");
        Reject<ArgumentException>(() => OriginalPostProcessDrawState.Capture(frame, color, Matrix4x4.identity, Matrix4x4.identity, projection, false, Vector2.one, new Vector2(0, float.PositiveInfinity)), "Non-finite UV offset rejected.");
        Reject<InvalidOperationException>(() => OriginalPostProcessDrawState.Capture(default, color, Matrix4x4.identity, Matrix4x4.identity, projection, false), "Missing frame rejected.");
        Reject<InvalidOperationException>(() => OriginalPostProcessDrawState.Capture(frame, null, Matrix4x4.identity, Matrix4x4.identity, projection, false), "Missing draw input rejected.");
        var resized = Target(6, 4); snapshot = OriginalPostProcessDrawState.Capture(frame, resized, Matrix4x4.identity, Matrix4x4.identity, projection, false);
        resized.Release(); resized.width = 8; resized.Create(); Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(frame, resized), "Resized input rejected.");
    }
    static void MaterialCases(OriginalCameraFrame frame, RenderTexture color, Matrix4x4 projection, Mesh mesh)
    {
        var source = Own(new Material(Shader.Find("Hidden/OriginalRendering/PostProcessDrawValidation")));
        var outputMaterial = Own(new Material(source));
        var input = Own(new Texture2D(8, 4, TextureFormat.RGBA32, false, true));
        var pixels = new Color32[32]; for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(80, 120, 200, 128);
        input.SetPixels32(pixels); input.Apply();
        Func<OriginalPostProcessDrawState> capture = () => OriginalPostProcessDrawState.Capture(frame, input,
            Matrix4x4.identity, Matrix4x4.identity, projection, false, Vector2.one, Vector2.zero, source, "_SourceVector");
        var rawColor = source.GetVector("_SourceColor");
        var colorState = OriginalPostProcessDrawState.Capture(frame, input, Matrix4x4.identity, Matrix4x4.identity,
            projection, false, Vector2.one, Vector2.zero, source, "_SourceColor");
        Check(colorState.MaterialVector.Value.Equals(rawColor), "Color property preserves its raw material vector.");
        var vectors = new[] { new Vector4(.25f, .5f, .75f, .5f), new Vector4(.5f, .25f, 1, .75f), Vector4.zero };
        var expected = new[] { new Color32(20, 60, 150, 64), new Color32(40, 30, 200, 96), new Color32(0, 0, 0, 0) };
        for (int sample = 0; sample < vectors.Length; sample++)
        {
            if (sample > 0) source.SetVector("_SourceVector", vectors[sample]);
            var draw = capture(); draw.ValidateCurrent(frame, input);
            Check(draw.MaterialVector.HasValue && draw.MaterialVector.Value.Equals(vectors[sample]), "Shader default or current explicit material value.");
            using (var chain = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(outputMaterial, 0, 8, 4, mesh,
                new OriginalPostProcessChain.Binding("_MainTex", -2, input))))
            {
                var target = chain.Process(frame.Source, color, (stage, m) => {
                    draw.ValidateCurrent(frame, input); m.SetMatrix("_DrawModel", draw.ObjectToWorld); m.SetMatrix("_DrawVP", draw.GpuViewProjection);
                    m.SetVector("_DrawTexelSize", draw.TexelSize); m.SetVector("_DrawUvScaleOffset", draw.UvScaleOffset);
                    m.SetVector("_DrawMaterialVector", draw.MaterialVector.Value);
                }, null);
                var read = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); read.WaitForCompletion(); Check(!read.hasError, "Material vector GPU readback.");
                var bytes = read.GetData<byte>(); var p = expected[sample];
                for (int i = 0; i < 32; i++) { report.pixels++; if (bytes[i * 4] != p.r || bytes[i * 4 + 1] != p.g || bytes[i * 4 + 2] != p.b || bytes[i * 4 + 3] != p.a) throw new Exception("Material vector GPU RGBA mismatch."); report.rgbaExact++; }
            }
        }
        source.SetVector("_SourceVector", Vector4.one); var snapshot = capture();
        source.SetVector("_SourceVector", new Vector4(1 + 1f / 1048576, 1, 1, 1));
        Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(frame, input), "Small material mutation rejects stale snapshot.");
        Reject<InvalidOperationException>(() => OriginalPostProcessDrawState.Capture(frame, input, Matrix4x4.identity, Matrix4x4.identity, projection, false, Vector2.one, Vector2.zero, null, "_SourceVector"), "Missing material rejected.");
        foreach (string property in new[] { null, "", "_Missing", "_MainTex", "_Scalar" })
            Reject<InvalidOperationException>(() => OriginalPostProcessDrawState.Capture(frame, input, Matrix4x4.identity, Matrix4x4.identity, projection, false, Vector2.one, Vector2.zero, source, property), "Missing or non-vector property rejected.");
        for (int lane = 0; lane < 4; lane++) foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity }) {
            var value = Vector4.one; value[lane] = invalid; source.SetVector("_SourceVector", value);
            Reject<ArgumentException>(() => capture(), "Non-finite material lane rejected.");
        }
        source.SetVector("_SourceVector", Vector4.one); snapshot = capture();
        source.shader = Shader.Find("Hidden/InternalErrorShader");
        Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(frame, input), "Changed shader rejected.");
        Object.DestroyImmediate(source);
        Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(frame, input), "Destroyed material rejected.");
    }
    static void PropertyCases(OriginalCameraFrame frame, RenderTexture color, Matrix4x4 projection, Mesh mesh)
    {
        const string global = "_ArtificialOriginalPostProcessGlobalVector";
        var previous = Shader.GetGlobalVector(global);
        var source = Own(new Material(Shader.Find("Hidden/OriginalRendering/PostProcessDrawValidation")));
        var targetMaterial = Own(new Material(source));
        var input = Own(new Texture2D(8, 4, TextureFormat.RGBA32, false, true));
        var pixels = new Color32[32];
        for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++) pixels[y * 8 + x] = new Color32((byte)(x * 20), (byte)(y * 40), 80, 128);
        input.SetPixels32(pixels); input.Apply();
        try
        {
            Shader.SetGlobalVector(global, Vector4.zero);
            var colorState = OriginalPostProcessPropertyState.Capture(frame, input, source, "_SourceColor", global);
            Check(colorState.MaterialVector.Equals(source.GetVector("_SourceColor")), "Paired color source retains raw shader default.");
            for (int sample = 0; sample < 2; sample++)
            {
                var currentFrame = Frame(frame.Source, color, sample + 1, sample + 1);
                source.SetVector("_SourceVector", new Vector4(sample == 0 ? 1 : .5f, 1, 1, 1));
                Shader.SetGlobalVector(global, new Vector4(sample * .25f, 0, 0, 0));
                var state = OriginalPostProcessPropertyState.Capture(currentFrame, input, source, "_SourceVector", global);
                state.ValidateCurrent(currentFrame, input);
                Check(state.MaterialVector.Equals(source.GetVector("_SourceVector")) && state.GlobalVector.Equals(Shader.GetGlobalVector(global)), "Current independent material/global values.");
                var draw = OriginalPostProcessDrawState.Capture(currentFrame, input, Matrix4x4.identity, Matrix4x4.identity, projection, false,
                    Vector2.one, new Vector2(state.GlobalVector.x, state.GlobalVector.y));
                using (var chain = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(targetMaterial, 0, 8, 4, mesh,
                    new OriginalPostProcessChain.Binding("_MainTex", -2, input))))
                {
                    var target = chain.Process(frame.Source, color, (stage, m) => {
                        state.ValidateCurrent(currentFrame, input); draw.ValidateCurrent(currentFrame, input);
                        m.SetMatrix("_DrawModel", draw.ObjectToWorld); m.SetMatrix("_DrawVP", draw.GpuViewProjection);
                        m.SetVector("_DrawTexelSize", draw.TexelSize); m.SetVector("_DrawUvScaleOffset", draw.UvScaleOffset);
                        m.SetVector("_DrawMaterialVector", state.MaterialVector);
                    }, null);
                    var read = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); read.WaitForCompletion(); Check(!read.hasError, "Current material/global GPU readback.");
                    var data = read.GetData<byte>();
                    for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++)
                    {
                        var expected = pixels[y * 8 + Mathf.Min(7, x + sample * 2)]; int at = (y * 8 + x) * 4;
                        report.pixels++;
                        if (data[at] != (sample == 0 ? expected.r : expected.r / 2) || data[at + 1] != expected.g || data[at + 2] != expected.b || data[at + 3] != expected.a)
                            throw new Exception("Current material/global GPU RGBA mismatch.");
                        report.rgbaExact++;
                    }
                }
                Reject<InvalidOperationException>(() => state.ValidateCurrent(Frame(frame.Source, color, sample + 2, sample + 1), input), "Property snapshot rejects changed render.");
                Reject<InvalidOperationException>(() => state.ValidateCurrent(Frame(frame.Source, color, sample + 1, sample + 2), input), "Property snapshot rejects changed selection.");
                Reject<InvalidOperationException>(() => state.ValidateCurrent(currentFrame, color), "Property snapshot rejects foreign input.");
                Shader.SetGlobalVector(global, state.GlobalVector + new Vector4(1f / 1048576, 0, 0, 0));
                Reject<InvalidOperationException>(() => state.ValidateCurrent(currentFrame, input), "Small global mutation invalidates snapshot.");
                Shader.SetGlobalVector(global, state.GlobalVector);
                source.SetVector("_SourceVector", state.MaterialVector + new Vector4(1f / 1048576, 0, 0, 0));
                Reject<InvalidOperationException>(() => state.ValidateCurrent(currentFrame, input), "Small material mutation invalidates paired snapshot.");
            }
            source.SetVector("_SourceVector", Vector4.one);
            foreach (string property in new[] { null, "", "_Missing", "_MainTex", "_Scalar" })
                Reject<InvalidOperationException>(() => OriginalPostProcessPropertyState.Capture(frame, input, source, property, global), "Missing or non-vector paired source rejected.");
            foreach (string property in new[] { null, "", "_SourceVector", "_SourceColor" })
                Reject<InvalidOperationException>(() => OriginalPostProcessPropertyState.Capture(frame, input, source, "_SourceVector", property), "Missing global or declared material override rejected.");
            for (int lane = 0; lane < 4; lane++) foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var value = Vector4.one; value[lane] = invalid; Shader.SetGlobalVector(global, value);
                Reject<ArgumentException>(() => OriginalPostProcessPropertyState.Capture(frame, input, source, "_SourceVector", global), "Non-finite global lane rejected.");
                Shader.SetGlobalVector(global, Vector4.zero); source.SetVector("_SourceVector", value);
                Reject<ArgumentException>(() => OriginalPostProcessPropertyState.Capture(frame, input, source, "_SourceVector", global), "Non-finite paired material lane rejected.");
                source.SetVector("_SourceVector", Vector4.one);
            }
            var snapshot = OriginalPostProcessPropertyState.Capture(frame, input, source, "_SourceVector", global);
            source.shader = Shader.Find("Hidden/InternalErrorShader");
            Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(frame, input), "Paired source shader replacement rejected.");
            Object.DestroyImmediate(source);
            Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(frame, input), "Destroyed paired source rejected.");
            Reject<InvalidOperationException>(() => OriginalPostProcessPropertyState.Capture(default, input, targetMaterial, "_SourceVector", global), "Missing paired frame rejected.");
        }
        finally { Shader.SetGlobalVector(global, previous); }
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-integrationOutput"); if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("External output required.");
        string output = args[index + 1]; Directory.CreateDirectory(output); report = new Report();
        var g = GraphicsSettings.defaultRenderPipeline; var q = QualitySettings.renderPipeline; var active = RenderTexture.active; bool srgb = GL.sRGBWrite; int exit = 0;
        try { Pipeline(false); Pipeline(true); report.status = "PostProcessDrawValidated_TestOnly"; }
        catch (Exception e) { report.status = "Failed"; report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally { QualitySettings.renderPipeline = q; GraphicsSettings.defaultRenderPipeline = g; RenderTexture.active = active; GL.sRGBWrite = srgb; foreach (var item in owned) if (item) { if (item is RenderTexture rt) rt.Release(); Object.DestroyImmediate(item); } owned.Clear(); File.WriteAllText(Path.Combine(output, "integration-summary.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit); }
    }
}
