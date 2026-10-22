using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gallop.Live.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;
using R = Gallop.Live.Rendering.OriginalMultiCameraRenderer;

// One bounded frame of Viewer-owned submission evidence. No original-game hook,
// camera identity inferred from image content, or game asset extraction.
public sealed class OriginalLiveChannelEvidence : IDisposable
{
    [Serializable] public sealed class Result
    {
        public bool complete, exactReplay, distinctCameras, sameOwnerPair;
        public long selectedSerial;
        public int submissionSerial, sharedInputId, baseCameraId, rightCameraId, leftCameraId;
        public int rightTrack, leftTrack, rightComposite, leftComposite, rightKey, leftKey;
        public int replayDifferentPixels, rightContributionPixels, leftContributionPixels, baseContributionPixels;
        public int rightOnlyCoveragePixels, leftOnlyCoveragePixels, baseOnlyCoveragePixels, width, height;
        public float currentOwnerTime, comparisonOwnerTime;
        public string failure;
        public List<int> stages = new List<int>();
        public bool OriginalVerified = false, runtime_contract = false;
    }
    readonly OriginalLiveContinuousRenderer preview;
    readonly int rightTrack, leftTrack;
    readonly Func<OriginalLiveTimelineBridge.Frame, Camera> expectedBase;
    readonly List<RenderTexture> targets = new List<RenderTexture>();
    readonly OriginalMultiCameraRenderer replay;
    OriginalLiveTimelineBridge.Frame selected;
    RenderTexture right, left, background, final, reference;
    bool disposed;
    public Result Evidence { get; } = new Result();

    public OriginalLiveChannelEvidence(OriginalLiveContinuousRenderer preview, int rightTrack, int leftTrack,
        Func<OriginalLiveTimelineBridge.Frame, Camera> expectedBase)
    {
        if (preview == null || expectedBase == null || rightTrack < 0 || leftTrack < 0 || rightTrack == leftTrack)
            throw new ArgumentException("Explicit distinct channels and expected base resolver required.");
        this.preview = preview; this.rightTrack = rightTrack; this.leftTrack = leftTrack; this.expectedBase = expectedBase;
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
        if (disposed || Evidence.complete || Evidence.failure != null) return;
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
                right = Target(step.SharedInput); left = Target(step.SharedInput);
                background = Target(step.Output); final = Target(step.Output); reference = Target(step.Output);
            }
            Require(ReferenceEquals(selected, trace.Selected) && step.FrameSerial == Evidence.submissionSerial &&
                step.SharedInput.GetInstanceID() == Evidence.sharedInputId && (int)step.Step == Evidence.stages.Count,
                "All stages must belong to the same selected update and shared input.");
            Evidence.stages.Add((int)step.Step);
            if (step.Step == R.Stage.RightInputUpdated) { Require(step.InputVersion == 1, "Right input version required."); Graphics.CopyTexture(step.SharedInput, right); }
            if (step.Step == R.Stage.LeftInputUpdated) { Require(step.InputVersion == 2, "Left input version required."); Graphics.CopyTexture(step.SharedInput, left); }
            if (step.Step == R.Stage.BasePrepared) Graphics.CopyTexture(step.Output, background);
            if (step.Step == R.Stage.FinalCompositeDrawn)
            {
                Graphics.CopyTexture(step.Output, final);
                Verify(); Evidence.complete = true; preview.TraceEmitted -= Observe;
            }
        }
        catch (Exception e) { Evidence.failure = e.ToString(); preview.TraceEmitted -= Observe; throw; }
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
        replay.Render(background, r, l, reference);
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
        Require(Evidence.exactReplay && Evidence.rightContributionPixels > 0 && Evidence.leftContributionPixels > 0 && Evidence.baseContributionPixels > 0 &&
            Evidence.rightOnlyCoveragePixels > 0 && Evidence.leftOnlyCoveragePixels > 0 && Evidence.baseOnlyCoveragePixels > 0,
            "Exact replay and independent visible contributions from all three explicit camera roles required.");
    }
    public void Save(string output)
    {
        Require(Evidence.complete && Evidence.failure == null, "Completed channel evidence required.");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "channel-evidence.private.json"), JsonUtility.ToJson(Evidence, true));
        SaveImage(final, Path.Combine(output, "two-channel.private.png"));
        SaveImage(background, Path.Combine(output, "base.private.png"));
        SaveImage(right, Path.Combine(output, "right-coverage.private.png"));
        SaveImage(left, Path.Combine(output, "left-coverage.private.png"));
    }
    static void SaveImage(RenderTexture source, string path)
    {
        var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        try { image.SetPixels32(Read(source)); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { Destroy(image); }
    }
    static void Destroy(Object value) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; preview.TraceEmitted -= Observe; replay.Dispose();
        foreach (var target in targets) { target.Release(); Destroy(target); }
    }
}
