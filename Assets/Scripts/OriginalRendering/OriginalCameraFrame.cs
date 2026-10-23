using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Textures belong to the producer and are reused at the next render. Consume
    // or copy them inside Process; the serial does not make an RT immutable.
    public readonly struct OriginalCameraFrame
    {
        public readonly Camera Source;
        public readonly RenderTexture Color, Depth;
        public readonly long RenderSerial, SelectedSerial;
        public readonly Matrix4x4 View, Projection;
        public readonly float Near, Far;
        public readonly bool ReversedZ;
        internal OriginalCameraFrame(Camera source, Camera rendered, RenderTexture color,
            RenderTexture depth, long renderSerial, long selectedSerial)
        {
            Source = source; Color = color; Depth = depth; RenderSerial = renderSerial;
            SelectedSerial = selectedSerial; View = rendered.worldToCameraMatrix;
            Projection = GL.GetGPUProjectionMatrix(rendered.projectionMatrix, true);
            Near = rendered.nearClipPlane; Far = rendered.farClipPlane;
            ReversedZ = SystemInfo.usesReversedZBuffer;
        }
    }

    public interface IOriginalCameraFramePostProcessor
    {
        RenderTexture Process(OriginalCameraFrame frame);
    }
}
