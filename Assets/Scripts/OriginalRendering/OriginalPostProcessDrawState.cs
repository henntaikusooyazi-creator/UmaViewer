using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Fullscreen draws have their own transforms, separate from the camera
    // producing color/depth. The caller supplies transforms and texture Y sign;
    // original-game layouts and effect globals are not inferred here.
    public sealed class OriginalPostProcessDrawState
    {
        public readonly Matrix4x4 ObjectToWorld, GpuViewProjection;
        public readonly Vector4 TexelSize, UvScaleOffset;
        public readonly Camera Camera;
        public readonly Texture Input;
        public readonly long SelectedSerial, RenderSerial;
        readonly RenderTexture color, depth;
        readonly int width, height;

        OriginalPostProcessDrawState(OriginalCameraFrame frame, Texture input,
            Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, bool inputYFlipped,
            Vector2 uvScale, Vector2 uvOffset)
        {
            Camera = frame.Source; Input = input; color = frame.Color; depth = frame.Depth;
            SelectedSerial = frame.SelectedSerial; RenderSerial = frame.RenderSerial;
            width = input.width; height = input.height;
            ObjectToWorld = model;
            GpuViewProjection = GL.GetGPUProjectionMatrix(projection, true) * view;
            Finite(GpuViewProjection);
            TexelSize = new Vector4(1f / width, (inputYFlipped ? -1f : 1f) / height, width, height);
            UvScaleOffset = new Vector4(uvScale.x, uvScale.y, uvOffset.x, uvOffset.y);
        }
        public static OriginalPostProcessDrawState Capture(OriginalCameraFrame frame,
            Texture input, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, bool inputYFlipped)
            => Capture(frame, input, model, view, projection, inputYFlipped, Vector2.one, Vector2.zero);

        // Use the current draw command's UV arguments. Effect globals, tint,
        // backend projection ownership and sampler selection remain separate.
        public static OriginalPostProcessDrawState Capture(OriginalCameraFrame frame,
            Texture input, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, bool inputYFlipped,
            Vector2 uvScale, Vector2 uvOffset)
        {
            Live(frame); Available(input);
            Finite(model); Finite(view); Finite(projection);
            Finite(uvScale); Finite(uvOffset);
            return new OriginalPostProcessDrawState(frame, input, model, view, projection,
                inputYFlipped, uvScale, uvOffset);
        }
        // Consume synchronously. Serials do not extend a texture's lifetime or
        // make a reused render target immutable.
        public void ValidateCurrent(OriginalCameraFrame frame, Texture input)
        {
            Live(frame); Available(input);
            if (frame.Source != Camera || frame.SelectedSerial != SelectedSerial ||
                frame.RenderSerial != RenderSerial || frame.Color != color || frame.Depth != depth ||
                input != Input || input.width != width || input.height != height)
                throw new InvalidOperationException("Fullscreen draw state belongs to a different input or update.");
        }
        static void Live(OriginalCameraFrame frame)
        {
            if (!frame.Source || !frame.Color || !frame.Color.IsCreated() ||
                frame.SelectedSerial <= 0 || frame.RenderSerial <= 0)
                throw new InvalidOperationException("A current rendered camera frame is required.");
        }
        static void Available(Texture input)
        {
            if (!input || input.width <= 0 || input.height <= 0 ||
                (input is RenderTexture rt && !rt.IsCreated()))
                throw new InvalidOperationException("A live draw input texture is required.");
        }
        static void Finite(Matrix4x4 matrix)
        {
            for (int i = 0; i < 16; i++)
                if (float.IsNaN(matrix[i]) || float.IsInfinity(matrix[i]))
                    throw new ArgumentException("Draw transforms must be finite.");
        }
        static void Finite(Vector2 value)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y))
                throw new ArgumentException("Draw UV arguments must be finite.");
        }
    }
}
