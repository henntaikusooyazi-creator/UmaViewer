using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Pure arithmetic from saved producer instructions. No asset, clock, camera,
    // or kind-table lookup; callers explicitly supply the unresolved dependencies.
    public static class OriginalCompositeProducerMath
    {
        public readonly struct ThicknessInputs
        {
            public readonly float? FixedValue, Minimum;
            // Explicit receiver state can resolve the time-dependent kind-0 branch.
            public readonly bool? UseCueThickness;
            public ThicknessInputs(float? fixedValue, float? minimum, bool? useCueThickness = null)
            { FixedValue = fixedValue; Minimum = minimum; UseCueThickness = useCueThickness; }
        }

        // Nonzero kinds have an unconditional producer write to the receiver flag.
        // Kind 0 depends on unresolved timing/state and is not inferred here.
        public static bool? SelectThicknessBranch(int? kind)
        { return !kind.HasValue || kind.Value == 0 ? (bool?)null : kind.Value != 1; }

        public static float? EvaluateEffectiveThickness(
            OriginalCompositeCueAdapter.CueValues values, ThicknessInputs inputs)
        {
            bool? useCue = inputs.UseCueThickness ?? SelectThicknessBranch(values.Kind);
            if (!useCue.HasValue) return null;
            if (!useCue.Value) return inputs.FixedValue;
            if (!values.Thickness.HasValue || !inputs.Minimum.HasValue) return null;
            float cue = values.Thickness.Value, minimum = inputs.Minimum.Value;
            // Ordered comparison preserves MAXSS's second-operand selection for
            // equal values (including signed zero) and unordered comparisons.
            return cue > minimum ? cue : minimum;
        }

        public readonly struct AngleInputs
        {
            public readonly float? KindBase, Divisor, SecondaryOrigin, SecondaryScale;
            public AngleInputs(float? kindBase, float? divisor,
                float? secondaryOrigin = null, float? secondaryScale = null)
            {
                KindBase = kindBase; Divisor = divisor;
                SecondaryOrigin = secondaryOrigin; SecondaryScale = secondaryScale;
            }
        }

        [Flags]
        public enum MissingAngleInput
        {
            None = 0, RotationSource = 1, KindBase = 2, Divisor = 4, Kind = 8,
            SecondarySource = 16, SecondaryOrigin = 32, SecondaryScale = 64
        }

        public readonly struct AngleResult
        {
            public readonly float? Z, W;
            public readonly MissingAngleInput Missing;
            public Vector2? Value => Z.HasValue && W.HasValue
                ? new Vector2(Z.Value, W.Value) : (Vector2?)null;
            internal AngleResult(float? z, float? w, MissingAngleInput missing)
            { Z = z; W = w; Missing = missing; }
        }

        // Values must already have passed through the existing key interpolation.
        // Only the current key's kind is used; kind 10 has the secondary branch.
        public static AngleResult EvaluateShaderAngles(
            OriginalCompositeCueAdapter.CueValues values, AngleInputs inputs)
        {
            var missing = MissingAngleInput.None;
            if (!values.RotationSource.HasValue) missing |= MissingAngleInput.RotationSource;
            if (!inputs.KindBase.HasValue) missing |= MissingAngleInput.KindBase;
            if (!inputs.Divisor.HasValue) missing |= MissingAngleInput.Divisor;
            float? z = null, w = null;
            if (missing == MissingAngleInput.None)
            {
                float sum = inputs.KindBase.Value + values.RotationSource.Value;
                z = sum / inputs.Divisor.Value;
            }
            if (!values.Kind.HasValue) missing |= MissingAngleInput.Kind;
            else if (values.Kind.Value != 10) w = 0f;
            else
            {
                if (!values.SecondaryAngleSource.HasValue) missing |= MissingAngleInput.SecondarySource;
                if (!inputs.SecondaryOrigin.HasValue) missing |= MissingAngleInput.SecondaryOrigin;
                if (!inputs.SecondaryScale.HasValue) missing |= MissingAngleInput.SecondaryScale;
                if (values.SecondaryAngleSource.HasValue && inputs.SecondaryOrigin.HasValue &&
                    inputs.SecondaryScale.HasValue && inputs.Divisor.HasValue)
                {
                    float scaled = values.SecondaryAngleSource.Value * inputs.SecondaryScale.Value;
                    float difference = inputs.SecondaryOrigin.Value - scaled;
                    w = difference / inputs.Divisor.Value;
                }
            }
            return new AngleResult(z, w, missing);
        }

        public readonly struct FadeResult
        {
            public readonly int EndFrame;
            public readonly float BeforeSetter, Fade;
            public readonly bool OutFlag, ReturnValue;
            // Null means the helper does not issue a camera-enable operation.
            public readonly bool? RequestedCameraEnabled;
            internal FadeResult(int endFrame, float beforeSetter, bool outFlag,
                bool returnValue, bool? requestedCameraEnabled)
            {
                EndFrame = endFrame; BeforeSetter = beforeSetter;
                Fade = ClampFadeSetter(beforeSetter); OutFlag = outFlag;
                ReturnValue = returnValue;
                RequestedCameraEnabled = requestedCameraEnabled;
            }
        }

        // The owner/key selection must already have succeeded. durationCount is the
        // int32 result AFTER the original time multiplier, double helper, and CVTT.
        // This function does not turn raw key duration into frames or assume an FPS.
        public static FadeResult EvaluateFadeAfterResolvedDuration(int frame, int ownerComparisonFrame,
            int keyFrame, int durationCount, bool keyEnabled, int kind)
        {
            int end = unchecked(keyFrame + durationCount);
            if (frame >= end)
            {
                bool crossed = ownerComparisonFrame < end;
                return new FadeResult(end, keyEnabled ? 1f : 0f, crossed, crossed,
                    kind != 0 && keyEnabled);
            }

            int delta = unchecked(frame - keyFrame);
            float ratio = (float)delta / (float)durationCount;
            ratio = ClampFadeSetter(ratio);
            bool rising = keyEnabled && kind != 0;
            float start = rising ? 0f : 1f;
            float difference = (rising ? 1f : 0f) - start;
            float scaled = difference * ratio;
            float fade = scaled + start;
            return new FadeResult(end, fade, true, false,
                ownerComparisonFrame < keyFrame ? true : (bool?)null);
        }

        // Mirrors the scalar comparisons. NaN and negative zero pass through;
        // no IsFinite guard or new rejection condition is introduced.
        public static float ClampFadeSetter(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        // Opt-in only. Existing evaluated/observed draw values take precedence.
        // Unresolved helper inputs leave the corresponding component unresolved.
        public static OriginalCompositeCueAdapter.Snapshot ApplyToSnapshot(
            OriginalCompositeCueAdapter.Snapshot snapshot, AngleInputs angles, FadeResult? fade = null)
        {
            var evaluatedAngles = EvaluateShaderAngles(snapshot.Values, angles);
            var draw = new OriginalCompositeCueAdapter.DrawState(
                snapshot.Draw.Fade ?? fade?.Fade, snapshot.Draw.EffectiveThickness,
                snapshot.Draw.ShaderAngles ?? evaluatedAngles.Value);
            return snapshot.WithDrawState(draw);
        }
    }
}
