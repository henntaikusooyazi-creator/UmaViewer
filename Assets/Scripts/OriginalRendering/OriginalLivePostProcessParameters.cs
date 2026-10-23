using System;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Opt-in current-frame conversion. A missing film observation stays null;
    // neither an older overlay nor a zero-filled overlay is substituted.
    public sealed class OriginalLivePostProcessParameters : IOriginalCameraFramePostProcessor
    {
        public readonly struct FilmObservation
        {
            public readonly long SelectedSerial;
            public readonly Camera Camera;
            public readonly OriginalPostProcessProducerMath.FilmInputs Value;
            public FilmObservation(long selectedSerial, Camera camera, OriginalPostProcessProducerMath.FilmInputs value)
            { SelectedSerial = selectedSerial; Camera = camera; Value = value; }
        }
        public readonly struct Values
        {
            public readonly PostEffectUpdateInfo_BloomDiffusion Input;
            public readonly OriginalPostProcessProducerMath.BloomDiffusionValues BloomDiffusion;
            public readonly OriginalPostProcessProducerMath.FilmValues? Film;
            internal Values(PostEffectUpdateInfo_BloomDiffusion input,
                OriginalPostProcessProducerMath.BloomDiffusionValues bloomDiffusion,
                OriginalPostProcessProducerMath.FilmValues? film)
            { Input = input; BloomDiffusion = bloomDiffusion; Film = film; }
        }
        readonly OriginalLivePostProcessInputs adapter;
        public long ProcessCount => adapter.ProcessCount;
        public long LastSelectedSerial => adapter.LastSelectedSerial;

        public OriginalLivePostProcessParameters(Func<OriginalLiveTimelineBridge.Frame> current,
            OriginalPostProcessProducerMath.ConfirmedProfile profile,
            Func<OriginalCameraFrame, Values, RenderTexture> process,
            Func<OriginalCameraFrame, FilmObservation?> film = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (process == null) throw new ArgumentNullException(nameof(process));
            adapter = new OriginalLivePostProcessInputs(current, (frame, payload) =>
            {
                var bloom = OriginalPostProcessProducerMath.EvaluateBloomDiffusion(payload,
                    frame.Color.width, frame.Color.height, profile);
                OriginalPostProcessProducerMath.FilmValues? evaluatedFilm = null;
                var observation = film?.Invoke(frame);
                if (observation.HasValue)
                {
                    var value = observation.Value;
                    if (value.SelectedSerial != frame.SelectedSerial || value.Camera != frame.Source)
                        throw new InvalidOperationException("Film must belong to the same selected update and camera.");
                    evaluatedFilm = OriginalPostProcessProducerMath.EvaluateFilm(value.Value,
                        frame.Color.width, frame.Color.height);
                }
                return process(frame, new Values(payload, bloom, evaluatedFilm));
            });
        }
        public RenderTexture Process(OriginalCameraFrame frame) => adapter.Process(frame);
    }
}
