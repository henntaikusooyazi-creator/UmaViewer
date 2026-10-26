using System;
using Gallop.Live;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Explicit Viewer audio source -> Director -> actual Control owner update.
    // This registration does not establish an original-game audio epoch.
    public sealed class OriginalLiveAudioClock : IDisposable
    {
        public readonly struct Sample
        {
            public readonly AudioSource Source;
            public readonly AudioClip Clip;
            public readonly float SourceTime;
            public readonly int SampledUnityFrame, AudioSamples;
            internal Sample(Pending sample)
            { Source = sample.Source; Clip = sample.Clip; SourceTime = sample.Time;
              SampledUnityFrame = sample.UnityFrame; AudioSamples = sample.Samples; }
        }
        public readonly struct Observation
        {
            public readonly OriginalLiveTimelineBridge.Frame Selected;
            public readonly AudioSource Source;
            public readonly AudioClip Clip;
            public readonly int SampledUnityFrame, AudioSamples;
            public readonly float SourceTime, OwnerTime;
            internal Observation(OriginalLiveTimelineBridge.Frame selected, Pending sample)
            { Selected = selected; Source = sample.Source; Clip = sample.Clip; SourceTime = sample.Time; OwnerTime = sample.OwnerTime;
              SampledUnityFrame = sample.UnityFrame; AudioSamples = sample.Samples; }
        }
        internal readonly struct Pending
        {
            internal readonly AudioSource Source;
            internal readonly AudioClip Clip;
            internal readonly float Time, OwnerTime;
            internal readonly int UnityFrame, Samples;
            internal Pending(AudioSource source, AudioClip clip, float time, int samples)
            { Source = source; Clip = clip; Time = OwnerTime = time; Samples = samples; UnityFrame = UnityEngine.Time.frameCount; }
            internal Pending(Pending sample, float ownerTime)
            { Source = sample.Source; Clip = sample.Clip; Time = sample.Time; OwnerTime = ownerTime;
              Samples = sample.Samples; UnityFrame = sample.UnityFrame; }
        }
        readonly Director director;
        readonly OriginalLiveContinuousProvider provider;
        readonly Func<AudioSource> source;
        readonly Func<Sample, float?> ownerTime;
        readonly Func<float?> read;
        Pending? pending;
        bool disposed;
        public long ReadCount { get; private set; }
        public long JoinedFrames { get; private set; }
        public long HeldReads { get; private set; }
        public long UnobservedFrames { get; private set; }
        public long RejectedFrames { get; private set; }
        public Observation? LastObservation { get; private set; }

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
            { HeldReads++; return null; }
            float time = current.time;
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0)
            { HeldReads++; return null; }
            // timeSamples is observed metadata; it is never converted into time.
            var sample = new Pending(current, clip, time, current.timeSamples);
            if (ownerTime != null) return Resolve(sample);
            pending = sample; ReadCount++;
            return time;
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
            if (disposed) return; disposed = true; pending = null; LastObservation = null;
            provider.FrameReady -= OnFrame;
            if (director && ReferenceEquals(director.OriginalLiveTimeProvider, read)) director.OriginalLiveTimeProvider = null;
        }
    }
}
