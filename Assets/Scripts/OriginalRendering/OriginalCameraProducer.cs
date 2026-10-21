using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.Live.Rendering
{
    // Explicit Viewer camera preview. Source components and their SetTargetBuffers /
    // targetTexture state are not changed. Camera scripts/effect components are not cloned.
    public sealed class OriginalCameraProducer : IDisposable
    {
        readonly GameObject cameraObject;
        readonly Camera renderCamera;
        RenderTexture shared, backdrop;
        bool disposed, rendering;
        public RenderTexture SharedInput => shared;
        public RenderTexture Backdrop => backdrop;
        public int RenderCount { get; private set; }
        public string LastBackend { get; private set; }
        public OriginalCameraProducer(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            cameraObject = new GameObject("Original.ExplicitCamera") { hideFlags = HideFlags.HideAndDontSave };
            renderCamera = cameraObject.AddComponent<Camera>(); renderCamera.enabled = false;
            try { shared = Target(width, height, "Original.CameraInput"); backdrop = Target(width, height, "Original.CameraBase"); }
            catch { Dispose(); throw; }
        }
        public RenderTexture RenderBase(Camera source) => Render(source, backdrop);
        public RenderTexture RenderInput(Camera source) => Render(source, shared);
        RenderTexture Render(Camera source, RenderTexture output)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalCameraProducer));
            if (!source) throw new ArgumentNullException(nameof(source));
            if (rendering) throw new InvalidOperationException("Camera producer reentry.");
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite; rendering = true;
            try
            {
                renderCamera.CopyFrom(source);
                renderCamera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                renderCamera.targetTexture = output; renderCamera.enabled = false;
                GL.sRGBWrite = false;
                if (GraphicsSettings.currentRenderPipeline == null)
                {
                    renderCamera.Render(); LastBackend = "BuiltIn.Camera.Render";
                }
                else if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                {
                    renderCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = output };
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(renderCamera, request);
                    LastBackend = "URP.SingleCameraRequest";
                }
                else throw new NotSupportedException("This explicit camera producer supports Built-in and URP.");
                RenderCount++; return output;
            }
            finally { renderCamera.targetTexture = null; RenderTexture.active = active; GL.sRGBWrite = srgb; rendering = false; }
        }
        static RenderTexture Target(int width, int height, string name)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1, hideFlags = HideFlags.HideAndDontSave };
            if (!rt.Create()) { Destroy(rt); throw new InvalidOperationException("Camera RT creation failed."); }
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
            try { GL.sRGBWrite = false; RenderTexture.active = rt; GL.Clear(true, true, Color.clear); }
            finally { RenderTexture.active = active; GL.sRGBWrite = srgb; }
            return rt;
        }
        static void Destroy(UnityEngine.Object value)
        { if (!value) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
        public void Dispose()
        {
            if (disposed) return;
            if (rendering) throw new InvalidOperationException("Cannot dispose a rendering camera producer.");
            if (shared) { shared.Release(); Destroy(shared); shared = null; }
            if (backdrop) { backdrop.Release(); Destroy(backdrop); backdrop = null; }
            Destroy(cameraObject); disposed = true;
        }
    }
}
