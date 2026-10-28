using System;
using System.IO;
using System.Linq;
using Gallop.Live;
using Gallop.Live.Cutt;
using Gallop.Live.Rendering;
using UnityEngine;

// Explicit saved clock consumption with synthetic geometry. No AudioSource read,
// original GPU frame, automatic epoch mapping or native layout is implied.
public static class OriginalObservedClockValidation
{
    [Serializable] sealed class Input
    {
        public string schema, smoothing;
        public float sourceTime;
        public float[] state, parameters;
        public int[] expectedBits;
        public int currentFrame, previousFrame;
    }
    [Serializable] sealed class Receipt
    {
        public string scope = "ExplicitObservedClock_ActualControl_SyntheticGPUReplay_NotOriginalFrameRGB";
        public bool runtime_contract, OriginalVerified, SourceToViewerClockContract;
        public int components, rgbaPixels, submissionSerial, unityFrame;
        public long selectedSerial;
    }
    static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

    public static int Exercise(LiveTimelineControl control, Camera background, RenderTexture output,
        OriginalCompositeProducerMath.ConfirmedProfile profile,
        OriginalLiveTimelineRegistration.TrackBinding[] bindings)
    {
        var args = Environment.GetCommandLineArgs();
        int option = Array.IndexOf(args, "-observedClockConsumption");
        if (option < 0) return 0;
        if (option + 1 >= args.Length) throw new ArgumentException("An external clock observation is required.");
        string path = Path.GetFullPath(args[option + 1]);
        string repo = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (path == repo || path.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            new FileInfo(path).Length > 65536) throw new ArgumentException("Bounded external observation required.");
        var input = JsonUtility.FromJson<Input>(File.ReadAllText(path));
        if (input == null || input.schema != "ExplicitConsumedClock-v1" || input.state == null || input.state.Length != 4 ||
            input.parameters == null || input.parameters.Length != 5 || input.expectedBits == null || input.expectedBits.Length != 8 ||
            (input.smoothing != "Enabled" && input.smoothing != "Disabled") || input.currentFrame < 30 || input.currentFrame > 600000 ||
            input.previousFrame < 0 || input.previousFrame > 600000)
            throw new ArgumentException("Complete typed clock observation required.");
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
        var s = input.state; var p = input.parameters;
        Check(OriginalLiveClockMath.TryEvaluate(input.sourceTime, new OriginalLiveClockMath.State(s[0], s[1], s[2], s[3]),
            new OriginalLiveClockMath.Parameters(input.smoothing == "Enabled", p[0], p[1], p[2], p[3], p[4]), out var evaluated),
            "Observed consumed source and explicit state must evaluate.");
        var computed = new[] { evaluated.SourceTime, evaluated.Next.SmoothedTime, evaluated.Next.Correction,
            evaluated.Next.PreviousSourceTime, evaluated.Next.CurrentTime, evaluated.PreviousTime,
            evaluated.DeltaTime, evaluated.Next.CurrentTime };
        for (int i = 0; i < computed.Length; i++)
            Check(Bits(computed[i]) == input.expectedBits[i], "Observed clock component must match float32 bits.");
        Check((int)Math.Floor((float)(evaluated.Next.CurrentTime * 60f)) == input.currentFrame &&
            (int)Math.Floor((float)(evaluated.PreviousTime * 60f)) == input.previousFrame,
            "Observed owner frame conversions must agree.");

        // Only the unsaved synthetic worksheet owned by the surrounding fixture.
        var keys = control.data.worksheetList[0].multiCameraPosKeys[0].keys.thisList;
        keys.Clear();
        keys.Add(new LiveTimelineKeyMultiCameraPositionData { frame = input.currentFrame - 30,
            enableMultiCamera = true, fadeTime = 1, position = new Vector3(0, 0, -10),
            nearClip = .1f, farClip = 50, fov = 40,
            maskType = (LiveTimelineKeyMultiCameraPositionData.MaskType)3,
            maskOffset = Vector2.zero, lineThickness = .1f, LineColor = Color.white });
        var observedBindings = new[] { OriginalLiveTimelineRegistration.TrackBinding.FromTrack(0,
            control.data.worksheetList[0].multiCameraPosKeys[0]) };
        using (var provider = new OriginalLiveContinuousProvider(control, profile, observedBindings, _ =>
            new OriginalLiveDrawInputResolver.Inputs(default, OriginalLiveContinuousRenderer.FullViewportMapping(output.height))))
        using (var preview = new OriginalLiveContinuousRenderer(provider, 0, null, _ => background,
            Shader.Find(OriginalMultiCameraRenderer.ShaderName), Shader.Find(OriginalMaskCoverageGenerator.ShaderName), output))
        {
            OriginalLiveTimelineBridge.Frame traced = null;
            int submission = 0;
            preview.TraceEmitted += trace =>
            {
                if (trace.Submission.Step == OriginalMultiCameraRenderer.Stage.FinalCompositeDrawn)
                { traced = trace.Selected; submission = trace.Submission.FrameSerial; }
            };
            void Tick(float time) { control.AlterUpdate(time); control.AlterLateUpdate(); }
            Tick(evaluated.PreviousTime); Tick(evaluated.Next.CurrentTime);
            var selected = provider.LastFrame;
            var clock = selected.Selections[0].Selection.ComparisonClock;
            Check(clock.HasValue && Bits(clock.Value.CurrentOwnerTime) == Bits(evaluated.Next.CurrentTime) &&
                Bits(clock.Value.ComparisonOwnerTime) == Bits(evaluated.PreviousTime), "Actual Control must consume the observed owner pair.");
            Check(preview.LastException == null && preview.LastStatus == "RenderedContinuousViewerCameras" &&
                ReferenceEquals(traced, selected.Selected) && submission > 0, "Final GPU submission must belong to this selected frame.");
            var actual = Read(output);
            Check(actual.Any(pixel => pixel.r > 0), "Synthetic active mask must visibly contribute.");
            int firstSubmission = submission;
            Tick(evaluated.PreviousTime); Tick(evaluated.Next.CurrentTime);
            Check(submission > firstSubmission && ReferenceEquals(traced, provider.LastFrame.Selected), "Replay must be a new current submission.");
            var replay = Read(output);
            Check(actual.SequenceEqual(replay), "Same explicit owner input must replay every RGBA pixel.");
            int outArg = Array.IndexOf(args, "-integrationOutput");
            string pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ? "urp" : "builtin";
            File.WriteAllText(Path.Combine(args[outArg + 1], "observed-clock-" + pipeline + ".private.json"),
                JsonUtility.ToJson(new Receipt { components = 8, rgbaPixels = actual.Length,
                    selectedSerial = selected.Selected.Serial, submissionSerial = firstSubmission, unityFrame = Time.frameCount }, true));
        }
        return checks;
    }

    static Color32[] Read(RenderTexture target)
    {
        var active = RenderTexture.active;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
        try
        {
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            return pixels.GetPixels32();
        }
        finally { RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(pixels); }
    }
}
