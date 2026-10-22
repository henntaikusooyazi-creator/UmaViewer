using System;

namespace Gallop.Live.Rendering
{
    // An opt-in resolve delegate for the existing timeline registration/session.
    // Control owns key selection, interpolation, track identity and camera selection.
    public sealed class OriginalLiveDrawInputResolver
    {
        public readonly struct Inputs
        {
            public readonly OriginalCompositeCueAdapter.DrawState EvaluatedDraw;
            public readonly OriginalCompositeCueAdapter.TextureMapping? Texture;
            public readonly OriginalCompositeProducerMath.AngleInputs Angles;
            public readonly OriginalCompositeProducerMath.FadeResult? ResolvedFade;
            public readonly OriginalCompositeProducerMath.ThicknessInputs? Thickness;
            public readonly OriginalCompositeProducerMath.ConfirmedProfile Profile;
            public readonly int? CueComparisonFrame, OwnerComparisonFrame;
            public readonly bool? UseCueThickness;

            public Inputs(OriginalCompositeCueAdapter.DrawState evaluatedDraw,
                OriginalCompositeCueAdapter.TextureMapping? texture,
                OriginalCompositeProducerMath.AngleInputs angles = default,
                OriginalCompositeProducerMath.FadeResult? resolvedFade = null)
                : this(evaluatedDraw, texture, angles, resolvedFade, null) { }

            // The existing constructor remains unchanged. Only this explicit
            // overload enables the cue-to-effective-thickness calculation.
            public Inputs(OriginalCompositeCueAdapter.DrawState evaluatedDraw,
                OriginalCompositeCueAdapter.TextureMapping? texture,
                OriginalCompositeProducerMath.AngleInputs angles,
                OriginalCompositeProducerMath.FadeResult? resolvedFade,
                OriginalCompositeProducerMath.ThicknessInputs? thickness)
            {
                EvaluatedDraw = evaluatedDraw; Texture = texture;
                Angles = angles; ResolvedFade = resolvedFade; Thickness = thickness;
                Profile = null; CueComparisonFrame = OwnerComparisonFrame = null;
                UseCueThickness = null;
            }

            Inputs(Inputs source, OriginalCompositeProducerMath.ConfirmedProfile profile,
                int? cueComparisonFrame, int? ownerComparisonFrame, bool? useCueThickness)
            {
                EvaluatedDraw = source.EvaluatedDraw; Texture = source.Texture;
                Angles = source.Angles; ResolvedFade = source.ResolvedFade; Thickness = source.Thickness;
                Profile = profile; CueComparisonFrame = cueComparisonFrame;
                OwnerComparisonFrame = ownerComparisonFrame; UseCueThickness = useCueThickness;
            }

            // Opt in per Selection. Comparison frames retain their explicit clock
            // domain; ViewerFrame/serial/Unity frame are never converted here.
            public Inputs WithConfirmedProfile(OriginalCompositeProducerMath.ConfirmedProfile profile,
                int? cueComparisonFrame = null, int? ownerComparisonFrame = null,
                bool? useCueThickness = null)
            { return new Inputs(this, profile, cueComparisonFrame, ownerComparisonFrame, useCueThickness); }
        }

        readonly Func<OriginalLiveTimelineBridge.Selection, Inputs> inputsForSelection;

        // A missing provider leaves the existing missing-input behavior unchanged.
        // The provider receives this exact Selection on every call; no time cache,
        // frame conversion, inferred kind table or scene search is performed here.
        public OriginalLiveDrawInputResolver(
            Func<OriginalLiveTimelineBridge.Selection, Inputs> inputsForSelection)
        { this.inputsForSelection = inputsForSelection; }

        public OriginalCompositeCueAdapter.Snapshot Resolve(OriginalLiveTimelineBridge.Selection selection)
        {
            var inputs = inputsForSelection != null ? inputsForSelection(selection) : default;
            OriginalCompositeProducerMath.FadeResult? naturalFade = null;
            int? cueFrame = inputs.CueComparisonFrame, ownerFrame = inputs.OwnerComparisonFrame;
            // Only an entirely absent explicit pair may use the same Selection's
            // owner-clock snapshot. Never combine halves from different sources.
            if (!cueFrame.HasValue && !ownerFrame.HasValue && selection.ComparisonClock.HasValue)
            {
                cueFrame = selection.ComparisonClock.Value.Current;
                ownerFrame = selection.ComparisonClock.Value.Comparison;
            }
            if (inputs.Profile != null && cueFrame.HasValue && ownerFrame.HasValue && selection.Cue.Values.Kind.HasValue)
            {
                int duration = OriginalCompositeProducerMath.ResolveDurationCount(
                    selection.RawFadeDuration, inputs.Profile.DurationMultiplier);
                naturalFade = OriginalCompositeProducerMath.EvaluateFadeAfterResolvedDuration(
                    cueFrame.Value, ownerFrame.Value,
                    selection.CurrentKeyFrame, duration, selection.KeyEnabled, selection.Cue.Values.Kind.Value);
            }
            return Complete(selection.Cue, inputs, naturalFade);
        }

        // Existing evaluated values take priority, then explicit producer values,
        // then the known arithmetic with explicitly resolved helper dependencies.
        public static OriginalCompositeCueAdapter.Snapshot Complete(
            OriginalCompositeCueAdapter.Snapshot selected, Inputs inputs)
        { return Complete(selected, inputs, null); }

        static OriginalCompositeCueAdapter.Snapshot Complete(
            OriginalCompositeCueAdapter.Snapshot selected, Inputs inputs,
            OriginalCompositeProducerMath.FadeResult? naturalFade)
        {
            var thickness = inputs.Thickness;
            var angles = inputs.Angles;
            if (inputs.Profile != null)
            {
                thickness = new OriginalCompositeProducerMath.ThicknessInputs(
                    thickness?.FixedValue ?? inputs.Profile.FixedThickness,
                    thickness?.Minimum ?? inputs.Profile.MinimumThickness,
                    thickness?.UseCueThickness ?? inputs.UseCueThickness);
                angles = inputs.Profile.Angles(selected.Values.Kind, angles);
            }
            var draw = new OriginalCompositeCueAdapter.DrawState(
                selected.Draw.Fade ?? inputs.EvaluatedDraw.Fade,
                selected.Draw.EffectiveThickness ?? inputs.EvaluatedDraw.EffectiveThickness ??
                    (thickness.HasValue ? OriginalCompositeProducerMath.EvaluateEffectiveThickness(selected.Values, thickness.Value) : (float?)null),
                selected.Draw.ShaderAngles ?? inputs.EvaluatedDraw.ShaderAngles);
            var snapshot = new OriginalCompositeCueAdapter.Snapshot(
                selected.Source, selected.Values, draw, selected.Texture ?? inputs.Texture,
                selected.Stage, selected.RequestedInterpolation,
                selected.AppliedInterpolation, selected.AppliedRatio);
            return OriginalCompositeProducerMath.ApplyToSnapshot(snapshot, angles, inputs.ResolvedFade ?? naturalFade);
        }
    }
}
