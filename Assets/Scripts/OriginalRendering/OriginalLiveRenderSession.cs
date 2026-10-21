using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Constructing a session is explicit opt-in. It owns no source camera or output RT.
    public sealed class OriginalLiveRenderSession : IDisposable
    {
        readonly OriginalLiveTimelineBridge bridge;
        readonly OriginalCameraProducer cameras;
        readonly OriginalMaskCoverageGenerator coverage;
        readonly OriginalMultiCameraRenderer renderer;
        readonly Camera baseCamera, rightCamera, leftCamera;
        readonly Func<OriginalLiveTimelineBridge.Frame, Camera> selectedBaseCamera;
        readonly RenderTexture output;
        readonly int rightTrack, leftTrack;
        readonly Func<OriginalLiveTimelineBridge.Selection, OriginalCompositeCueAdapter.Snapshot> resolve;
        bool disposed, rendering;
        public int RenderCount { get; private set; }
        public long LastFrameSerial { get; private set; }
        public string LastStatus { get; private set; } = "NotRendered";
        public Exception LastException { get; private set; }
        public OriginalCompositeCueAdapter.Unresolved LastUnresolved { get; private set; }
        public OriginalLiveRenderSession(OriginalLiveTimelineBridge bridge, Shader compositeShader, Shader coverageShader,
            int rightTrack, int leftTrack, Camera baseCamera, Camera rightCamera, Camera leftCamera, RenderTexture output,
            Func<OriginalLiveTimelineBridge.Selection, OriginalCompositeCueAdapter.Snapshot> resolve)
        {
            if (bridge == null || !output || !output.IsCreated() || !baseCamera || !rightCamera || !leftCamera)
                throw new ArgumentException("Explicit bridge, cameras and created output are required.");
            this.bridge = bridge; this.rightTrack = rightTrack; this.leftTrack = leftTrack;
            this.baseCamera = baseCamera; this.rightCamera = rightCamera; this.leftCamera = leftCamera;
            this.output = output; this.resolve = resolve;
            try
            {
                cameras = new OriginalCameraProducer(output.width, output.height);
                coverage = new OriginalMaskCoverageGenerator(coverageShader);
                renderer = new OriginalMultiCameraRenderer(compositeShader, RenderTextureFormat.ARGBFloat);
            }
            catch { cameras?.Dispose(); coverage?.Dispose(); renderer?.Dispose(); throw; }
            bridge.FrameReady += OnFrame;
        }
        // This overload uses the camera references attached to the actual selected
        // Viewer tracks. The old explicit-camera constructor remains unchanged.
        public OriginalLiveRenderSession(OriginalLiveTimelineBridge bridge, Shader compositeShader, Shader coverageShader,
            int rightTrack, int leftTrack, Func<OriginalLiveTimelineBridge.Frame, Camera> selectedBaseCamera,
            RenderTexture output, Func<OriginalLiveTimelineBridge.Selection, OriginalCompositeCueAdapter.Snapshot> resolve)
        {
            if (bridge == null || selectedBaseCamera == null || !output || !output.IsCreated())
                throw new ArgumentException("Explicit bridge, base camera resolver and created output are required.");
            this.bridge = bridge; this.rightTrack = rightTrack; this.leftTrack = leftTrack;
            this.selectedBaseCamera = selectedBaseCamera; this.output = output; this.resolve = resolve;
            try
            {
                cameras = new OriginalCameraProducer(output.width, output.height);
                coverage = new OriginalMaskCoverageGenerator(coverageShader);
                renderer = new OriginalMultiCameraRenderer(compositeShader, RenderTextureFormat.ARGBFloat);
            }
            catch { cameras?.Dispose(); coverage?.Dispose(); renderer?.Dispose(); throw; }
            bridge.FrameReady += OnFrame;
        }
        void OnFrame(OriginalLiveTimelineBridge.Frame frame)
        {
            if (disposed || rendering) return;
            LastFrameSerial = frame.Serial; LastException = null; LastUnresolved = OriginalCompositeCueAdapter.Unresolved.None;
            if (!frame.TryGetTrack(rightTrack, out var right) || !frame.TryGetTrack(leftTrack, out var left)) { LastStatus = "MissingTrackSelection"; return; }
            rendering = true;
            try
            {
                var r = resolve != null ? resolve(right) : right.Cue;
                var l = resolve != null ? resolve(left) : left.Cue;
                bool readyR = r.TryCreatePass0Parameters(out var rp, out var ru);
                bool readyL = l.TryCreatePass0Parameters(out var lp, out var lu);
                LastUnresolved = ru | lu;
                if (!readyR || !readyL) { LastStatus = "UnresolvedDrawInputs"; return; }
                var baseSource = selectedBaseCamera != null ? selectedBaseCamera(frame) : baseCamera;
                var rightSource = selectedBaseCamera != null ? right.ViewerCamera : rightCamera;
                var leftSource = selectedBaseCamera != null ? left.ViewerCamera : leftCamera;
                if (selectedBaseCamera != null && (!baseSource || !rightSource || !leftSource))
                { LastStatus = "MissingSelectedCamera"; return; }
                Texture baseImage = cameras.RenderBase(baseSource);
                renderer.RenderSequential(baseImage,
                    () => coverage.GenerateSubmission(cameras.RenderInput(rightSource), output.width, output.height, rp),
                    () => coverage.GenerateSubmission(cameras.RenderInput(leftSource), output.width, output.height, lp), output);
                RenderCount++; LastStatus = "RenderedExplicitViewerCameras";
            }
            catch (Exception e) { LastException = e; LastStatus = "RenderFailed"; }
            finally { rendering = false; }
        }
        public void Dispose()
        {
            if (disposed) return;
            if (rendering) throw new InvalidOperationException("Cannot dispose a rendering session.");
            bridge.FrameReady -= OnFrame; renderer.Dispose(); coverage.Dispose(); cameras.Dispose(); disposed = true;
        }
    }
}
