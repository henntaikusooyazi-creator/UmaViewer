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
        public readonly struct Binding
        {
            public readonly string Property;
            public readonly int Stage;
            public readonly Texture External;
            public Binding(string property, int stage, Texture external = null)
            {
                if (string.IsNullOrEmpty(property) || stage < -2 || (stage == -2 && !external))
                    throw new ArgumentException("An explicit texture binding is required.");
                Property = property; Stage = stage; External = external;
            }
        }
        public sealed class Pass
        {
            public readonly Material Material;
            public readonly Mesh Mesh;
            internal readonly bool UsesMesh;
            public readonly int ShaderPass, Width, Height;
            internal readonly Binding[] Bindings;
            public Pass(Material material, int shaderPass, int width, int height,
                Mesh mesh, params Binding[] bindings)
            {
                if (!material || !material.shader || !material.shader.isSupported || shaderPass < 0 || shaderPass >= material.passCount || width <= 0 || height <= 0)
                    throw new ArgumentException("A valid program, pass and target size are required.");
                Material = material; ShaderPass = shaderPass; Width = width; Height = height; Mesh = mesh; UsesMesh = mesh;
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
                            (binding.Stage == -2 && !binding.External)) throw new ArgumentException("Invalid or forward texture reference.");
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
                    if (!pass.Material || !pass.Material.shader || !pass.Material.shader.isSupported || pass.ShaderPass >= pass.Material.passCount || (pass.UsesMesh && !pass.Mesh) || !targets[i].IsCreated())
                        throw new InvalidOperationException("The explicit pass inputs are unavailable.");
                    Texture blitSource = i == 0 ? input : targets[i - 1];
                    foreach (var binding in pass.Bindings)
                    {
                        Texture texture = binding.Stage == -1 ? input : binding.Stage == -2 ? binding.External : targets[binding.Stage];
                        if (!texture) throw new InvalidOperationException("A required texture is unavailable.");
                        if (texture is RenderTexture target && !target.IsCreated())
                            throw new InvalidOperationException("A required texture has been released.");
                        pass.Material.SetTexture(binding.Property, texture);
                        if (binding.Property == "_MainTex") blitSource = texture;
                    }
                    RenderTexture.active = targets[i]; GL.Clear(false, true, Color.clear);
                    if (pass.UsesMesh)
                    {
                        if (!pass.Material.SetPass(pass.ShaderPass)) throw new InvalidOperationException("The explicit shader pass is unavailable.");
                        Graphics.DrawMeshNow(pass.Mesh, Matrix4x4.identity);
                    }
                    else Graphics.Blit(blitSource, targets[i], pass.Material, pass.ShaderPass);
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
