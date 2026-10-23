using System;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Caller supplies the confirmed conversion/program. Typed Viewer values do
    // not establish original-game constant-buffer layout or time correspondence.
    public sealed class OriginalLivePostProcessInputs : IOriginalCameraFramePostProcessor
    {
        readonly Func<OriginalLiveTimelineBridge.Frame> current;
        readonly Func<OriginalCameraFrame, PostEffectUpdateInfo_BloomDiffusion, RenderTexture> process;
        public long ProcessCount { get; private set; }
        public long LastSelectedSerial { get; private set; }
        public PostEffectUpdateInfo_BloomDiffusion LastParameters { get; private set; }
        public OriginalLivePostProcessInputs(Func<OriginalLiveTimelineBridge.Frame> current,
            Func<OriginalCameraFrame, PostEffectUpdateInfo_BloomDiffusion, RenderTexture> process)
        { this.current = current ?? throw new ArgumentNullException(nameof(current)); this.process = process ?? throw new ArgumentNullException(nameof(process)); }
        public RenderTexture Process(OriginalCameraFrame input)
        {
            var selected = current();
            if (selected == null || selected.Serial != input.SelectedSerial || !selected.PostEffect.HasValue)
                throw new InvalidOperationException("Current selected update and post-effect payload are required.");
            bool found = false;
            foreach (var selection in selected.Selections)
                if (selection.ViewerCamera == input.Source && selection.Serial == selected.Serial && selection.ComparisonClock.HasValue) found = true;
            if (!found || !input.Depth || !input.Depth.IsCreated() || !input.Color ||
                input.Depth.width != input.Color.width || input.Depth.height != input.Color.height)
                throw new InvalidOperationException("Current selected camera, owner observation and depth are required.");
            var value = selected.PostEffect.Value;
            var result = process(input, value);
            if (!result || !result.IsCreated() || result.width != input.Color.width || result.height != input.Color.height)
                throw new InvalidOperationException("The postprocessor did not return the current input size.");
            LastParameters = value; LastSelectedSerial = selected.Serial; ProcessCount++;
            return result;
        }
    }
}
