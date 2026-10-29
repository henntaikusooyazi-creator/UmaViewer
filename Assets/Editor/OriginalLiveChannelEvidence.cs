using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Gallop.Live.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;
using R = Gallop.Live.Rendering.OriginalMultiCameraRenderer;

// One bounded frame of Viewer-owned submission evidence. No original-game hook,
// camera identity inferred from image content, or game asset extraction.
public sealed class OriginalLiveChannelEvidence : IDisposable
{
    [Serializable] public sealed class TextureInfo
    {
        public int width, height, depthBufferBits, msaaSamples, volumeDepth, mipCount;
        public string format, graphicsFormat, dimension;
        public bool sRGB, useMipMap, autoGenerateMips, enableRandomWrite, bindMS, useDynamicScale;
        public TextureInfo(RenderTexture texture)
        {
            var d = texture.descriptor;
            width = d.width; height = d.height; depthBufferBits = d.depthBufferBits;
            msaaSamples = d.msaaSamples; volumeDepth = d.volumeDepth; mipCount = d.mipCount;
            format = texture.format.ToString(); graphicsFormat = d.graphicsFormat.ToString(); dimension = d.dimension.ToString();
            sRGB = texture.sRGB; useMipMap = d.useMipMap; autoGenerateMips = d.autoGenerateMips;
            enableRandomWrite = d.enableRandomWrite; bindMS = d.bindMS; useDynamicScale = d.useDynamicScale;
        }
    }
    [Serializable] public sealed class Capture
    {
        public string stage, png, pngSha256;
        public long selectedSerial;
        public int submissionSerial, inputVersion, sourceId, copyId;
        public bool sRGBWriteAtCopy;
        public TextureInfo source, copy;
        public string readbackFormat = "RGBA32";
        public bool readbackLinear = true;
    }
    [Serializable] public sealed class Result
    {
        public bool complete, exactReplay, distinctCameras, sameOwnerPair;
        public bool captured, verificationDeferred;
        public long captureCompletedTicks, verificationBeginTicks, verificationEndTicks, imageWriteBeginTicks, imageWriteEndTicks;
        public bool exactRightAccumulationReplay, exactLeftAccumulationReplay;
        public long selectedSerial;
        public int submissionSerial, sharedInputId, baseCameraId, rightCameraId, leftCameraId;
        public int rightTrack, leftTrack, rightComposite, leftComposite, rightKey, leftKey;
        public int replayDifferentPixels, rightContributionPixels, leftContributionPixels, baseContributionPixels;
        public int rightAccumulationDifferentPixels = -1, leftAccumulationDifferentPixels = -1;
        public int rightOnlyCoveragePixels, leftOnlyCoveragePixels, baseOnlyCoveragePixels, width, height;
        public float currentOwnerTime, comparisonOwnerTime;
        public string failure, activeColorSpace;
        public List<int> stages = new List<int>();
        public List<Capture> captures = new List<Capture>();
        public bool OriginalVerified = false, runtime_contract = false;
    }
    readonly OriginalLiveContinuousRenderer preview;
    readonly int rightTrack, leftTrack;
    readonly Func<OriginalLiveTimelineBridge.Frame, Camera> expectedBase;
    readonly List<RenderTexture> targets = new List<RenderTexture>();
    readonly List<RenderTexture> capturedTargets = new List<RenderTexture>();
    readonly OriginalMultiCameraRenderer replay;
    readonly bool deferVerification;
    OriginalLiveTimelineBridge.Frame selected;
    RenderTexture right, left, rightAccumulation, leftAccumulation, background, final, reference;
    bool disposed;
    public Result Evidence { get; } = new Result();

    public OriginalLiveChannelEvidence(OriginalLiveContinuousRenderer preview, int rightTrack, int leftTrack,
        Func<OriginalLiveTimelineBridge.Frame, Camera> expectedBase)
        : this(preview, rightTrack, leftTrack, expectedBase, false) { }

