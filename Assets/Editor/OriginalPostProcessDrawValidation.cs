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
        foreach (int width in new[] { 16, 8 })
        {
            int height = width / 2; var input = Own(new Texture2D(width, height, TextureFormat.RGBA32, false, true));
            var expected = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) expected[y * width + x] = new Color32((byte)(x * 11), (byte)(y * 23), 97, (byte)(160 + x));
            input.SetPixels32(expected); input.Apply();
            foreach (bool flip in new[] { false, true })
            {
                var draw = OriginalPostProcessDrawState.Capture(frame, input, Matrix4x4.identity, Matrix4x4.identity, projection, flip); draw.ValidateCurrent(frame, input);
                Check(draw.TexelSize == new Vector4(1f / width, (flip ? -1f : 1f) / height, width, height), "Current descriptor and explicit sign.");
                Check(draw.GpuViewProjection == GL.GetGPUProjectionMatrix(projection, true), "Fullscreen projection is separate from camera projection.");
                using (var chain = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(material, 0, width, height, mesh, new OriginalPostProcessChain.Binding("_MainTex", -2, input))))
                {
                    var target = chain.Process(camera, color, (stage, m) => { draw.ValidateCurrent(frame, input); m.SetMatrix("_DrawModel", draw.ObjectToWorld); m.SetMatrix("_DrawVP", draw.GpuViewProjection); m.SetVector("_DrawTexelSize", draw.TexelSize); }, null);
                    var read = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); read.WaitForCompletion(); Check(!read.hasError, "Draw readback."); var bytes = read.GetData<byte>();
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        var p = expected[(flip ? height - 1 - y : y) * width + x]; int at = (y * width + x) * 4; report.pixels++;
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
        var snapshot = OriginalPostProcessDrawState.Capture(frame, color, Matrix4x4.identity, Matrix4x4.identity, projection, false);
        color.Release(); Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(frame, color), "Released RT rejected."); color.Create();
        var bad = Matrix4x4.identity; bad[0] = float.NaN;
        Reject<ArgumentException>(() => OriginalPostProcessDrawState.Capture(frame, color, bad, Matrix4x4.identity, projection, false), "Non-finite transform rejected.");
        Reject<InvalidOperationException>(() => OriginalPostProcessDrawState.Capture(default, color, Matrix4x4.identity, Matrix4x4.identity, projection, false), "Missing frame rejected.");
        Reject<InvalidOperationException>(() => OriginalPostProcessDrawState.Capture(frame, null, Matrix4x4.identity, Matrix4x4.identity, projection, false), "Missing draw input rejected.");
        var resized = Target(6, 4); snapshot = OriginalPostProcessDrawState.Capture(frame, resized, Matrix4x4.identity, Matrix4x4.identity, projection, false);
        resized.Release(); resized.width = 8; resized.Create(); Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(frame, resized), "Resized input rejected.");
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
