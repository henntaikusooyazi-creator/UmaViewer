using System;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // A caller opts in for one already-loaded Viewer timeline. No scene search,
    // song-number heuristic, ordinal-to-j inference, or original camera attribution.
    public sealed class OriginalLiveTimelineRegistration : IDisposable
    {
        public readonly struct TrackBinding
        {
            public readonly int TrackOrdinal, CompositeIndex;
            public TrackBinding(int trackOrdinal, int compositeIndex)
            { TrackOrdinal = trackOrdinal; CompositeIndex = compositeIndex; }
            public static TrackBinding FromTrack(int ordinal, LiveTimelineMultiCameraPositionData track)
            {
                if (track == null) throw new ArgumentNullException(nameof(track));
                return new TrackBinding(ordinal, track.MultiCameraNo);
            }
        }
        readonly LiveTimelineControl control;
        bool disposed;
        public OriginalLiveTimelineBridge Bridge { get; }
        public OriginalLiveRenderSession Session { get; }
        public OriginalLiveTimelineRegistration(LiveTimelineControl control, int rightTrack, int leftTrack,
            Func<OriginalLiveTimelineBridge.Frame, Camera> selectBaseCamera,
            Shader compositeShader, Shader coverageShader, RenderTexture output,
            Func<OriginalLiveTimelineBridge.Selection, OriginalCompositeCueAdapter.Snapshot> resolve)
            : this(control,
                TrackBinding.FromTrack(rightTrack, control.data.worksheetList[0].multiCameraPosKeys[rightTrack]),
                TrackBinding.FromTrack(leftTrack, control.data.worksheetList[0].multiCameraPosKeys[leftTrack]),
                selectBaseCamera, compositeShader, coverageShader, output, resolve) { }
        public OriginalLiveTimelineRegistration(LiveTimelineControl control,
            TrackBinding right, TrackBinding left,
            Func<OriginalLiveTimelineBridge.Frame, Camera> selectBaseCamera,
            Shader compositeShader, Shader coverageShader, RenderTexture output,
            Func<OriginalLiveTimelineBridge.Selection, OriginalCompositeCueAdapter.Snapshot> resolve)
        {
            if (!control) throw new ArgumentNullException(nameof(control));
            if (control.OriginalCompositeBridge != null)
                throw new InvalidOperationException("The existing bridge must remain owned by its current registration.");
            this.control = control;
            Bridge = new OriginalLiveTimelineBridge();
            Bridge.BindTrack(right.TrackOrdinal, right.CompositeIndex);
            Bridge.BindTrack(left.TrackOrdinal, left.CompositeIndex);
            Session = new OriginalLiveRenderSession(Bridge, compositeShader, coverageShader,
                right.TrackOrdinal, left.TrackOrdinal, selectBaseCamera, output, resolve);
            control.OriginalCompositeBridge = Bridge;
        }
        public void Dispose()
        {
            if (disposed) return;
            Session.Dispose();
            if (control && ReferenceEquals(control.OriginalCompositeBridge, Bridge)) control.OriginalCompositeBridge = null;
            disposed = true;
        }
    }
}
