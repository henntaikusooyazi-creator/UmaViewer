using System;
using System.IO;
using Gallop.Live.Cutt;
using Gallop.Live.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

// Explicit natural-playback diagnostic. The RGB equation is artificial; it
// checks current inputs and never supplies an original-game constant mapping.
public sealed class OriginalNaturalPostProcessProbe : IDisposable
{
    [Serializable] public sealed class Result
    {
        public string mode = "ArtificialSevenStageDiagnostic";
        public long inputRenders, lastSelectedSerial, lastRenderSerial;
        public int rgbSamples, pixels, maxRgbError, parameterChanges, nonFarDepthSamples, stageRgbComparisons;
        public int stages = 7;
        public long completedStages;
        public float minimumDepth = 1, maximumDepth;
        public bool OriginalVerified = false, runtime_contract = false;
    }
    public readonly Result Evidence = new Result();
    public OriginalLivePostProcessInputs Processor { get; }
    readonly Material[] materials = new Material[7];
    OriginalPostProcessChain chain;
    RenderTexture currentDepth;
    int width, height;
    PostEffectUpdateInfo_BloomDiffusion previous;
    public OriginalNaturalPostProcessProbe(OriginalLiveContinuousRenderer renderer)
    {
        var shader = Shader.Find("Hidden/OriginalRendering/NaturalPostProcessValidation");
        if (!shader || !shader.isSupported) throw new InvalidOperationException("Artificial frame-input shader unavailable.");
        for (int i = 0; i < materials.Length; i++) materials[i] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        Processor = new OriginalLivePostProcessInputs(() => renderer.RenderingFrame, Process);
    }
    static byte[] Color(RenderTexture texture)
    {
        var r = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32); r.WaitForCompletion();
        if (r.hasError) throw new InvalidOperationException("Current color readback failed."); return r.GetData<byte>().ToArray();
    }
    RenderTexture Process(OriginalCameraFrame frame, PostEffectUpdateInfo_BloomDiffusion parameters)
    {
        if (frame.RenderSerial <= Evidence.lastRenderSerial || frame.SelectedSerial < Evidence.lastSelectedSerial ||
            float.IsNaN(parameters.intensity) || float.IsInfinity(parameters.intensity)) throw new InvalidOperationException("Current frame input ordering failed.");
        if (chain == null || width != frame.Color.width || height != frame.Color.height)
        {
            chain?.Dispose(); width = frame.Color.width; height = frame.Color.height;
            var passes = new OriginalPostProcessChain.Pass[materials.Length];
            for (int i = 0; i < passes.Length; i++) passes[i] = new OriginalPostProcessChain.Pass(materials[i], 0, width, height, null,
                new OriginalPostProcessChain.Binding("_MainTex", i == 0 ? -1 : i - 1),
                new OriginalPostProcessChain.Binding("_Depth", () => currentDepth));
            chain = new OriginalPostProcessChain(passes);
        }
        bool sample = Evidence.rgbSamples < 32 && (Evidence.inputRenders < 4 || Evidence.inputRenders % 32 == 0);
        byte[] expected = null; float[] depth = null;
        if (sample)
        {
            expected = Color(frame.Color);
            var request = AsyncGPUReadback.Request(frame.Depth, 0, TextureFormat.RFloat); request.WaitForCompletion();
            if (request.hasError) throw new InvalidOperationException("Current depth readback failed."); depth = request.GetData<float>().ToArray();
            for (int i = 0; i < depth.Length; i++)
            {
                float value = depth[i]; if (float.IsNaN(value) || value < 0 || value > 1) throw new InvalidOperationException("Depth is not a current raw device value.");
                Evidence.minimumDepth = Mathf.Min(Evidence.minimumDepth, value); Evidence.maximumDepth = Mathf.Max(Evidence.maximumDepth, value);
                if (frame.ReversedZ ? value > .000001f : value < .999999f) Evidence.nonFarDepthSamples++;
            }
        }
        RenderTexture result; currentDepth = frame.Depth;
        try
        {
            result = chain.Process(frame.Source, frame.Color,
                (stage, material) => material.SetFloat("_Parameter", parameters.intensity / materials.Length),
                (stage, target) =>
                {
                    Evidence.completedStages++;
                    if (!sample) return;
                    var actual = Color(target);
                    for (int y = height / 2 - 4; y < height / 2 + 4; y++) for (int x = width / 2 - 8; x < width / 2 + 8; x++)
                    {
                        int i = y * width + x; float[] add = { depth[i] * .2f, parameters.intensity / materials.Length * .1f, .03f };
                        for (int c = 0; c < 3; c++)
                        {
                            int j = i * 4 + c;
                            expected[j] = (byte)Mathf.Clamp(Mathf.RoundToInt(expected[j] * .5f + add[c] * 255), 0, 255);
                            Evidence.maxRgbError = Math.Max(Evidence.maxRgbError, Math.Abs(actual[j] - expected[j]));
                        }
                        Evidence.pixels++;
                    }
                    if (Evidence.maxRgbError > 1) throw new InvalidOperationException("Current depth/parameter stage RGB comparison failed.");
                    Evidence.stageRgbComparisons++;
                });
        }
        finally { currentDepth = null; }
        if (sample) Evidence.rgbSamples++;
        if (Evidence.inputRenders > 0 && !parameters.Equals(previous)) Evidence.parameterChanges++;
        previous = parameters; Evidence.inputRenders++; Evidence.lastRenderSerial = frame.RenderSerial; Evidence.lastSelectedSerial = frame.SelectedSerial;
        return result;
    }
    public bool Passed => Evidence.inputRenders > 0 && Evidence.rgbSamples > 0 && Evidence.nonFarDepthSamples > 0 && Evidence.maxRgbError <= 1 &&
        Evidence.completedStages == Evidence.inputRenders * materials.Length && Evidence.stageRgbComparisons == Evidence.rgbSamples * materials.Length;
    public void Save(string output) => File.WriteAllText(Path.Combine(output, "frame-inputs.private.json"), JsonUtility.ToJson(Evidence, true));
    public void Dispose() { chain?.Dispose(); foreach (var material in materials) if (material) UnityEngine.Object.DestroyImmediate(material); }
}
