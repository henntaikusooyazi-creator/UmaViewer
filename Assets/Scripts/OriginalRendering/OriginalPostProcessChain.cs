using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    public interface IOriginalCameraPostProcessor
    {
        RenderTexture Process(Camera camera, RenderTexture input);
    }

    // Explicit caller-provided programs and inputs. This class establishes no
    // original-game shader, camera, depth, or clock mapping.
    public sealed class OriginalPostProcessChain : IOriginalCameraPostProcessor, IDisposable
    {
        public sealed class Program
        {
            public readonly Material Material;
            public readonly int ShaderPass;
            readonly Shader shader;
            readonly string[] keywords;
            public Program(Material material, int shaderPass)
            {
                if (!material || !material.shader || !material.shader.isSupported || shaderPass < 0 || shaderPass >= material.passCount)
                    throw new ArgumentException("An available registered program is required.");
                Material = material; ShaderPass = shaderPass; shader = material.shader;
                keywords = material.shaderKeywords; Array.Sort(keywords, StringComparer.Ordinal);
            }
            internal void Validate()
            {
                if (!Material || Material.shader != shader || !shader || !shader.isSupported || ShaderPass >= Material.passCount)
                    throw new InvalidOperationException("The registered program changed or became unavailable.");
                var current = Material.shaderKeywords; Array.Sort(current, StringComparer.Ordinal);
                if (current.Length != keywords.Length) throw new InvalidOperationException("Registered program keywords changed.");
                for (int i = 0; i < current.Length; i++) if (current[i] != keywords[i]) throw new InvalidOperationException("Registered program keywords changed.");
            }
        }
        public readonly struct Binding
        {
            public readonly string Property;
            public readonly int Stage;
            public readonly Texture External;
            internal readonly Func<Texture> ResolveExternal;
            public Binding(string property, int stage, Texture external = null)
            {
                if (string.IsNullOrEmpty(property) || stage < -2 || (stage == -2 && !external))
                    throw new ArgumentException("An explicit texture binding is required.");
                Property = property; Stage = stage; External = external; ResolveExternal = null;
            }
            // Resolve once per draw, inside the synchronous frame callback. No
            // texture from the preceding render is retained as a fallback.
            public Binding(string property, Func<Texture> external)
            {
                if (string.IsNullOrEmpty(property) || external == null)
                    throw new ArgumentException("An explicit current texture resolver is required.");
                Property = property; Stage = -2; External = null; ResolveExternal = external;
            }
        }
        public sealed class Pass
        {
            public readonly Material Material;
            public readonly Mesh Mesh;
            internal readonly bool UsesMesh;
            public readonly int ShaderPass, Width, Height;
            internal readonly Binding[] Bindings;
            internal readonly Func<Program> ResolveProgram;
            public Pass(Material material, int shaderPass, int width, int height,
                Mesh mesh, params Binding[] bindings)
            {
                if (!material || !material.shader || !material.shader.isSupported || shaderPass < 0 || shaderPass >= material.passCount || width <= 0 || height <= 0)
                    throw new ArgumentException("A valid program, pass and target size are required.");
                Material = material; ShaderPass = shaderPass; Width = width; Height = height; Mesh = mesh; UsesMesh = mesh;
                Bindings = bindings == null ? Array.Empty<Binding>() : (Binding[])bindings.Clone();
            }
            public Pass(Func<Program> program, int width, int height, Mesh mesh, params Binding[] bindings)
            {
                ResolveProgram = program ?? throw new ArgumentNullException(nameof(program));
                if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
                Width = width; Height = height; Mesh = mesh; UsesMesh = mesh;
                Bindings = bindings == null ? Array.Empty<Binding>() : (Binding[])bindings.Clone();
            }
        }
        readonly Pass[] passes;
        readonly RenderTexture[] targets;
        bool disposed, processing;
        public int ProcessCount { get; private set; }
        public OriginalPostProcessChain(params Pass[] passes)
        {
            if (passes == null || passes.Length == 0 || passes.Length > 16)
                throw new ArgumentException("One to sixteen explicit passes are required.");
            this.passes = (Pass[])passes.Clone(); targets = new RenderTexture[passes.Length];
            try
            {
                for (int i = 0; i < passes.Length; i++)
                {
                    var pass = this.passes[i] ?? throw new ArgumentException("A pass is missing.");
                    foreach (var binding in pass.Bindings)
                        if (string.IsNullOrEmpty(binding.Property) || binding.Stage >= i || binding.Stage < -2 ||
                            (binding.Stage == -2 && !binding.External && binding.ResolveExternal == null)) throw new ArgumentException("Invalid or forward texture reference.");
                    var rt = new RenderTexture(pass.Width, pass.Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    { name = "Original.ExplicitPostProcess." + i, antiAliasing = 1, filterMode = FilterMode.Bilinear,
                      wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                    targets[i] = rt;
                    if (!rt.Create()) throw new InvalidOperationException("Postprocess target creation failed.");
                }
            }
            catch { Dispose(); throw; }
        }
        public RenderTexture Process(Camera camera, RenderTexture input)
            => Process(camera, input, null, null);

        // Both callbacks are synchronous. Copy a completed target in the
        // callback if it must survive reuse. The caller owns parameter meaning;
        // no timeline-to-shader conversion or missing-value fallback is implied.
        public RenderTexture Process(Camera camera, RenderTexture input,
            Action<int, Material> prepare, Action<int, RenderTexture> completed)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalPostProcessChain));
            if (!input || !input.IsCreated()) throw new ArgumentException("A created camera input is required.");
            if (processing) throw new InvalidOperationException("Postprocess reentry.");
            foreach (var target in targets) if (target == input) throw new ArgumentException("Input aliases an owned output.");
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite; processing = true;
            try
            {
                GL.sRGBWrite = false;
                for (int i = 0; i < passes.Length; i++)
                {
                    var pass = passes[i];
                    var program = pass.ResolveProgram != null ? pass.ResolveProgram() ?? throw new InvalidOperationException("Program resolution returned no registered program.") : null;
                    program?.Validate();
                    var material = program != null ? program.Material : pass.Material;
                    int shaderPass = program != null ? program.ShaderPass : pass.ShaderPass;
                    if (!material || !material.shader || !material.shader.isSupported || shaderPass >= material.passCount || (pass.UsesMesh && !pass.Mesh) || !targets[i].IsCreated())
                        throw new InvalidOperationException("The explicit pass inputs are unavailable.");
                    prepare?.Invoke(i, material);
                    program?.Validate();
                    if (!input || !input.IsCreated() || !material || !material.shader || !material.shader.isSupported ||
                        shaderPass >= material.passCount || (pass.UsesMesh && !pass.Mesh) || !targets[i].IsCreated())
                        throw new InvalidOperationException("The prepared pass inputs are unavailable.");
                    Texture blitSource = i == 0 ? input : targets[i - 1];
                    foreach (var binding in pass.Bindings)
                    {
                        Texture texture = binding.Stage == -1 ? input : binding.Stage == -2 ?
                            (binding.ResolveExternal != null ? binding.ResolveExternal() : binding.External) : targets[binding.Stage];
                        if (!texture) throw new InvalidOperationException("A required texture is unavailable.");
                        if (texture == targets[i]) throw new InvalidOperationException("A texture aliases the current output.");
                        if (texture is RenderTexture target && !target.IsCreated())
                            throw new InvalidOperationException("A required texture has been released.");
                        material.SetTexture(binding.Property, texture);
                        if (binding.Property == "_MainTex") blitSource = texture;
                    }
                    program?.Validate();
                    GL.sRGBWrite = false;
                    RenderTexture.active = targets[i]; GL.Clear(false, true, Color.clear);
                    if (pass.UsesMesh)
                    {
                        if (!material.SetPass(shaderPass)) throw new InvalidOperationException("The explicit shader pass is unavailable.");
                        Graphics.DrawMeshNow(pass.Mesh, Matrix4x4.identity);
                    }
                    else Graphics.Blit(blitSource, targets[i], material, shaderPass);
                    completed?.Invoke(i, targets[i]);
                }
                ProcessCount++; return targets[targets.Length - 1];
            }
            finally { RenderTexture.active = active; GL.sRGBWrite = srgb; processing = false; }
        }
        public void Dispose()
        {
            if (disposed) return;
            if (processing) throw new InvalidOperationException("Cannot dispose a processing chain.");
            disposed = true;
            foreach (var target in targets) if (target)
            {
                target.Release();
                if (Application.isPlaying) UnityEngine.Object.Destroy(target); else UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
