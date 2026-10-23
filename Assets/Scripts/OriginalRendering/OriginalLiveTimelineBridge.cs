using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    public sealed class OriginalLiveTimelineBridge
    {
        public readonly struct Selection
        {
            public readonly long Serial;
            public readonly float ViewerFrame;
            public readonly int TrackOrdinal, CurrentKeyFrame;
            public readonly int? NextKeyFrame;
            public readonly bool KeyEnabled;
            public readonly float RawFadeDuration;
            public readonly OriginalCompositeProducerMath.ComparisonFrames? ComparisonClock;
            public readonly OriginalCompositeCueAdapter.Snapshot Cue;
            public readonly Camera ViewerCamera;
            internal Selection(long serial, float frame, int k, int keyFrame, int? nextFrame,
                OriginalCompositeCueAdapter.Snapshot cue, Camera camera,
                bool keyEnabled, float rawFadeDuration,
                OriginalCompositeProducerMath.ComparisonFrames? comparisonClock)
            { Serial = serial; ViewerFrame = frame; TrackOrdinal = k; CurrentKeyFrame = keyFrame;
              NextKeyFrame = nextFrame; Cue = cue; ViewerCamera = camera;
              KeyEnabled = keyEnabled; RawFadeDuration = rawFadeDuration; ComparisonClock = comparisonClock; }
        }
        public sealed class Frame
        {
            public readonly long Serial;
            public readonly float ViewerFrame;
            public readonly ReadOnlyCollection<Selection> Selections;
            public readonly PostEffectUpdateInfo_BloomDiffusion? PostEffect;
            internal Frame(long serial, float frame, Selection[] selections, PostEffectUpdateInfo_BloomDiffusion? postEffect)
            { Serial = serial; ViewerFrame = frame; Selections = Array.AsReadOnly(selections); PostEffect = postEffect; }
            public bool TryGetTrack(int ordinal, out Selection selection)
            {
                selection = default; int count = 0;
                foreach (var s in Selections) if (s.TrackOrdinal == ordinal) { selection = s; count++; }
                return count == 1;
            }
        }
        sealed class Token { public long Id; }
        readonly ConditionalWeakTable<object, Token> tokens = new ConditionalWeakTable<object, Token>();
        readonly Dictionary<int, int> compositeIndices = new Dictionary<int, int>();
        readonly List<Selection> pending = new List<Selection>();
        long serial, nextToken;
        float viewerFrame;
        bool collecting;
        PostEffectUpdateInfo_BloomDiffusion? postEffect;
        float? ownerClockMultiplier;
        OriginalCompositeProducerMath.ComparisonFrames? ownerClock;
        public event Action<Frame> FrameReady;
        public Frame LastFrame { get; private set; }
        public Exception LastException { get; private set; }
        public int FaultCount { get; private set; }
        public void BindTrack(int trackOrdinal, int compositeIndex) { compositeIndices[trackOrdinal] = compositeIndex; }
        public void UnbindTrack(int trackOrdinal) { compositeIndices.Remove(trackOrdinal); }
        // Opt in before an actual AlterUpdate. A late registration must wait for
        // its next owner update; BeginFrame never reconstructs a missing clock.
        public void UseConfirmedOwnerClock(float confirmedMultiplier)
        { ownerClockMultiplier = confirmedMultiplier; ownerClock = null; }
        public void ClearConfirmedOwnerClock()
        { ownerClockMultiplier = null; ownerClock = null; }
        public void CaptureOwnerTimes(float currentOwnerTime, float comparisonOwnerTime)
        {
            if (!ownerClockMultiplier.HasValue) return;
            ownerClock = OriginalCompositeProducerMath.ResolveOwnerComparisonFrames(
                currentOwnerTime, comparisonOwnerTime, ownerClockMultiplier.Value);
        }
        long TokenFor(object key) => tokens.GetValue(key, _ => new Token { Id = ++nextToken }).Id;
        public void BeginFrame(float frame)
        { pending.Clear(); postEffect = null; viewerFrame = frame; serial++; collecting = true; }

        // Preserve the value actually emitted by Control in this update. No key
        // search, parameter conversion, or previous-frame fallback occurs here.
        public void CapturePostEffect(PostEffectUpdateInfo_BloomDiffusion value)
        { if (collecting) postEffect = value; }

        // Called with the already selected key objects and the ratio actually used by
        // LiveTimelineControl. No second key search or interpolation-helper invocation.
        public void CaptureSelected(int k, float frame, LiveTimelineKeyMultiCameraPositionData current,
            LiveTimelineKeyMultiCameraPositionData next, float? usedRatio, Camera viewerCamera)
        {
            try
            {
                if (!collecting || current == null) return;
                int? j = compositeIndices.TryGetValue(k, out int index) ? index : (int?)null;
                var context = new OriginalCompositeCueAdapter.Context(k, j, TokenFor(current),
                    next != null ? TokenFor(next) : (long?)null, serial);
                var cue = OriginalCompositeCueAdapter.FromSelectedKeys(context, Values(current),
                    next != null ? Values(next) : (OriginalCompositeCueAdapter.CueValues?)null,
                    usedRatio.HasValue, usedRatio.GetValueOrDefault(), default, null);
                pending.Add(new Selection(serial, frame, k, current.frame, next != null ? next.frame : (int?)null,
                    cue, viewerCamera, current.enableMultiCamera, current.fadeTime, ownerClock));
            }
            catch (Exception e) { Record(e); }
        }
        static OriginalCompositeCueAdapter.CueValues Values(LiveTimelineKeyMultiCameraPositionData key)
        {
            // Viewer typed fields are preserved as Viewer inputs. The absent secondary
            // angle field and unknown shader-domain conversions are not invented.
            return new OriginalCompositeCueAdapter.CueValues(key.lineThickness, key.LineColor, key.LineAntialiasing,
                key.maskOffset, key.maskRoll, null, (int)key.maskType, (int)key.LineType);
        }
        public void CompleteFrame()
        {
            if (!collecting) return;
            collecting = false;
            var frame = new Frame(serial, viewerFrame, pending.ToArray(), postEffect); LastFrame = frame;
            // A comparison pair belongs to one owner update. A second late update
            // without AlterUpdate must not reuse it as a new observation.
            ownerClock = null;
            var handlers = FrameReady;
            if (handlers == null) return;
            foreach (Action<Frame> handler in handlers.GetInvocationList())
                try { handler(frame); } catch (Exception e) { Record(e); }
        }
        void Record(Exception exception) { LastException = exception; FaultCount++; }
    }
}
