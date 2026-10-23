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
        IOriginalCameraPostProcessor inputPostProcessor;
        IOriginalCameraFramePostProcessor framePostProcessor;
        Material depthCopy;
        RenderTexture depth;
        long inputSerial;
        RenderTexture lastInput;
        public RenderTexture SharedInput => lastInput ? lastInput : shared;
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
        public void SetInputPostProcessor(IOriginalCameraPostProcessor processor)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalCameraProducer));
            if (rendering) throw new InvalidOperationException("Cannot change a rendering producer.");
            inputPostProcessor = processor; framePostProcessor = null; lastInput = null;
        }
        // Explicit opt-in: depth is copied while this private camera owns the
        // render. Never resolve a previous camera's global depth after returning.
        public void SetInputFramePostProcessor(IOriginalCameraFramePostProcessor processor, Shader depthCopyShader)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalCameraProducer));
            if (rendering) throw new InvalidOperationException("Cannot change a rendering producer.");
            if (processor != null && (!depthCopyShader || !depthCopyShader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat)))
                throw new ArgumentException("An explicit supported depth-copy shader and RFloat target are required.");
            Material replacement = processor == null ? null : new Material(depthCopyShader) { hideFlags = HideFlags.HideAndDontSave };
            if (replacement && replacement.passCount < 2) { Destroy(replacement); throw new ArgumentException("Bound and current-camera depth-copy passes are required."); }
            if (depthCopy) Destroy(depthCopy);
            depthCopy = replacement;
            framePostProcessor = processor; inputPostProcessor = null; lastInput = null;
        }
        public RenderTexture RenderInput(Camera source) => Render(source, shared, true);
        public RenderTexture RenderInput(Camera source, long selectedSerial) => Render(source, shared, true, selectedSerial);
        RenderTexture Render(Camera source, RenderTexture output, bool processInput = false, long selectedSerial = 0)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalCameraProducer));
            if (!source) throw new ArgumentNullException(nameof(source));
            if (rendering) throw new InvalidOperationException("Camera producer reentry.");
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite; rendering = true;
            bool captureDepth = processInput && framePostProcessor != null;
            CommandBuffer copy = null;
            Action<ScriptableRenderContext, Camera> onCameraEnd = null;
            UniversalAdditionalCameraData additional = null;
            CameraOverrideOption oldDepthRequest = CameraOverrideOption.UsePipelineSettings;
            bool depthSeen = false, attached = false;
            try
            {
                renderCamera.CopyFrom(source);
                renderCamera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                renderCamera.targetTexture = output; renderCamera.enabled = false;
                GL.sRGBWrite = false;
                if (captureDepth)
                {
                    if (!depth)
                    {
                        depth = new RenderTexture(output.width, output.height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                        { name = "Original.CurrentCameraDepth", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                        if (!depth.Create()) throw new InvalidOperationException("Depth target creation failed.");
                    }
                    if (!depth.IsCreated()) throw new InvalidOperationException("Depth target has been released.");
                    RenderTexture.active = depth; GL.Clear(false, true, new Color(-1, -1, -1, -1));
                    RenderTexture.active = output;
                    renderCamera.depthTextureMode |= DepthTextureMode.Depth;
                    copy = new CommandBuffer { name = "Original.CopyCurrentCameraDepth" };
                    if (GraphicsSettings.currentRenderPipeline == null)
                    {
                        copy.Blit(BuiltinRenderTextureType.Depth, depth, depthCopy);
                        renderCamera.AddCommandBuffer(CameraEvent.BeforeImageEffects, copy);
                        attached = true;
                    }
                    else if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                    {
                        additional = renderCamera.GetUniversalAdditionalCameraData();
                        oldDepthRequest = additional.requiresDepthOption; additional.requiresDepthTexture = true;
                        // URP's RTHandle is not a temporary RT registered under
                        // this name. Sample the current global binding in pass 1.
                        copy.Blit(Texture2D.blackTexture, depth, depthCopy, 1);
                        onCameraEnd = (context, camera) =>
                        {
                            if (camera != renderCamera) return;
                            context.ExecuteCommandBuffer(copy); context.Submit(); depthSeen = true;
                        };
                        RenderPipelineManager.endCameraRendering += onCameraEnd;
                    }
                    else throw new NotSupportedException("Depth capture supports Built-in and URP only.");
                }
                if (GraphicsSettings.currentRenderPipeline == null)
                {
                    renderCamera.Render(); depthSeen = true; LastBackend = "BuiltIn.Camera.Render";
                }
                else if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                {
                    renderCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = output };
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(renderCamera, request);
                    LastBackend = "URP.SingleCameraRequest";
                }
                else throw new NotSupportedException("This explicit camera producer supports Built-in and URP.");
                RenderCount++;
                if (processInput)
                {
                    RenderTexture result;
                    if (captureDepth)
                    {
                        if (!depthSeen) throw new InvalidOperationException("The current camera depth callback was not observed.");
                        result = framePostProcessor.Process(new OriginalCameraFrame(source, renderCamera, output, depth, ++inputSerial, selectedSerial));
                    }
                    else result = inputPostProcessor == null ? output : inputPostProcessor.Process(source, output);
                    if (!result || !result.IsCreated() || result.width != output.width || result.height != output.height)
                        throw new InvalidOperationException("Postprocessor must return a created target of the camera input size.");
                    lastInput = result; return result;
                }
                return output;
            }
            finally
            {
                if (onCameraEnd != null) RenderPipelineManager.endCameraRendering -= onCameraEnd;
                if (additional) additional.requiresDepthOption = oldDepthRequest;
                if (copy != null) { if (attached) renderCamera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, copy); copy.Release(); }
                renderCamera.targetTexture = null; RenderTexture.active = active; GL.sRGBWrite = srgb; rendering = false;
            }
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
            if (depth) { depth.Release(); Destroy(depth); depth = null; }
            if (depthCopy) { Destroy(depthCopy); depthCopy = null; }
            framePostProcessor = null; inputPostProcessor = null; lastInput = null; Destroy(cameraObject); disposed = true;
        }
    }
}
