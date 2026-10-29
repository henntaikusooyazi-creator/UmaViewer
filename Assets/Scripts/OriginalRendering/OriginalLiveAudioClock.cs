using System;
using Gallop.Live;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Explicit Viewer audio source -> Director -> actual Control owner update.
    // This registration does not establish an original-game audio epoch.
    public sealed class OriginalLiveAudioClock : IDisposable
    {
        // Main-thread diagnostic event after an actual staged source read.
        public event Action<Sample> SourceCaptured;
        public long SourceCaptureObserverFailures { get; private set; }
        public readonly struct Sample
        {
            public readonly AudioSource Source;
            public readonly AudioClip Clip;
            public readonly float SourceTime;
            public readonly int SampledUnityFrame, AudioSamples;
            public readonly int SourceReadUnityFrame, SourceReadStage;
            public readonly long SourceReadSerial;
            public readonly long ConsumeBeginTicks, ConsumeEndTicks;
            public readonly long ReadBeginTicks, ReadEndTicks;
            public readonly bool HasDspTime;
            public readonly double ReadBeginDspTime, ReadEndDspTime;
            public readonly string ClipName;
            public readonly int ClipSampleCount, ClipFrequency, ClipChannels;
            internal Sample(Pending sample)
            { Source = sample.Source; Clip = sample.Clip; SourceTime = sample.Time;
              SampledUnityFrame = sample.UnityFrame; AudioSamples = sample.Samples;
              SourceReadUnityFrame = sample.SourceReadUnityFrame; SourceReadStage = sample.SourceReadStage; SourceReadSerial = sample.SourceReadSerial;
              ConsumeBeginTicks = sample.ConsumeBeginTicks; ConsumeEndTicks = sample.ConsumeEndTicks;
              ReadBeginTicks = sample.ReadBeginTicks; ReadEndTicks = sample.ReadEndTicks;
              HasDspTime = sample.HasDspTime; ReadBeginDspTime = sample.ReadBeginDspTime; ReadEndDspTime = sample.ReadEndDspTime;
              ClipName = sample.ClipName; ClipSampleCount = sample.ClipSampleCount;
              ClipFrequency = sample.ClipFrequency; ClipChannels = sample.ClipChannels; }
        }
        public readonly struct Observation
        {
            public readonly OriginalLiveTimelineBridge.Frame Selected;
            public readonly AudioSource Source;
            public readonly AudioClip Clip;
            public readonly int SampledUnityFrame, AudioSamples;
            public readonly int SourceReadUnityFrame, SourceReadStage;
            public readonly long SourceReadSerial;
            public readonly long ConsumeBeginTicks, ConsumeEndTicks;
            public readonly long ReadBeginTicks, ReadEndTicks;
            public readonly bool HasDspTime;
            public readonly double ReadBeginDspTime, ReadEndDspTime;
            public readonly float SourceTime, OwnerTime;
            public readonly string ClipName;
            public readonly int ClipSampleCount, ClipFrequency, ClipChannels;
            internal Observation(OriginalLiveTimelineBridge.Frame selected, Pending sample)
            { Selected = selected; Source = sample.Source; Clip = sample.Clip; SourceTime = sample.Time; OwnerTime = sample.OwnerTime;
              SampledUnityFrame = sample.UnityFrame; AudioSamples = sample.Samples;
              SourceReadUnityFrame = sample.SourceReadUnityFrame; SourceReadStage = sample.SourceReadStage; SourceReadSerial = sample.SourceReadSerial;
              ConsumeBeginTicks = sample.ConsumeBeginTicks; ConsumeEndTicks = sample.ConsumeEndTicks;
              ReadBeginTicks = sample.ReadBeginTicks; ReadEndTicks = sample.ReadEndTicks;
              HasDspTime = sample.HasDspTime; ReadBeginDspTime = sample.ReadBeginDspTime; ReadEndDspTime = sample.ReadEndDspTime;
              ClipName = sample.ClipName; ClipSampleCount = sample.ClipSampleCount;
              ClipFrequency = sample.ClipFrequency; ClipChannels = sample.ClipChannels; }
        }
        internal readonly struct Pending
        {
            internal readonly AudioSource Source;
            internal readonly AudioClip Clip;
            internal readonly float Time, OwnerTime;
            internal readonly int UnityFrame, Samples;
            internal readonly int SourceReadUnityFrame, SourceReadStage;
            internal readonly long SourceReadSerial;
            internal readonly long ConsumeBeginTicks, ConsumeEndTicks;
            internal readonly long ReadBeginTicks, ReadEndTicks;
            internal readonly bool HasDspTime;
            internal readonly double ReadBeginDspTime, ReadEndDspTime;
            internal readonly string ClipName;
            internal readonly int ClipSampleCount, ClipFrequency, ClipChannels;
            // Preserve existing explicit fixture construction. Zero means that
            // no runtime timestamp bracket was measured for this sample.
            internal Pending(AudioSource source, AudioClip clip, float time, int samples)
                : this(source, clip, time, samples, 0, 0) { }
            internal Pending(AudioSource source, AudioClip clip, float time, int samples, long beginTicks, long endTicks)
                : this(source, clip, time, samples, beginTicks, endTicks, false, 0, 0) { }
            internal Pending(AudioSource source, AudioClip clip, float time, int samples, long beginTicks, long endTicks,
                bool hasDspTime, double beginDspTime, double endDspTime)
                : this(source, clip, time, samples, beginTicks, endTicks, hasDspTime, beginDspTime, endDspTime, 0, 0) { }
            internal Pending(AudioSource source, AudioClip clip, float time, int samples, long beginTicks, long endTicks,
                bool hasDspTime, double beginDspTime, double endDspTime, int sourceStage, long sourceSerial)
            { Source = source; Clip = clip; Time = OwnerTime = time; Samples = samples; UnityFrame = UnityEngine.Time.frameCount;
              SourceReadUnityFrame = UnityFrame; SourceReadStage = sourceStage; SourceReadSerial = sourceSerial;
              ConsumeBeginTicks = ConsumeEndTicks = 0;
              ReadBeginTicks = beginTicks; ReadEndTicks = endTicks;
              HasDspTime = hasDspTime; ReadBeginDspTime = beginDspTime; ReadEndDspTime = endDspTime;
              ClipName = clip.name; ClipSampleCount = clip.samples; ClipFrequency = clip.frequency; ClipChannels = clip.channels; }
            internal Pending(Pending sample, float ownerTime)
            { Source = sample.Source; Clip = sample.Clip; Time = sample.Time; OwnerTime = ownerTime;
              Samples = sample.Samples; UnityFrame = sample.UnityFrame;
              SourceReadUnityFrame = sample.SourceReadUnityFrame; SourceReadStage = sample.SourceReadStage; SourceReadSerial = sample.SourceReadSerial;
              ConsumeBeginTicks = sample.ConsumeBeginTicks; ConsumeEndTicks = sample.ConsumeEndTicks;
              ReadBeginTicks = sample.ReadBeginTicks; ReadEndTicks = sample.ReadEndTicks;
              HasDspTime = sample.HasDspTime; ReadBeginDspTime = sample.ReadBeginDspTime; ReadEndDspTime = sample.ReadEndDspTime;
              ClipName = sample.ClipName; ClipSampleCount = sample.ClipSampleCount;
              ClipFrequency = sample.ClipFrequency; ClipChannels = sample.ClipChannels; }
            internal Pending(Pending captured) : this(captured, captured.OwnerTime)
            { UnityFrame = UnityEngine.Time.frameCount; }
            internal Pending(Pending sample, long begin, long end) : this(sample, sample.OwnerTime)
            { ConsumeBeginTicks = begin; ConsumeEndTicks = end; }
        }
        readonly Director director;
        readonly OriginalLiveContinuousProvider provider;
        readonly Func<AudioSource> source;
        readonly Func<Sample, float?> ownerTime;
        readonly Func<float?> read;
        Pending? pending;
        bool disposed;
        bool stagedMode, stagedInvalidated, stagedBound;
        Pending? staged;
        AudioSource stagedSource;
        AudioClip stagedClip;
        Action stagedReady, stagedRefresh;
        int stagedConsumedFrame = -1;
        public long StagedSourceCaptures { get; private set; }
        public long StagedSourceConsumes { get; private set; }
        public long StagedSourceInvalidations { get; private set; }
        public long ReadCount { get; private set; }
        public long JoinedFrames { get; private set; }
        public long HeldReads { get; private set; }
        public long UnobservedFrames { get; private set; }
        public long RejectedFrames { get; private set; }
        public Observation? LastObservation { get; private set; }
        // Optional diagnostics only; these values never enter clock arithmetic.
        // A failed reader leaves the ordinary source/owner evaluation intact.
        public Func<double> DiagnosticDspTimeReader { get; set; }
        public long DiagnosticDspFailures { get; private set; }

        // Explicitly include the initial Director synchronization in this same
        // registration. Existing constructors retain the previous startup path.
        public void EnableStartupConsumption()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalLiveAudioClock));
            if (!director || !ReferenceEquals(director.OriginalLiveTimeProvider, read) ||
                (director.OriginalLiveStartupTimeProvider != null &&
                 !ReferenceEquals(director.OriginalLiveStartupTimeProvider, read)))
                throw new InvalidOperationException("Startup remains owned by its current registration.");
            director.OriginalLiveStartupTimeProvider = read;
        }

        // An explicit producer/consumer policy, carrying actual API samples.
        // Stage 1=before Play, 2=after owner, 3=unprimed bootstrap capture.
        public void EnableStagedSourceConsumption()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalLiveAudioClock));
            if (stagedMode)
            {
                if (stagedInvalidated || !director || !ReferenceEquals(director.OriginalLiveAudioReady, stagedReady) ||
                    !ReferenceEquals(director.OriginalLiveAfterOwnerUpdate, stagedRefresh))
                    throw new InvalidOperationException("The staged source lifetime has ended.");
                EnableStartupConsumption(); return;
            }
            if (!director || director.OriginalLiveAudioReady != null || director.OriginalLiveAfterOwnerUpdate != null)
                throw new InvalidOperationException("Source capture callbacks remain owned by their registration.");
            EnableStartupConsumption();
            stagedReady = () => CaptureStagedSource(1); stagedRefresh = RefreshStagedSource; stagedMode = true;
            director.OriginalLiveAudioReady = stagedReady; director.OriginalLiveAfterOwnerUpdate = stagedRefresh;
        }
        void InvalidateStagedSource()
        { staged = null; if (!stagedInvalidated) { stagedInvalidated = true; StagedSourceInvalidations++; } }
        bool SameStagedRegistration() => !disposed && director && ReferenceEquals(director.OriginalLiveTimeProvider, read) &&
            ReferenceEquals(director.OriginalLiveAudioReady, stagedReady) && ReferenceEquals(director.OriginalLiveAfterOwnerUpdate, stagedRefresh);
        bool CaptureStagedSource(int stage)
        {
            if (disposed) return false;
            if (stagedInvalidated || !SameStagedRegistration()) { InvalidateStagedSource(); return false; }
            if (staged.HasValue) { InvalidateStagedSource(); return false; }
            var current = source(); var clip = current ? current.clip : null;
            if (stagedBound && (!ReferenceEquals(current, stagedSource) || !ReferenceEquals(clip, stagedClip)))
            { InvalidateStagedSource(); return false; }
            if (!current || !clip || clip.loadState != AudioDataLoadState.Loaded || (stage == 1 ? current.isPlaying : !current.isPlaying))
            { staged = null; return false; }
            if (stage == 1 && StagedSourceCaptures != 0) { InvalidateStagedSource(); return false; }
            var capture = CaptureSource(current, clip, stage, StagedSourceCaptures + 1);
            if (!capture.HasValue) { staged = null; return false; }
            if (!SameStagedRegistration() || !ReferenceEquals(current.clip, clip)) { InvalidateStagedSource(); return false; }
            stagedSource = current; stagedClip = clip; stagedBound = true; staged = capture; StagedSourceCaptures++;
            try { SourceCaptured?.Invoke(new Sample(capture.Value)); } catch { SourceCaptureObserverFailures++; }
            return true;
        }
        void RefreshStagedSource()
        {
            if (disposed) return;
            if (stagedConsumedFrame != Time.frameCount) { InvalidateStagedSource(); return; }
            CaptureStagedSource(2);
        }
        float? ReadStagedSource(AudioSource current, AudioClip clip)
        {
            if (stagedInvalidated || !SameStagedRegistration() || (stagedBound &&
                (!ReferenceEquals(current, stagedSource) || !ReferenceEquals(clip, stagedClip))))
            { InvalidateStagedSource(); HeldReads++; return null; }
            if (!staged.HasValue) { CaptureStagedSource(3); HeldReads++; return null; }
            var captured = staged.Value; staged = null;
            long begin = System.Diagnostics.Stopwatch.GetTimestamp();
            var accepted = Publish(new Pending(new Pending(captured), begin, 0));
            if (accepted.HasValue && !SameStagedRegistration())
            { pending = null; InvalidateStagedSource(); HeldReads++; return null; }
            if (accepted.HasValue)
            { pending = new Pending(pending.Value, begin, System.Diagnostics.Stopwatch.GetTimestamp()); stagedConsumedFrame = Time.frameCount; StagedSourceConsumes++; }
            return accepted;
        }

        public OriginalLiveAudioClock(Director director, OriginalLiveContinuousProvider provider, Func<AudioSource> source)
            : this(director, provider, source, null) { }

        // Optional evaluation keeps raw source time distinct from the owner time.
        // The caller supplies and owns any smoothing state and its lifetime.
        public OriginalLiveAudioClock(Director director, OriginalLiveContinuousProvider provider, Func<AudioSource> source,
            Func<Sample, float?> ownerTime)
        {
            if (!director || provider == null || source == null || !director._liveTimelineControl ||
                !ReferenceEquals(director._liveTimelineControl.OriginalCompositeBridge, provider.Bridge))
                throw new ArgumentException("The registered Director, provider and explicit audio resolver must belong together.");
            if (director.OriginalLiveTimeProvider != null)
                throw new InvalidOperationException("An existing time provider remains owned by its registration.");
            this.director = director; this.provider = provider; this.source = source; this.ownerTime = ownerTime;
            read = Read; director.OriginalLiveTimeProvider = read; provider.FrameReady += OnFrame;
        }
        float? Read()
        {
            pending = null;
            if (disposed) return null;
            var current = source(); var clip = current ? current.clip : null;
            if (!current || !clip || !current.isPlaying || clip.loadState != AudioDataLoadState.Loaded)
            {
                if (stagedMode && stagedBound)
                {
                    if (current && clip && ReferenceEquals(current, stagedSource) && ReferenceEquals(clip, stagedClip) && !current.isPlaying)
                    { staged = null; stagedInvalidated = true; }
                    else InvalidateStagedSource();
                }
                HeldReads++; return null;
            }
            if (stagedMode) return ReadStagedSource(current, clip);
            var captured = CaptureSource(current, clip, 0, 0);
            if (!captured.HasValue) { HeldReads++; return null; }
            return Publish(captured.Value);
        }
        Pending? CaptureSource(AudioSource current, AudioClip clip, int stage, long serial)
        {
            long beginTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            var dspReader = DiagnosticDspTimeReader;
            double beginDspTime = 0, endDspTime = 0;
            bool dspValid = TryReadDsp(dspReader, out beginDspTime);
            float time = current.time;
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0)
                return null;
            // timeSamples is observed metadata; it is never converted into time.
            int audioSamples = current.timeSamples;
            bool dspEndValid = TryReadDsp(dspReader, out endDspTime);
            dspValid = dspValid && dspEndValid && endDspTime >= beginDspTime;
            if (dspReader != null && !dspValid) DiagnosticDspFailures++;
            long endTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            return new Pending(current, clip, time, audioSamples, beginTicks, endTicks,
                dspValid, dspValid ? beginDspTime : 0, dspValid ? endDspTime : 0, stage, serial);
        }
        float? Publish(Pending sample)
        {
            if (ownerTime != null) return Resolve(sample);
            pending = sample; ReadCount++;
            return sample.Time;
        }
        static bool TryReadDsp(Func<double> reader, out double value)
        {
            value = 0;
            if (reader == null) return false;
            try { value = reader(); return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0; }
            catch { return false; }
        }
        float? Resolve(Pending sample)
        {
            pending = null;
            if (disposed) return null;
            float? evaluated = ownerTime(new Sample(sample));
            if (disposed) return null;
            if (!evaluated.HasValue || float.IsNaN(evaluated.Value) || float.IsInfinity(evaluated.Value) || evaluated.Value < 0 ||
                sample.UnityFrame != Time.frameCount || !sample.Source || !sample.Clip || sample.Source.clip != sample.Clip)
            { HeldReads++; return null; }
            pending = new Pending(sample, evaluated.Value); ReadCount++;
            return evaluated;
        }
        static bool SameBits(float a, float b) => BitConverter.ToInt32(BitConverter.GetBytes(a), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b), 0);
        void OnFrame(OriginalLiveContinuousProvider.Frame frame)
        {
            var sample = pending; pending = null; LastObservation = null;
            if (!sample.HasValue) { UnobservedFrames++; return; }
            var p = sample.Value;
            if (p.UnityFrame != Time.frameCount || !p.Source || p.Source.clip != p.Clip || frame.Selected.Selections.Count == 0)
            { RejectedFrames++; return; }
            foreach (var selected in frame.Selected.Selections)
                if (!selected.ComparisonClock.HasValue || !SameBits(selected.ComparisonClock.Value.CurrentOwnerTime, p.OwnerTime))
                { RejectedFrames++; return; }
            LastObservation = new Observation(frame.Selected, p); JoinedFrames++;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; pending = staged = null; LastObservation = null; DiagnosticDspTimeReader = null; SourceCaptured = null;
            provider.FrameReady -= OnFrame;
            if (director && ReferenceEquals(director.OriginalLiveAudioReady, stagedReady)) director.OriginalLiveAudioReady = null;
            if (director && ReferenceEquals(director.OriginalLiveAfterOwnerUpdate, stagedRefresh)) director.OriginalLiveAfterOwnerUpdate = null;
            if (director && ReferenceEquals(director.OriginalLiveStartupTimeProvider, read)) director.OriginalLiveStartupTimeProvider = null;
            if (director && ReferenceEquals(director.OriginalLiveTimeProvider, read)) director.OriginalLiveTimeProvider = null;
        }
    }
}