    // Copies still occur at the exact draw stage. Only GPU replay/readback and
    // CPU pixel checks may be postponed until the caller explicitly completes.
    public OriginalLiveChannelEvidence(OriginalLiveContinuousRenderer preview, int rightTrack, int leftTrack,
        Func<OriginalLiveTimelineBridge.Frame, Camera> expectedBase, bool deferVerification)
    {
        if (preview == null || expectedBase == null || rightTrack < 0 || leftTrack < 0 || rightTrack == leftTrack)
            throw new ArgumentException("Explicit distinct channels and expected base resolver required.");
        this.preview = preview; this.rightTrack = rightTrack; this.leftTrack = leftTrack; this.expectedBase = expectedBase;
        this.deferVerification = deferVerification; Evidence.verificationDeferred = deferVerification;
        replay = new OriginalMultiCameraRenderer(Shader.Find(R.ShaderName), RenderTextureFormat.ARGBFloat);
        preview.TraceEmitted += Observe;
    }
    void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    RenderTexture Target(RenderTexture source)
    {
        Require(source && source.IsCreated() && source.width * (long)source.height <= 262144, "Bounded created Viewer target required.");
        var target = new RenderTexture(source.descriptor) { name = "Explicit channel evidence", filterMode = FilterMode.Point };
        Require(target.Create(), "Evidence target creation failed."); targets.Add(target); return target;
    }
    void Observe(OriginalLiveContinuousRenderer.Trace trace)
    {
        if (disposed || Evidence.captured || Evidence.complete || Evidence.failure != null) return;
        try
        {
            var step = trace.Submission;
            if (selected == null)
            {
                Require(step.Step == R.Stage.Cleared && trace.TransparentChannels == 0, "Capture begins at an active pair's cleared submission.");
                selected = trace.Selected;
                bool hasRight = selected.TryGetTrack(rightTrack, out var r);
                bool hasLeft = selected.TryGetTrack(leftTrack, out var l);
                Require(hasRight && hasLeft, "Both natural selections required.");
                Require(r.KeyEnabled && l.KeyEnabled && r.ComparisonClock.HasValue && l.ComparisonClock.HasValue, "Active same-owner selections required.");
                var rc = r.ComparisonClock.Value; var lc = l.ComparisonClock.Value;
                Evidence.sameOwnerPair = rc.CurrentOwnerTime == lc.CurrentOwnerTime && rc.ComparisonOwnerTime == lc.ComparisonOwnerTime &&
                    rc.Current == lc.Current && rc.Comparison == lc.Comparison;
                Evidence.distinctCameras = trace.BaseCamera && trace.RightCamera && trace.LeftCamera &&
                    trace.BaseCamera != trace.RightCamera && trace.BaseCamera != trace.LeftCamera && trace.RightCamera != trace.LeftCamera &&
                    ReferenceEquals(trace.RightCamera, r.ViewerCamera) && ReferenceEquals(trace.LeftCamera, l.ViewerCamera) &&
                    ReferenceEquals(trace.BaseCamera, expectedBase(selected));
                Require(Evidence.sameOwnerPair && Evidence.distinctCameras, "Explicit camera roles and common owner pair must match.");
                Evidence.selectedSerial = selected.Serial; Evidence.submissionSerial = step.FrameSerial; Evidence.sharedInputId = step.SharedInput.GetInstanceID();
                Evidence.baseCameraId = trace.BaseCamera.GetInstanceID(); Evidence.rightCameraId = trace.RightCamera.GetInstanceID(); Evidence.leftCameraId = trace.LeftCamera.GetInstanceID();
                Evidence.rightTrack = rightTrack; Evidence.leftTrack = leftTrack;
                Evidence.rightComposite = r.Cue.Source.CompositeIndex.Value; Evidence.leftComposite = l.Cue.Source.CompositeIndex.Value;
                Evidence.rightKey = r.CurrentKeyFrame; Evidence.leftKey = l.CurrentKeyFrame;
                Evidence.currentOwnerTime = rc.CurrentOwnerTime; Evidence.comparisonOwnerTime = rc.ComparisonOwnerTime;
                Evidence.width = step.Output.width; Evidence.height = step.Output.height;
                Evidence.activeColorSpace = QualitySettings.activeColorSpace.ToString();
                right = Target(step.SharedInput); left = Target(step.SharedInput);
                rightAccumulation = Target(step.Accumulation); leftAccumulation = Target(step.Accumulation);
                background = Target(step.Output); final = Target(step.Output); reference = Target(step.Output);
            }
            Require(ReferenceEquals(selected, trace.Selected) && step.FrameSerial == Evidence.submissionSerial &&
                step.SharedInput.GetInstanceID() == Evidence.sharedInputId && (int)step.Step == Evidence.stages.Count,
                "All stages must belong to the same selected update and shared input.");
            Evidence.stages.Add((int)step.Step);
            if (step.Step == R.Stage.RightInputUpdated) Copy(step, step.SharedInput, right, 1, "right-coverage.private.png");
            if (step.Step == R.Stage.RightMaskDrawn) Copy(step, step.Accumulation, rightAccumulation, 1, "right-accumulation.private.png");
            if (step.Step == R.Stage.LeftInputUpdated) Copy(step, step.SharedInput, left, 2, "left-coverage.private.png");
            if (step.Step == R.Stage.LeftMaskDrawn) Copy(step, step.Accumulation, leftAccumulation, 2, "left-accumulation.private.png");
            if (step.Step == R.Stage.BasePrepared) Copy(step, step.Output, background, 2, "base.private.png");
            if (step.Step == R.Stage.FinalCompositeDrawn)
            {
                Copy(step, step.Output, final, 2, "two-channel.private.png");
                Evidence.captured = true; Evidence.captureCompletedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                preview.TraceEmitted -= Observe;
                if (!deferVerification) CompleteDeferredVerification();
            }
        }
        catch (Exception e) { Evidence.failure = e.ToString(); preview.TraceEmitted -= Observe; throw; }
    }
    void Copy(R.Trace step, RenderTexture source, RenderTexture target, int version, string png)
    {
        Require(step.InputVersion == version && source != target && (int)step.Step == Evidence.captures.Count + 1,
            "Independent copies must be submitted at their exact stage and input version.");
        Graphics.CopyTexture(source, target);
        Evidence.captures.Add(new Capture { stage = step.Step.ToString(), png = png,
            selectedSerial = Evidence.selectedSerial, submissionSerial = step.FrameSerial, inputVersion = step.InputVersion,
            sourceId = source.GetInstanceID(), copyId = target.GetInstanceID(), sRGBWriteAtCopy = GL.sRGBWrite,
            source = new TextureInfo(source), copy = new TextureInfo(target) });
        capturedTargets.Add(target);
    }
    static Color32[] Read(RenderTexture source)
    {
        var active = RenderTexture.active; var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        try { RenderTexture.active = source; image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply(); return image.GetPixels32(); }
        finally { RenderTexture.active = active; Destroy(image); }
    }
    static Color[] ReadFloat(RenderTexture source)
    {
        var active = RenderTexture.active; var image = new Texture2D(source.width, source.height, TextureFormat.RGBAFloat, false, true);
        try { RenderTexture.active = source; image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply(); return image.GetPixels(); }
        finally { RenderTexture.active = active; Destroy(image); }
    }
    static int Differences(Color32[] a, Color32[] b) { int n = 0; for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) n++; return n; }
    static int VisibleDifferences(Color32[] a, Color32[] b)
    {
        int n = 0;
        for (int i = 0; i < a.Length; i++)
            if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) n++;
        return n;
    }
    void Verify()
    {
        var r = new R.Submission(right, R.InputEncoding.StraightColorWithCoverage);
        var l = new R.Submission(left, R.InputEncoding.StraightColorWithCoverage);
        var none = new R.Submission(Texture2D.blackTexture, R.InputEncoding.StraightColorWithCoverage, 0);
        var actual = Read(final);
        replay.Render(background, r, l, reference, step => {
            if (step.Step == R.Stage.RightMaskDrawn)
                Evidence.rightAccumulationDifferentPixels = Differences(Read(rightAccumulation), Read(step.Accumulation));
            if (step.Step == R.Stage.LeftMaskDrawn)
                Evidence.leftAccumulationDifferentPixels = Differences(Read(leftAccumulation), Read(step.Accumulation));
        });
        Evidence.exactRightAccumulationReplay = Evidence.rightAccumulationDifferentPixels == 0;
        Evidence.exactLeftAccumulationReplay = Evidence.leftAccumulationDifferentPixels == 0;
        Evidence.replayDifferentPixels = Differences(actual, Read(reference)); Evidence.exactReplay = Evidence.replayDifferentPixels == 0;
        // Visible role claims use RGB only: replacing the base can also change
        // destination alpha, which the final composite deliberately preserves.
        replay.Render(background, none, l, reference); Evidence.rightContributionPixels = VisibleDifferences(actual, Read(reference));
        replay.Render(background, r, none, reference); Evidence.leftContributionPixels = VisibleDifferences(actual, Read(reference));
        replay.Render(Texture2D.blackTexture, r, l, reference); Evidence.baseContributionPixels = VisibleDifferences(actual, Read(reference));
        var ra = ReadFloat(right); var la = ReadFloat(left);
        for (int i = 0; i < ra.Length; i++)
        {
            if (ra[i].a > 0 && la[i].a == 0) Evidence.rightOnlyCoveragePixels++;
            if (la[i].a > 0 && ra[i].a == 0) Evidence.leftOnlyCoveragePixels++;
            if (ra[i].a == 0 && la[i].a == 0) Evidence.baseOnlyCoveragePixels++;
        }
        Require(Evidence.captures.Count == 6 && Evidence.exactRightAccumulationReplay && Evidence.exactLeftAccumulationReplay &&
            Evidence.exactReplay && Evidence.rightContributionPixels > 0 && Evidence.leftContributionPixels > 0 && Evidence.baseContributionPixels > 0 &&
            Evidence.rightOnlyCoveragePixels > 0 && Evidence.leftOnlyCoveragePixels > 0 && Evidence.baseOnlyCoveragePixels > 0,
            "Exact replay and independent visible contributions from all three explicit camera roles required.");
    }
    public void CompleteDeferredVerification()
    {
        if (disposed) throw new ObjectDisposedException(nameof(OriginalLiveChannelEvidence));
        Require(Evidence.captured && Evidence.failure == null, "All immutable stage copies must be captured before verification.");
        if (Evidence.complete) return;
        Evidence.verificationBeginTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        try { Verify(); Evidence.complete = true; }
        catch (Exception e) { Evidence.failure = e.ToString(); throw; }
        finally { Evidence.verificationEndTicks = System.Diagnostics.Stopwatch.GetTimestamp(); }
    }
    public void Save(string output)
    {
        Require(Evidence.complete && Evidence.failure == null, "Completed channel evidence required.");
        Directory.CreateDirectory(output);
        bool first = Evidence.imageWriteBeginTicks == 0;
        if (first) Evidence.imageWriteBeginTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int i = 0; i < Evidence.captures.Count; i++)
            Evidence.captures[i].pngSha256 = SaveImage(capturedTargets[i], Path.Combine(output, Evidence.captures[i].png));
        if (first) Evidence.imageWriteEndTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        // Emit the receipt only after every independent image has been written.
        File.WriteAllText(Path.Combine(output, "channel-evidence.private.json"), JsonUtility.ToJson(Evidence, true));
    }
    static string SaveImage(RenderTexture source, string path)
    {
        var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        try
        {
            image.SetPixels32(Read(source)); image.Apply(); var png = image.EncodeToPNG(); File.WriteAllBytes(path, png);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
        }
        finally { Destroy(image); }
    }
    static void Destroy(Object value) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; preview.TraceEmitted -= Observe; replay.Dispose();
        foreach (var target in targets) { target.Release(); Destroy(target); }
    }
}
