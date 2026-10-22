using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Pure arithmetic from saved producer instructions. No asset, clock or camera
    // lookup; callers explicitly supply the qualified scalar/kind dependencies.
    public static class OriginalCompositeProducerMath
    {
        public readonly struct ComparisonFrames
        {
            public readonly int Current, Comparison;
            public readonly float CurrentOwnerTime, ComparisonOwnerTime;
            internal ComparisonFrames(float currentOwnerTime, float comparisonOwnerTime,
                int current, int comparison)
            { CurrentOwnerTime = currentOwnerTime; ComparisonOwnerTime = comparisonOwnerTime;
              Current = current; Comparison = comparison; }
        }

        // The two owner-time fields and their multiplier must be qualified by the
        // caller. This does not consume ViewerFrame, Unity frameCount or serial.
        public static ComparisonFrames ResolveOwnerComparisonFrames(float currentOwnerTime,
            float comparisonOwnerTime, float confirmedMultiplier)
        {
            return new ComparisonFrames(currentOwnerTime, comparisonOwnerTime,
                ResolveDurationCount(currentOwnerTime, confirmedMultiplier),
                ResolveDurationCount(comparisonOwnerTime, confirmedMultiplier));
        }

        static float Single(float value)
        { return BitConverter.ToSingle(BitConverter.GetBytes(value), 0); }
        // Explicit, immutable dependencies qualified by the caller for its edition.
        // No private capture, address, game path or guessed table is embedded here.
        public sealed class ConfirmedProfile
        {
            public readonly float FixedThickness, MinimumThickness, AngleDivisor;
            public readonly float SecondaryOrigin, SecondaryScale, DurationMultiplier;
            readonly float[] kindBases;
            public ConfirmedProfile(float fixedThickness, float minimumThickness,
                float angleDivisor, float secondaryOrigin, float secondaryScale,
                float durationMultiplier, float[] kindBases)
            {
                if (kindBases == null || kindBases.Length != 11)
                    throw new ArgumentException("An explicitly resolved eleven-entry kind table is required.", nameof(kindBases));
                FixedThickness = fixedThickness; MinimumThickness = minimumThickness;
                AngleDivisor = angleDivisor; SecondaryOrigin = secondaryOrigin;
                SecondaryScale = secondaryScale; DurationMultiplier = durationMultiplier;
                this.kindBases = (float[])kindBases.Clone();
            }
            public float? KindBase(int? kind)
            {
                if (!kind.HasValue) return null;
                // The saved unsigned range check sends negative and >10 to zero.
                return (uint)kind.Value <= 10 ? kindBases[kind.Value] : 0f;
            }
            public AngleInputs Angles(int? kind, AngleInputs overrides)
            {
                return new AngleInputs(overrides.KindBase ?? KindBase(kind),
                    overrides.Divisor ?? AngleDivisor,
                    overrides.SecondaryOrigin ?? SecondaryOrigin,
                    overrides.SecondaryScale ?? SecondaryScale);
            }
        }

        // The producer multiplies in float32, widens, floors, then CVTTSD2SI.
        // The multiplier is a captured dependency, not a Viewer FPS assumption.
        public static int ResolveDurationCount(float rawDuration, float multiplier)
        {
            // Mono may retain an intermediate as double across a float local.
            // A real 32-bit store preserves the producer's MULSS-before-CVT order.
            float scaled = Single(rawDuration * multiplier);
            double rounded = System.Math.Floor((double)scaled);
            if (double.IsNaN(rounded) || rounded < int.MinValue || rounded >= 2147483648d)
                return int.MinValue; // x64 CVTT indefinite integer.
            return (int)rounded;
        }

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
                float sum = Single(inputs.KindBase.Value + values.RotationSource.Value);
                z = Single(sum / inputs.Divisor.Value);
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
                    float scaled = Single(values.SecondaryAngleSource.Value * inputs.SecondaryScale.Value);
                    float difference = Single(inputs.SecondaryOrigin.Value - scaled);
                    w = Single(difference / inputs.Divisor.Value);
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
            float ratio = Single((float)delta / (float)durationCount);
            ratio = ClampFadeSetter(ratio);
            bool rising = keyEnabled && kind != 0;
            float start = rising ? 0f : 1f;
            float difference = Single((rising ? 1f : 0f) - start);
            float scaled = Single(difference * ratio);
            float fade = Single(scaled + start);
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
