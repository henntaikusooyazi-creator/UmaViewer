using System;

namespace Gallop.Live.Rendering
{
    // The normal consumed-audio branch only. Initialization, pause, source lifetime
    // and edition-specific dependencies remain explicit caller responsibilities.
    public static class OriginalLiveClockMath
    {
        public readonly struct State
        {
            public readonly float PreviousSourceTime, SmoothedTime, Correction, CurrentTime;
            public State(float previousSourceTime, float smoothedTime, float correction, float currentTime)
            { PreviousSourceTime = previousSourceTime; SmoothedTime = smoothedTime; Correction = correction; CurrentTime = currentTime; }
        }
        public readonly struct Parameters
        {
            public readonly bool SmoothingEnabled;
            public readonly float ReferenceStep, DeadZone, CorrectionLimit, ResetDistance, MaximumTime;
            public Parameters(bool smoothingEnabled, float referenceStep, float deadZone, float correctionLimit,
                float resetDistance, float maximumTime)
            { SmoothingEnabled = smoothingEnabled; ReferenceStep = referenceStep; DeadZone = deadZone;
              CorrectionLimit = correctionLimit; ResetDistance = resetDistance; MaximumTime = maximumTime; }
        }
        public readonly struct Result
        {
            public readonly State Next;
            public readonly float SourceTime, PreviousTime, DeltaTime;
            public readonly bool Smoothed;
            internal Result(State next, float sourceTime, float previousTime, float deltaTime, bool smoothed)
            { Next = next; SourceTime = sourceTime; PreviousTime = previousTime; DeltaTime = deltaTime; Smoothed = smoothed; }
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static float Single(float value) => BitConverter.ToSingle(BitConverter.GetBytes(value), 0);

        public static bool TryEvaluate(float sourceTime, State state, Parameters parameters, out Result result)
        {
            result = default;
            if (!Finite(sourceTime) || !Finite(state.PreviousSourceTime) || !Finite(state.SmoothedTime) ||
                !Finite(state.Correction) || !Finite(state.CurrentTime) || !Finite(parameters.ReferenceStep) ||
                !Finite(parameters.DeadZone) || !Finite(parameters.CorrectionLimit) ||
                !Finite(parameters.ResetDistance) || !Finite(parameters.MaximumTime) ||
                parameters.ReferenceStep < 0 || parameters.DeadZone < 0 || parameters.CorrectionLimit < 0 ||
                parameters.ResetDistance < 0 || parameters.MaximumTime <= 0) return false;
            float delta = Single(sourceTime - state.PreviousSourceTime);
            float distance = Single(state.SmoothedTime - sourceTime);
            if (!Finite(delta) || !Finite(distance)) return false;
            bool smooth = parameters.SmoothingEnabled && System.Math.Abs(distance) <= parameters.ResetDistance;
            float smoothed, accumulated;
            if (smooth)
            {
                float correction = Single(delta - parameters.ReferenceStep);
                if (!Finite(correction)) return false;
                if (correction < -parameters.CorrectionLimit) correction = -parameters.CorrectionLimit;
                if (correction > parameters.CorrectionLimit) correction = parameters.CorrectionLimit;
                accumulated = state.Correction;
                if (System.Math.Abs(correction) > parameters.DeadZone &&
                    (correction > 0 ? parameters.DeadZone > accumulated : accumulated > -parameters.DeadZone))
                    accumulated = Single(correction + accumulated);
                else correction = 0;
                smoothed = Single(Single(delta - correction) + state.SmoothedTime);
            }
            else { smoothed = sourceTime; accumulated = 0; }
            float current = smoothed < 0 ? 0 : smoothed > parameters.MaximumTime ? parameters.MaximumTime : smoothed;
            float elapsed = Single(current - state.CurrentTime);
            if (!Finite(smoothed) || !Finite(accumulated) || !Finite(elapsed)) return false;
            result = new Result(new State(sourceTime, smoothed, accumulated, current), sourceTime, state.CurrentTime, elapsed, smooth);
            return true;
        }
    }
}
