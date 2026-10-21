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

            public Inputs(OriginalCompositeCueAdapter.DrawState evaluatedDraw,
                OriginalCompositeCueAdapter.TextureMapping? texture,
                OriginalCompositeProducerMath.AngleInputs angles = default,
                OriginalCompositeProducerMath.FadeResult? resolvedFade = null)
            {
                EvaluatedDraw = evaluatedDraw; Texture = texture;
                Angles = angles; ResolvedFade = resolvedFade;
            }
        }

        readonly Func<OriginalLiveTimelineBridge.Selection, Inputs> inputsForSelection;

        // A missing provider leaves the existing missing-input behavior unchanged.
        // The provider receives this exact Selection on every call; no time cache,
        // frame conversion, kind lookup or scene search is performed here.
        public OriginalLiveDrawInputResolver(
            Func<OriginalLiveTimelineBridge.Selection, Inputs> inputsForSelection)
        { this.inputsForSelection = inputsForSelection; }

        public OriginalCompositeCueAdapter.Snapshot Resolve(OriginalLiveTimelineBridge.Selection selection)
        {
            var inputs = inputsForSelection != null ? inputsForSelection(selection) : default;
            return Complete(selection.Cue, inputs);
        }

        // Existing evaluated values take priority, then explicit producer values,
        // then the known arithmetic with explicitly resolved helper dependencies.
        public static OriginalCompositeCueAdapter.Snapshot Complete(
            OriginalCompositeCueAdapter.Snapshot selected, Inputs inputs)
        {
            var draw = new OriginalCompositeCueAdapter.DrawState(
                selected.Draw.Fade ?? inputs.EvaluatedDraw.Fade,
                selected.Draw.EffectiveThickness ?? inputs.EvaluatedDraw.EffectiveThickness,
                selected.Draw.ShaderAngles ?? inputs.EvaluatedDraw.ShaderAngles);
            var snapshot = new OriginalCompositeCueAdapter.Snapshot(
                selected.Source, selected.Values, draw, selected.Texture ?? inputs.Texture,
                selected.Stage, selected.RequestedInterpolation,
                selected.AppliedInterpolation, selected.AppliedRatio);
            return OriginalCompositeProducerMath.ApplyToSnapshot(snapshot, inputs.Angles, inputs.ResolvedFade);
        }
    }
}
