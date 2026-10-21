using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Opt-in ordered renderer. No cue decoding, camera identity inference or live hookup.
    // Pass an explicit shader reference when including this renderer in a Player build.
    public sealed class OriginalMultiCameraRenderer : IDisposable
    {
        public const string ShaderName = "Hidden/Gallop/OriginalMultiCamera";
        public enum InputEncoding { StraightColorWithCoverage = 0, CapturedPostBlendLayer = 1 }
        public enum Stage { Cleared, RightInputUpdated, RightMaskDrawn, LeftInputUpdated,
            LeftMaskDrawn, BasePrepared, FinalCompositeDrawn }

        public readonly struct Submission
        {
            public readonly Texture Image;
            public readonly InputEncoding Encoding;
            public readonly float Opacity;
            public readonly Vector4 ScaleOffset;
            public Submission(Texture image, InputEncoding encoding, float opacity = 1f)
                : this(image, encoding, opacity, new Vector4(1, 1, 0, 0)) { }
            public Submission(Texture image, InputEncoding encoding, float opacity, Vector4 scaleOffset)
            { Image = image; Encoding = encoding; Opacity = opacity; ScaleOffset = scaleOffset; }
        }

        public readonly struct Trace
        {
            public readonly Stage Step;
            public readonly int FrameSerial, InputVersion;
            public readonly RenderTexture SharedInput, Accumulation, Output;
            public readonly Submission Draw;
            public bool HasSubmission => Draw.Image != null;
            internal Trace(Stage step, int serial, int version, RenderTexture shared,
                RenderTexture accumulation, RenderTexture output, Submission draw)
            { Step = step; FrameSerial = serial; InputVersion = version; SharedInput = shared;
              Accumulation = accumulation; Output = output; Draw = draw; }
        }

        readonly Material rightMaterial, leftMaterial, finalMaterial, copyMaterial;
        readonly RenderTextureFormat sharedInputFormat;
        RenderTexture sharedInput, accumulation;
        int frameSerial;
        bool rendering, disposed;
        public RenderTexture SharedInput => sharedInput;
        public RenderTexture Accumulation => accumulation;

        public OriginalMultiCameraRenderer(Shader shader) : this(shader, RenderTextureFormat.ARGB32) { }

        // Float input preserves generated coverage until the actual blend. The existing
        // constructor and captured replay retain their original ARGB32 storage.
        public OriginalMultiCameraRenderer(Shader shader, RenderTextureFormat sharedInputFormat)
        {
            if (!shader || !shader.isSupported || shader.name != ShaderName)
                throw new ArgumentException("Original multi-camera shader is unavailable.", nameof(shader));
            if (sharedInputFormat != RenderTextureFormat.ARGB32 && sharedInputFormat != RenderTextureFormat.ARGBFloat)
                throw new ArgumentException("Unsupported shared input format.", nameof(sharedInputFormat));
            // Keep the established default path free of a new capability gate.
            if (sharedInputFormat == RenderTextureFormat.ARGBFloat && !SystemInfo.SupportsRenderTextureFormat(sharedInputFormat))
                throw new NotSupportedException("Floating-point shared input storage is unavailable.");
            this.sharedInputFormat = sharedInputFormat;
            rightMaterial = MakeMaterial(shader); leftMaterial = MakeMaterial(shader);
            finalMaterial = MakeMaterial(shader); copyMaterial = MakeMaterial(shader);
        }

        static Material MakeMaterial(Shader shader) => new Material(shader)
            { hideFlags = HideFlags.HideAndDontSave };

        public void Render(Texture baseImage, Submission right, Submission left,
            RenderTexture output, Action<Trace> observe = null)
        { RenderSequential(baseImage, () => right, () => left, output, observe); }

        // Producers run at their actual submission point. They may update the same
        // external camera RT; the right input is copied before the left producer runs.
        public void RenderSequential(Texture baseImage, Func<Submission> rightProducer,
            Func<Submission> leftProducer, RenderTexture output, Action<Trace> observe = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalMultiCameraRenderer));
            if (rendering) throw new InvalidOperationException("A renderer cannot recursively render itself.");
            if (!baseImage || rightProducer == null || leftProducer == null || !output || !output.IsCreated())
                throw new ArgumentException("Created color images and output are required.");
            if (baseImage == output)
                throw new ArgumentException("An input cannot also be the output.");
            EnsureTargets(output.width, output.height);
            if (output == sharedInput || output == accumulation)
                throw new ArgumentException("A renderer-owned intermediate cannot also be the output.");
            if (baseImage == sharedInput || baseImage == accumulation)
                throw new ArgumentException("Renderer-owned textures are not frame inputs.");
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
            rendering = true; int serial = ++frameSerial;
            try
            {
                // Byte/coverage replay uses linear UNorm storage; no implicit output flip.
                GL.sRGBWrite = false;
                RenderTexture.active = accumulation; GL.Clear(false, true, Color.clear);
                Emit(Stage.Cleared, 0, default, output, serial, observe);
                Submission right = rightProducer(); Validate(right, output); GL.sRGBWrite = false;
                Copy(right.Image, sharedInput);
                Emit(Stage.RightInputUpdated, 1, right, output, serial, observe);
                Draw(rightMaterial, right);
                Emit(Stage.RightMaskDrawn, 1, right, output, serial, observe);
                // The same input RT is updated only after the right command was submitted.
                Submission left = leftProducer(); Validate(left, output); GL.sRGBWrite = false;
                Copy(left.Image, sharedInput);
                Emit(Stage.LeftInputUpdated, 2, left, output, serial, observe);
                Draw(leftMaterial, left);
                Emit(Stage.LeftMaskDrawn, 2, left, output, serial, observe);
                Copy(baseImage, output);
                Emit(Stage.BasePrepared, 2, default, output, serial, observe);
                finalMaterial.SetFloat("_Opacity", 1);
                Graphics.Blit(accumulation, output, finalMaterial, 2);
                Emit(Stage.FinalCompositeDrawn, 2, default, output, serial, observe);
            }
            finally
            {
                rightMaterial.SetTexture("_MainTex", null); leftMaterial.SetTexture("_MainTex", null);
                finalMaterial.SetTexture("_MainTex", null); copyMaterial.SetTexture("_MainTex", null);
                RenderTexture.active = active; GL.sRGBWrite = srgb; rendering = false;
            }
        }

        void Validate(Submission submission, RenderTexture output)
        {
            if (!submission.Image || submission.Image == output || submission.Image == sharedInput || submission.Image == accumulation)
                throw new ArgumentException("A submission needs an external color input distinct from the output.");
            if (submission.Encoding != InputEncoding.StraightColorWithCoverage &&
                submission.Encoding != InputEncoding.CapturedPostBlendLayer)
                throw new ArgumentOutOfRangeException(nameof(submission.Encoding));
            if (float.IsNaN(submission.Opacity) || float.IsInfinity(submission.Opacity) ||
                submission.Opacity < 0 || submission.Opacity > 1)
                throw new ArgumentOutOfRangeException(nameof(submission.Opacity));
        }
        void Copy(Texture source, RenderTexture target)
        { copyMaterial.SetVector("_ScaleOffset", new Vector4(1, 1, 0, 0)); Graphics.Blit(source, target, copyMaterial, 3); }
        void Draw(Material material, Submission submission)
        {
            material.SetFloat("_Opacity", submission.Opacity);
            material.SetVector("_ScaleOffset", submission.ScaleOffset);
            Graphics.Blit(sharedInput, accumulation, material, (int)submission.Encoding);
        }
        void Emit(Stage stage, int version, Submission draw, RenderTexture output, int serial, Action<Trace> observer)
        { observer?.Invoke(new Trace(stage, serial, version, sharedInput, accumulation, output, draw)); }
        void EnsureTargets(int width, int height)
        {
            if (sharedInput && sharedInput.width == width && sharedInput.height == height) return;
            ReleaseTargets();
            sharedInput = Target(width, height, "Original.SharedInput", sharedInputFormat);
            accumulation = Target(width, height, "Original.MaskAccumulation");
        }
        static RenderTexture Target(int width, int height, string name, RenderTextureFormat format = RenderTextureFormat.ARGB32)
        {
            var rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear)
                { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                  useMipMap = false, autoGenerateMips = false, antiAliasing = 1, hideFlags = HideFlags.HideAndDontSave };
            if (!rt.Create()) { Destroy(rt); throw new InvalidOperationException("Render texture creation failed."); }
            return rt;
        }
        static void Destroy(UnityEngine.Object value)
        { if (!value) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
        void ReleaseTargets()
        {
            if (sharedInput) { sharedInput.Release(); Destroy(sharedInput); sharedInput = null; }
            if (accumulation) { accumulation.Release(); Destroy(accumulation); accumulation = null; }
        }
        public void Dispose()
        {
            if (disposed) return;
            if (rendering) throw new InvalidOperationException("Cannot dispose while rendering.");
            ReleaseTargets(); Destroy(rightMaterial); Destroy(leftMaterial); Destroy(finalMaterial); Destroy(copyMaterial);
            disposed = true;
        }
    }
}
