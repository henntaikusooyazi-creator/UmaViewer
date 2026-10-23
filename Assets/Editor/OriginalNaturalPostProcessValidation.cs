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

// Artificial geometry and typed keys; no saved original-game values.
public static class OriginalNaturalPostProcessValidation
{
    [Serializable] class Report { public string status = "Running", failure; public int checks, pixels; public bool OriginalVerified = false, runtime_contract = false; }
    static readonly List<Object> owned = new List<Object>();
    static Report report;
    static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Check(bool value, string message) { report.checks++; if (!value) throw new InvalidOperationException(message); }
    static byte[] Read(RenderTexture texture)
    {
        var request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32); request.WaitForCompletion();
        if (request.hasError) throw new Exception("RGBA readback"); return request.GetData<byte>().ToArray();
    }
    static float[] Depth(RenderTexture texture)
    {
        var request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RFloat); request.WaitForCompletion();
        if (request.hasError) throw new Exception("Depth readback"); return request.GetData<float>().ToArray();
    }
    sealed class Probe : IOriginalCameraFramePostProcessor
    {
        public OriginalCameraFrame Last;
        public Material Material;
        public float Parameter;
        public RenderTexture Process(OriginalCameraFrame frame)
        {
            Last = frame; Material.SetFloat("_Parameter", Parameter);
            using (var chain = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(Material, 0, frame.Color.width, frame.Color.height, null,
                new OriginalPostProcessChain.Binding("_MainTex", -1), new OriginalPostProcessChain.Binding("_Depth", -2, frame.Depth))))
            {
                var pixels = Read(frame.Color); var depth = Depth(frame.Depth); var output = chain.Process(frame.Source, frame.Color); var actual = Read(output);
                int max = 0, nearPixels = 0, farPixels = 0; bool finite = true;
                for (int y = 12; y < 20; y++) for (int x = 24; x < 40; x++)
                {
                    int i = y * 64 + x; bool near = pixels[i * 4] > pixels[i * 4 + 2]; if (near) nearPixels++; else farPixels++;
                    var clip = frame.Projection * (frame.View * new Vector4(0, 0, near ? WorldDepth : FarWorldDepth, 1));
                    finite &= !float.IsNaN(depth[i]) && Math.Abs(depth[i] - clip.z / clip.w) < .00002f;
                    float[] add = { depth[i] * .2f, Parameter * .1f, .03f };
                    for (int c = 0; c < 3; c++) max = Math.Max(max, Math.Abs(actual[i * 4 + c] - Mathf.Clamp(Mathf.RoundToInt(pixels[i * 4 + c] * .5f + add[c] * 255), 0, 255)));
                    report.pixels++;
                }
                Check(finite && nearPixels > 0 && farPixels > 0, "Asymmetric color/depth agree: near=" + nearPixels + " far=" + farPixels + " rows=" + depth[12 * 64 + 32] + "/" + depth[19 * 64 + 32] + " red=" + pixels[(12 * 64 + 32) * 4] + "/" + pixels[(19 * 64 + 32) * 4] + " blue=" + pixels[(12 * 64 + 32) * 4 + 2] + "/" + pixels[(19 * 64 + 32) * 4 + 2] + " backend=" + GraphicsSettings.currentRenderPipeline);
                Check(max <= 1, "Natural frame inputs reach the explicit RGB chain.");
            }
            return frame.Color;
        }
        public float WorldDepth;
        public float FarWorldDepth;
    }
    static void SevenStages(OriginalCameraProducer producer, Camera source, OriginalLiveTimelineBridge bridge)
    {
        // This conversion is deliberately artificial. Original CB generation is
        // not inferred from similarly named fields or these fixture values.
        var passes = new OriginalPostProcessChain.Pass[7];
        OriginalCameraFrame current = default;
        RenderTexture rawInput = null;
        int resolves = 0, prepared = 0, completed = 0;
        for (int i = 0; i < passes.Length; i++)
        {
            var material = Own(new Material(Shader.Find("Hidden/OriginalRendering/PostProcessValidation")));
            passes[i] = new OriginalPostProcessChain.Pass(material, 0, 64, 32, null,
                new OriginalPostProcessChain.Binding("_MainTex", i == 0 ? -1 : i - 1),
                new OriginalPostProcessChain.Binding("_OtherTex", () => { resolves++; return current.Depth; }));
        }
        using (var chain = new OriginalPostProcessChain(passes))
        {
            var adapter = new OriginalLivePostProcessInputs(() => bridge.LastFrame, (frame, payload) =>
            {
                current = frame;
                rawInput = frame.Color;
                try
                {
                    var before = Read(frame.Color); var depth = Depth(frame.Depth);
                    var expected = new int[before.Length]; for (int i = 0; i < before.Length; i++) expected[i] = before[i];
                    return chain.Process(frame.Source, frame.Color, (stage, material) =>
                    {
                        Check(stage == prepared % 7, "Pass preparation follows stage order."); prepared++;
                        material.SetVector("_Gain", new Vector4(.5f, .5f, .5f, 1));
                        material.SetVector("_OtherGain", new Vector4(.125f, 0, 0, 0));
                        material.SetVector("_Offset", new Vector4(0, payload.intensity * (stage + 1) * .01f, 0, 0));
                    }, (stage, output) =>
                    {
                        var actual = Read(output); int max = 0;
                        for (int y = 12; y < 20; y++) for (int x = 24; x < 40; x++)
                        {
                            int p = y * 64 + x;
                            float[] add = { depth[p] * .125f, payload.intensity * (stage + 1) * .01f, 0 };
                            for (int c = 0; c < 3; c++)
                            {
                                int j = p * 4 + c;
                                expected[j] = Mathf.Clamp(Mathf.RoundToInt(expected[j] * .5f + add[c] * 255), 0, 255);
                                max = Math.Max(max, Math.Abs(actual[j] - expected[j]));
                            }
                            max = Math.Max(max, Math.Abs(actual[p * 4 + 3] - before[p * 4 + 3])); report.pixels++;
                        }
                        Check(max <= 1 && stage == completed % 7, "Current depth and payload agree with each quantized RGB stage."); completed++;
                    });
                }
                finally { current = default; }
            });
            producer.SetInputFramePostProcessor(adapter, Shader.Find("Hidden/OriginalRendering/DepthCopy"));
            for (int n = 0; n < 2; n++)
            {
                bridge.CaptureOwnerTimes(3 + n, 2 + n); bridge.BeginFrame(180 + n * 60);
                bridge.CaptureSelected(0, 180 + n * 60, new LiveTimelineKeyMultiCameraPositionData(), null, null, source);
                bridge.CapturePostEffect(new PostEffectUpdateInfo_BloomDiffusion { intensity = .25f + n }); bridge.CompleteFrame();
                producer.RenderInput(source, bridge.LastFrame.Serial);
            }
            Check(resolves == 14 && prepared == 14 && completed == 14 && chain.ProcessCount == 2, "All seven stages resolve and prepare anew for both updates.");
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite; int count = chain.ProcessCount;
            bool rejected = false; try { chain.Process(source, rawInput); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && chain.ProcessCount == count && RenderTexture.active == active && GL.sRGBWrite == srgb, "Missing current depth has no stale texture fallback and restores state.");
            producer.SetInputFramePostProcessor(null, null);
        }
    }
    static void Pipeline(bool urp)
    {
        if (urp) { var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>()); var asset = Own(UniversalRenderPipelineAsset.Create(data)); asset.msaaSampleCount = 1; asset.supportsHDR = false; GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset; }
        else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
        var source = Own(new GameObject("Synthetic.Depth.Camera")).AddComponent<Camera>(); source.enabled = false; source.cullingMask = 1 << 30;
        source.clearFlags = CameraClearFlags.SolidColor; source.backgroundColor = Color.black; source.nearClipPlane = .3f; source.farClipPlane = 70;
        var plane = Own(GameObject.CreatePrimitive(PrimitiveType.Quad)); plane.layer = 30; plane.transform.localScale = new Vector3(30, 30, 1);
        var material = Own(new Material(Shader.Find("Hidden/OriginalRendering/DepthFixture"))); plane.GetComponent<Renderer>().sharedMaterial = material;
        material.color = Color.red;
        var far = Own(GameObject.CreatePrimitive(PrimitiveType.Quad)); far.layer = 30; far.transform.localScale = new Vector3(40, 40, 1);
        var farMaterial = Own(new Material(material)); farMaterial.color = Color.blue; far.GetComponent<Renderer>().sharedMaterial = farMaterial;
        var probe = new Probe { Material = Own(new Material(Shader.Find("Hidden/OriginalRendering/NaturalPostProcessValidation"))) };
        using (var producer = new OriginalCameraProducer(64, 32))
        {
            var sourceMode = source.depthTextureMode; var sourceTarget = source.targetTexture;
            var clone = (Camera)typeof(OriginalCameraProducer).GetField("renderCamera", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(producer);
            var additional = urp ? clone.GetUniversalAdditionalCameraData() : null;
            var originalOption = additional ? additional.requiresDepthOption : CameraOverrideOption.UsePipelineSettings;
            producer.SetInputFramePostProcessor(probe, Shader.Find("Hidden/OriginalRendering/DepthCopy"));
            long previous = 0;
            for (int n = 0; n < 3; n++)
            {
                plane.transform.position = new Vector3(0, 15, 4 + n * 3); far.transform.position = new Vector3(0, 0, 8 + n * 3);
                source.nearClipPlane = .3f + n * .2f; source.farClipPlane = 70 + n * 10;
                probe.WorldDepth = plane.transform.position.z; probe.FarWorldDepth = far.transform.position.z; probe.Parameter = n + .25f;
                var result = producer.RenderInput(source, n + 101); var frame = probe.Last;
                Check(result == producer.SharedInput && frame.Color == result && frame.Source == source, "Frame belongs to the actual input camera.");
                Check(frame.SelectedSerial == n + 101 && frame.RenderSerial > previous && frame.Near == source.nearClipPlane && frame.Far == source.farClipPlane, "Serial and clip values are current."); previous = frame.RenderSerial;
                Check(source.depthTextureMode == sourceMode && source.targetTexture == sourceTarget && !source.enabled, "Source camera state is preserved.");
                if (additional) Check(additional.requiresDepthOption == originalOption, "URP depth inheritance is restored as an option, not a resolved boolean.");
            }
            long serial = probe.Last.RenderSerial; producer.RenderBase(source); Check(probe.Last.RenderSerial == serial, "Base path bypasses frame postprocessing.");
            producer.SetInputFramePostProcessor(null, null); producer.RenderInput(source, 500); Check(probe.Last.RenderSerial == serial, "Detached frame processor is not invoked.");
            var bridge = new OriginalLiveTimelineBridge(); bridge.UseConfirmedOwnerClock(64); bridge.CaptureOwnerTimes(1, .5f); bridge.BeginFrame(60);
            bridge.CaptureSelected(0, 60, new LiveTimelineKeyMultiCameraPositionData(), null, null, source);
            bridge.CapturePostEffect(new PostEffectUpdateInfo_BloomDiffusion { intensity = .75f }); bridge.CompleteFrame();
            var adapter = new OriginalLivePostProcessInputs(() => bridge.LastFrame, (f, p) => f.Color);
            producer.SetInputFramePostProcessor(adapter, Shader.Find("Hidden/OriginalRendering/DepthCopy")); producer.RenderInput(source, bridge.LastFrame.Serial);
            Check(adapter.ProcessCount == 1 && adapter.LastParameters.intensity == .75f, "Current serial, camera, owner and typed payload are delivered together.");
            bool rejected = false; try { producer.RenderInput(source, bridge.LastFrame.Serial + 1); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && adapter.ProcessCount == 1, "Different selected update cannot consume the payload.");
            bridge.CaptureOwnerTimes(2, 1); bridge.BeginFrame(120); bridge.CaptureSelected(0, 120, new LiveTimelineKeyMultiCameraPositionData(), null, null, source); bridge.CompleteFrame();
            rejected = false; try { producer.RenderInput(source, bridge.LastFrame.Serial); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && adapter.ProcessCount == 1 && adapter.LastParameters.intensity == .75f, "Missing payload is rejected without reusing the preceding value.");
            SevenStages(producer, source, bridge);
        }
        plane.SetActive(false); far.SetActive(false); source.gameObject.SetActive(false);
    }
    static void Timeline()
    {
        var go = Own(new GameObject("Synthetic.PostEffect.Control")); go.SetActive(false); var control = go.AddComponent<LiveTimelineControl>();
        var sheet = Own(ScriptableObject.CreateInstance<LiveTimelineWorkSheet>());
        sheet.postEffectBloomDiffusionKeys = new LiveTimelineKeyPostEffectBloomDiffusionDataList();
        var key = new LiveTimelineKeyPostEffectBloomDiffusionData { frame = 0, intensity = 2.5f, threshold = .2f, diffusionBright = .8f, IsEnabledBloom = true };
        sheet.postEffectBloomDiffusionKeys.Add(key);
        var bridge = new OriginalLiveTimelineBridge(); control.OriginalCompositeBridge = bridge;
        PostEffectUpdateInfo_BloomDiffusion observed = default; Action<PostEffectUpdateInfo_BloomDiffusion> handler = p => observed = p;
        control.OnUpdatePostEffect_BloomDiffusion += handler;
        var method = typeof(LiveTimelineControl).GetMethod("AlterUpdate_PostEffect_BloomDiffusion", BindingFlags.Instance | BindingFlags.NonPublic);
        bridge.BeginFrame(0); method.Invoke(control, new object[] { sheet, 0 }); bridge.CompleteFrame(); var saved = bridge.LastFrame;
        Check(saved.PostEffect.HasValue && saved.PostEffect.Value.intensity == observed.intensity && observed.intensity == key.intensity, "Actual Control payload is captured in the selected update.");
        control.OnUpdatePostEffect_BloomDiffusion -= handler; bridge.BeginFrame(1); method.Invoke(control, new object[] { sheet, 1 }); bridge.CompleteFrame();
        Check(!bridge.LastFrame.PostEffect.HasValue && saved.PostEffect.Value.intensity == 2.5f, "Missing payload stays unknown and saved values remain immutable.");
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); string output = null; for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-integrationOutput") output = args[i + 1];
        if (output == null) throw new ArgumentException("External output required"); Directory.CreateDirectory(output); report = new Report();
        var oldG = GraphicsSettings.defaultRenderPipeline; var oldQ = QualitySettings.renderPipeline; var oldActive = RenderTexture.active; bool oldSrgb = GL.sRGBWrite; int exit = 0;
        try { Timeline(); Pipeline(false); Pipeline(true); report.status = "NaturalPostProcessInputsValidated_TestOnly"; }
        catch (Exception e) { report.status = "Failed"; report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally { QualitySettings.renderPipeline = oldQ; GraphicsSettings.defaultRenderPipeline = oldG; RenderTexture.active = oldActive; GL.sRGBWrite = oldSrgb; foreach (var x in owned) if (x) Object.DestroyImmediate(x); owned.Clear(); File.WriteAllText(Path.Combine(output, "integration-summary.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit); }
    }
}
