using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        // Pixel width of the private render camera while its target is bound.
        // Color.width is the requested output, not a general URP internal
        // descriptor. Consumers must qualify render-scale / XR / TAA policy.
        public readonly int PixelWidth;
        public readonly float DynamicWidthScale;
        // Null when this render camera has no URP settings. It is not filled
        // from the source camera, a shader global, or a preceding render.
        public readonly float? TemporalMipBias;
        public readonly bool StereoEnabled;
        internal OriginalCameraFrame(Camera source, Camera rendered, RenderTexture color,
            RenderTexture depth, long renderSerial, long selectedSerial)
        {
            Source = source; Color = color; Depth = depth; RenderSerial = renderSerial;
            SelectedSerial = selectedSerial; View = rendered.worldToCameraMatrix;
            Projection = GL.GetGPUProjectionMatrix(rendered.projectionMatrix, true);
            Near = rendered.nearClipPlane; Far = rendered.farClipPlane;
            ReversedZ = SystemInfo.usesReversedZBuffer;
            PixelWidth = rendered.pixelWidth;
            DynamicWidthScale = rendered.allowDynamicResolution ? ScalableBufferManager.widthScaleFactor : 1f;
            TemporalMipBias = rendered.TryGetComponent<UniversalAdditionalCameraData>(out var additional)
                ? (float?)additional.taaSettings.mipBias : null;
            StereoEnabled = rendered.stereoEnabled;
        }
    }

    public interface IOriginalCameraFramePostProcessor
    {
        RenderTexture Process(OriginalCameraFrame frame);
    }
}
