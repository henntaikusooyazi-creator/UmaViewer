using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Port of the saved MultiCameraComposite VS / PS0 arithmetic. Explicit inputs only:
    // no cue field conversion, camera selection, or interpretation of track indices.
    public sealed class OriginalMaskCoverageGenerator : IDisposable
    {
        public const string ShaderName = "Hidden/Gallop/OriginalMaskCoverage";

        public readonly struct Parameters
        {
            public readonly Vector4 Transform, TextureScaleOffset, MeshUvScaleOffset;
            public readonly float Thickness, Fade, TextureTexelSizeY, MipBias;
            public Parameters(Vector4 transform, float thickness, float fade,
                Vector4 textureScaleOffset, float textureTexelSizeY)
                : this(transform, thickness, fade, textureScaleOffset, textureTexelSizeY,
                    new Vector4(1, 1, 0, 0), 0) { }
            public Parameters(Vector4 transform, float thickness, float fade,
                Vector4 textureScaleOffset, float textureTexelSizeY, Vector4 meshUvScaleOffset, float mipBias)
            {
                Transform = transform; Thickness = thickness; Fade = fade;
                TextureScaleOffset = textureScaleOffset; TextureTexelSizeY = textureTexelSizeY;
                MeshUvScaleOffset = meshUvScaleOffset; MipBias = mipBias;
            }
        }

        readonly Material material;
        RenderTexture output;
        bool disposed;
        public RenderTexture Output => output;
        public int GenerationSerial { get; private set; }
        public Parameters LastParameters { get; private set; }

        public OriginalMaskCoverageGenerator(Shader shader)
        {
            if (!shader || !shader.isSupported || shader.name != ShaderName)
                throw new ArgumentException("Original mask coverage shader is unavailable.", nameof(shader));
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
                throw new NotSupportedException("Floating-point coverage storage is unavailable.");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public OriginalMultiCameraRenderer.Submission GenerateSubmission(Texture source, int width, int height, Parameters parameters)
        {
            return new OriginalMultiCameraRenderer.Submission(Generate(source, width, height, parameters),
                OriginalMultiCameraRenderer.InputEncoding.StraightColorWithCoverage);
        }

        public RenderTexture Generate(Texture source, int width, int height, Parameters parameters)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalMaskCoverageGenerator));
            if (!source || source == output || width <= 0 || height <= 0)
                throw new ArgumentException("External input and positive output dimensions are required.");
            if (!output || output.width != width || output.height != height)
            {
                ReleaseOutput();
                output = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                {
                    name = "Original.MaskCoverage", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false, autoGenerateMips = false, antiAliasing = 1, hideFlags = HideFlags.HideAndDontSave
                };
                if (!output.Create()) { ReleaseOutput(); throw new InvalidOperationException("Coverage RT creation failed."); }
            }
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                material.SetVector("_Transform", parameters.Transform);
                material.SetVector("_TextureScaleOffset", parameters.TextureScaleOffset);
                material.SetVector("_MeshUvScaleOffset", parameters.MeshUvScaleOffset);
                material.SetFloat("_TextureTexelSizeY", parameters.TextureTexelSizeY);
                material.SetFloat("_Thickness", parameters.Thickness);
                material.SetFloat("_Fade", parameters.Fade);
                material.SetFloat("_MipBias", parameters.MipBias);
                Graphics.Blit(source, output, material, 0);
                LastParameters = parameters; GenerationSerial++;
                return output;
            }
            finally
            {
                material.SetTexture("_MainTex", null);
                RenderTexture.active = active; GL.sRGBWrite = srgb;
            }
        }

        void ReleaseOutput()
        {
            if (!output) return;
            output.Release(); Destroy(output); output = null;
        }
        static void Destroy(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
        }
        public void Dispose()
        {
            if (disposed) return;
            ReleaseOutput(); Destroy(material); disposed = true;
        }
    }
}
