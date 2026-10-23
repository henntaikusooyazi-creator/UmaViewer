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
        public long inputRenders, lastSelectedSerial, lastRenderSerial;
        public int rgbSamples, pixels, maxRgbError, parameterChanges, nonFarDepthSamples;
        public float minimumDepth = 1, maximumDepth;
        public bool OriginalVerified = false, runtime_contract = false;
    }
    public readonly Result Evidence = new Result();
    public OriginalLivePostProcessInputs Processor { get; }
    readonly Material material;
    OriginalPostProcessChain chain;
    RenderTexture boundDepth;
    PostEffectUpdateInfo_BloomDiffusion previous;
    public OriginalNaturalPostProcessProbe(OriginalLiveContinuousRenderer renderer)
    {
        var shader = Shader.Find("Hidden/OriginalRendering/NaturalPostProcessValidation");
        if (!shader || !shader.isSupported) throw new InvalidOperationException("Artificial frame-input shader unavailable.");
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
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
        if (chain == null || boundDepth != frame.Depth)
        {
            chain?.Dispose(); boundDepth = frame.Depth;
            chain = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(material, 0, frame.Color.width, frame.Color.height, null,
                new OriginalPostProcessChain.Binding("_MainTex", -1), new OriginalPostProcessChain.Binding("_Depth", -2, frame.Depth)));
        }
        material.SetFloat("_Parameter", parameters.intensity);
        var result = chain.Process(frame.Source, frame.Color);
        if (Evidence.rgbSamples < 32 && (Evidence.inputRenders < 4 || Evidence.inputRenders % 32 == 0))
        {
            var before = Color(frame.Color); var after = Color(result);
            var request = AsyncGPUReadback.Request(frame.Depth, 0, TextureFormat.RFloat); request.WaitForCompletion();
            if (request.hasError) throw new InvalidOperationException("Current depth readback failed."); var raw = request.GetData<float>();
            for (int i = 0; i < raw.Length; i++)
            {
                float value = raw[i]; if (float.IsNaN(value) || value < 0 || value > 1) throw new InvalidOperationException("Depth is not a current raw device value.");
                Evidence.minimumDepth = Mathf.Min(Evidence.minimumDepth, value); Evidence.maximumDepth = Mathf.Max(Evidence.maximumDepth, value);
                if (frame.ReversedZ ? value > .000001f : value < .999999f) Evidence.nonFarDepthSamples++;
            }
            for (int y = frame.Color.height / 2 - 4; y < frame.Color.height / 2 + 4; y++)
                for (int x = frame.Color.width / 2 - 8; x < frame.Color.width / 2 + 8; x++)
                {
                    int i = y * frame.Color.width + x; float[] add = { raw[i] * .2f, parameters.intensity * .1f, .03f };
                    for (int c = 0; c < 3; c++) Evidence.maxRgbError = Math.Max(Evidence.maxRgbError,
                        Math.Abs(after[i * 4 + c] - Mathf.Clamp(Mathf.RoundToInt(before[i * 4 + c] * .5f + add[c] * 255), 0, 255)));
                    Evidence.pixels++;
                }
            if (Evidence.maxRgbError > 1) throw new InvalidOperationException("Current depth/parameter RGB comparison failed."); Evidence.rgbSamples++;
        }
        if (Evidence.inputRenders > 0 && !parameters.Equals(previous)) Evidence.parameterChanges++;
        previous = parameters; Evidence.inputRenders++; Evidence.lastRenderSerial = frame.RenderSerial; Evidence.lastSelectedSerial = frame.SelectedSerial;
        return result;
    }
    public bool Passed => Evidence.inputRenders > 0 && Evidence.rgbSamples > 0 && Evidence.nonFarDepthSamples > 0 && Evidence.maxRgbError <= 1;
    public void Save(string output) => File.WriteAllText(Path.Combine(output, "frame-inputs.private.json"), JsonUtility.ToJson(Evidence, true));
    public void Dispose() { chain?.Dispose(); UnityEngine.Object.DestroyImmediate(material); }
}
