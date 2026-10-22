using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gallop.Live.Cutt;

namespace Gallop.Live.Rendering
{
    // Explicit lifetime for a loaded timeline. No saved frame list, clock synthesis,
    // scene search, file parsing, or replacement of the Director's update loop.
    public sealed class OriginalLiveContinuousProvider : IDisposable
    {
        public readonly struct ResolvedSelection
        {
            public readonly OriginalLiveTimelineBridge.Selection Selection;
            public readonly OriginalCompositeCueAdapter.Snapshot Snapshot;
            internal ResolvedSelection(OriginalLiveTimelineBridge.Selection selection,
                OriginalCompositeCueAdapter.Snapshot snapshot)
            { Selection = selection; Snapshot = snapshot; }
        }
        public sealed class Frame
        {
            public readonly OriginalLiveTimelineBridge.Frame Selected;
            public readonly ReadOnlyCollection<ResolvedSelection> Selections;
            internal Frame(OriginalLiveTimelineBridge.Frame selected, ResolvedSelection[] selections)
            { Selected = selected; Selections = Array.AsReadOnly(selections); }
        }

        readonly LiveTimelineControl control;
        readonly OriginalLiveDrawInputResolver resolver;
        readonly HashSet<int> boundTracks = new HashSet<int>();
        bool disposed;
        public OriginalLiveTimelineBridge Bridge { get; }
        public Frame LastFrame { get; private set; }
        public long FrameCount { get; private set; }
        public event Action<Frame> FrameReady;

        public OriginalLiveContinuousProvider(LiveTimelineControl control,
            OriginalCompositeProducerMath.ConfirmedProfile profile,
            IReadOnlyList<OriginalLiveTimelineRegistration.TrackBinding> bindings,
            Func<OriginalLiveTimelineBridge.Selection, OriginalLiveDrawInputResolver.Inputs> inputs = null)
        {
            if (!control || !control.data || control.data.worksheetList == null || control.data.worksheetList.Count == 0)
                throw new ArgumentException("An already loaded timeline is required.", nameof(control));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (float.IsNaN(profile.DurationMultiplier) || float.IsInfinity(profile.DurationMultiplier) || profile.DurationMultiplier <= 0)
                throw new ArgumentException("A finite positive confirmed multiplier is required.", nameof(profile));
            if (bindings == null || bindings.Count == 0) throw new ArgumentException("Explicit track bindings are required.", nameof(bindings));
            if (control.OriginalCompositeBridge != null)
                throw new InvalidOperationException("The existing bridge remains owned by its registration.");
            var tracks = control.data.worksheetList[0].multiCameraPosKeys;
            foreach (var binding in bindings)
            {
                if (binding.TrackOrdinal < 0 || binding.TrackOrdinal >= tracks.Count || !boundTracks.Add(binding.TrackOrdinal) ||
                    tracks[binding.TrackOrdinal] == null || tracks[binding.TrackOrdinal].MultiCameraNo != binding.CompositeIndex)
                    throw new ArgumentException("Bindings must match unique authored tracks.", nameof(bindings));
            }
            this.control = control;
            Bridge = new OriginalLiveTimelineBridge();
            foreach (var binding in bindings) Bridge.BindTrack(binding.TrackOrdinal, binding.CompositeIndex);
            Bridge.UseConfirmedOwnerClock(profile.DurationMultiplier);
            resolver = new OriginalLiveDrawInputResolver(selection =>
                (inputs != null ? inputs(selection) : default).WithConfirmedProfile(profile));
            Bridge.FrameReady += OnFrame;
            control.OriginalCompositeBridge = Bridge;
        }

        void OnFrame(OriginalLiveTimelineBridge.Frame selected)
        {
            if (disposed) return;
            var resolved = new List<ResolvedSelection>(boundTracks.Count);
            foreach (var selection in selected.Selections)
                if (boundTracks.Contains(selection.TrackOrdinal))
                    resolved.Add(new ResolvedSelection(selection, resolver.Resolve(selection)));
            var frame = new Frame(selected, resolved.ToArray());
            LastFrame = frame; FrameCount++;
            var handlers = FrameReady;
            if (handlers == null) return;
            // Bridge retains its established fault accounting and handler isolation.
            foreach (Action<Frame> handler in handlers.GetInvocationList())
                try { handler(frame); }
                catch (Exception e) { ReportConsumerException(e); }
        }

        public Exception LastConsumerException { get; private set; }
        public int ConsumerFaultCount { get; private set; }
        void ReportConsumerException(Exception exception)
        { LastConsumerException = exception; ConsumerFaultCount++; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Bridge.FrameReady -= OnFrame;
            Bridge.ClearConfirmedOwnerClock();
            if (control && ReferenceEquals(control.OriginalCompositeBridge, Bridge)) control.OriginalCompositeBridge = null;
        }
    }
}
