using System;
using Gallop.Live;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Explicit checkpoint lifetime. Evaluation is provisional until the actual
    // Control update joins it; rejected or missing updates never advance state.
    public sealed class OriginalLiveSmoothingClock : IDisposable
    {
        readonly Director director;
        readonly OriginalLiveContinuousProvider provider;
        readonly Func<float?> registration;
        readonly Func<AudioSource> resolveSource;
        AudioSource source;
        AudioClip clip;
        OriginalLiveClockMath.Parameters parameters;
        OriginalLiveClockMath.Result? candidate;
        OriginalLiveAudioClock.Sample sampled;
        bool disposed;
        bool bound;
        bool useResolver = true;
        public OriginalLiveAudioClock Clock { get; }
        public OriginalLiveClockMath.State CommittedState { get; private set; }
        public long CommittedFrames { get; private set; }
        public long DiscardedCandidates { get; private set; }
        public long HeldEvaluations { get; private set; }

        public OriginalLiveSmoothingClock(Director director, OriginalLiveContinuousProvider provider,
            AudioSource source, OriginalLiveClockMath.State initialState, OriginalLiveClockMath.Parameters parameters)
            : this(director, provider, RequiredSource(source), initialState, parameters)
        {
            this.source = source; clip = source.clip; bound = true; useResolver = false;
        }

        // The explicit resolver may become ready after timeline registration.
        // Its first valid sample binds one lifetime; later changes require Reset.
        public OriginalLiveSmoothingClock(Director director, OriginalLiveContinuousProvider provider,
            Func<AudioSource> source, OriginalLiveClockMath.State initialState, OriginalLiveClockMath.Parameters parameters)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidateState(initialState, parameters);
            this.director = director; this.provider = provider;
            resolveSource = source; CommittedState = initialState; this.parameters = parameters;
            Clock = new OriginalLiveAudioClock(director, provider, () => useResolver ? resolveSource() : this.source, Evaluate);
            registration = director.OriginalLiveTimeProvider;
            // The audio clock first validates the selected owner. Only then may
            // this registration commit its provisional next state.
            provider.FrameReady += OnFrame;
        }

        static Func<AudioSource> RequiredSource(AudioSource source)
        {
            if (!source || !source.clip) throw new ArgumentException("A current source/clip is required.");
            return () => source;
        }
        static void ValidateState(OriginalLiveClockMath.State state, OriginalLiveClockMath.Parameters parameters)
        {
            if (!OriginalLiveClockMath.TryEvaluate(state.PreviousSourceTime, state, parameters, out _))
                throw new ArgumentException("A finite explicit clock checkpoint is required.");
        }
        void Discard()
        {
            if (candidate.HasValue) { candidate = null; DiscardedCandidates++; }
        }
        public void Reset(AudioSource currentSource, OriginalLiveClockMath.State state, OriginalLiveClockMath.Parameters currentParameters)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OriginalLiveSmoothingClock));
            RequiredSource(currentSource); ValidateState(state, currentParameters);
            Discard(); source = currentSource; clip = source.clip; bound = true; CommittedState = state; parameters = currentParameters;
        }
        float? Evaluate(OriginalLiveAudioClock.Sample sample)
        {
            Discard();
            if (!bound && !disposed && director && ReferenceEquals(director.OriginalLiveTimeProvider, registration) && sample.Source && sample.Clip)
            { source = sample.Source; clip = sample.Clip; bound = true; }
            if (disposed || !director || !ReferenceEquals(director.OriginalLiveTimeProvider, registration) ||
                !source || !clip || source.clip != clip || sample.Source != source || sample.Clip != clip ||
                sample.SampledUnityFrame != Time.frameCount ||
                !OriginalLiveClockMath.TryEvaluate(sample.SourceTime, CommittedState, parameters, out var next))
            { HeldEvaluations++; return null; }
            sampled = sample; candidate = next;
            return next.Next.CurrentTime;
        }
        static bool SameBits(float a, float b) => BitConverter.ToInt32(BitConverter.GetBytes(a), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b), 0);
        void OnFrame(OriginalLiveContinuousProvider.Frame frame)
        {
            if (!candidate.HasValue) return;
            var next = candidate.Value;
            var observed = Clock.LastObservation;
            if (disposed || !director || !ReferenceEquals(director.OriginalLiveTimeProvider, registration) ||
                !source || !clip || source.clip != clip || (useResolver && resolveSource() != source) || !observed.HasValue ||
                !ReferenceEquals(observed.Value.Selected, frame.Selected) || observed.Value.Source != source ||
                observed.Value.Clip != clip || observed.Value.SampledUnityFrame != sampled.SampledUnityFrame ||
                observed.Value.SampledUnityFrame != Time.frameCount || observed.Value.AudioSamples != sampled.AudioSamples ||
                !SameBits(observed.Value.SourceTime, sampled.SourceTime) || !SameBits(observed.Value.OwnerTime, next.Next.CurrentTime))
            { Discard(); return; }
            CommittedState = next.Next; candidate = null; CommittedFrames++;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Discard(); provider.FrameReady -= OnFrame; Clock.Dispose();
        }
    }
}
