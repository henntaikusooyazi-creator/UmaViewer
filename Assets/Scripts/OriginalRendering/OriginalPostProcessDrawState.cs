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
        // Raw current material property; no color-space conversion is inferred.
        // Legacy captures leave this absent rather than inventing a tint.
        public readonly Vector4? MaterialVector;
        readonly RenderTexture color, depth;
        readonly int width, height;
        readonly Material vectorSource;
        readonly Shader vectorShader;
        readonly string vectorProperty;

        OriginalPostProcessDrawState(OriginalCameraFrame frame, Texture input,
            Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, bool inputYFlipped,
            Vector2 uvScale, Vector2 uvOffset, Material vectorSource = null,
            string vectorProperty = null, Vector4? materialVector = null)
        {
            Camera = frame.Source; Input = input; color = frame.Color; depth = frame.Depth;
            SelectedSerial = frame.SelectedSerial; RenderSerial = frame.RenderSerial;
            width = input.width; height = input.height;
            ObjectToWorld = model;
            GpuViewProjection = GL.GetGPUProjectionMatrix(projection, true) * view;
            Finite(GpuViewProjection);
            TexelSize = new Vector4(1f / width, (inputYFlipped ? -1f : 1f) / height, width, height);
            UvScaleOffset = new Vector4(uvScale.x, uvScale.y, uvOffset.x, uvOffset.y);
            this.vectorSource = vectorSource; vectorShader = vectorSource ? vectorSource.shader : null;
            this.vectorProperty = vectorProperty; MaterialVector = materialVector;
        }
        // Snapshot a caller-owned material synchronously with this draw. The
        // property can originate in shader defaults or a current explicit write.
        public static OriginalPostProcessDrawState Capture(OriginalCameraFrame frame,
            Texture input, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, bool inputYFlipped,
            Vector2 uvScale, Vector2 uvOffset, Material vectorSource, string vectorProperty)
        {
            Live(frame); Available(input);
            Finite(model); Finite(view); Finite(projection); Finite(uvScale); Finite(uvOffset);
            Vector4 value = ReadVector(vectorSource, vectorProperty);
            return new OriginalPostProcessDrawState(frame, input, model, view, projection,
                inputYFlipped, uvScale, uvOffset, vectorSource, vectorProperty, value);
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
            if (MaterialVector.HasValue && (!vectorSource || vectorSource.shader != vectorShader ||
                !ReadVector(vectorSource, vectorProperty).Equals(MaterialVector.Value)))
                throw new InvalidOperationException("Fullscreen material property changed after capture.");
        }
        static Vector4 ReadVector(Material source, string property)
        {
            if (!source || !source.shader || string.IsNullOrEmpty(property))
                throw new InvalidOperationException("A live material and named vector property are required.");
            int index = source.shader.FindPropertyIndex(property);
            if (index < 0 || (source.shader.GetPropertyType(index) != UnityEngine.Rendering.ShaderPropertyType.Vector &&
                source.shader.GetPropertyType(index) != UnityEngine.Rendering.ShaderPropertyType.Color))
                throw new InvalidOperationException("The material property must be a declared color or vector.");
            Vector4 value = source.GetVector(property);
            for (int i = 0; i < 4; i++)
                if (float.IsNaN(value[i]) || float.IsInfinity(value[i]))
                    throw new ArgumentException("Material vectors must be finite.");
            return value;
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
