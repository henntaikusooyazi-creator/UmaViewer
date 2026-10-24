using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    public sealed class OriginalLivePostFilmProvider
    {
        readonly Func<OriginalLiveTimelineBridge.Frame> current;
        readonly OriginalPostFilmMath.ConfirmedProfile profile;
        readonly int layer;
        public OriginalLivePostFilmProvider(Func<OriginalLiveTimelineBridge.Frame> current,
            OriginalPostFilmMath.ConfirmedProfile profile, int layer)
        {
            this.current = current ?? throw new ArgumentNullException(nameof(current));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (layer < 1 || layer > 3) throw new ArgumentOutOfRangeException(nameof(layer));
            this.layer = layer;
        }
        public OriginalLivePostProcessParameters.FilmObservation? Resolve(OriginalCameraFrame frame)
        {
            var selected = current();
            if (selected == null || selected.Serial != frame.SelectedSerial || !frame.Source)
                throw new InvalidOperationException("Film must come from the current selected update and camera.");
            if (!selected.TryGetFilm(frame.Source, layer, out var film)) return null;
            if (!film.Value.HasValue) throw new NotSupportedException(film.UnavailableReason ?? "Film producer unavailable.");
            return new OriginalLivePostProcessParameters.FilmObservation(selected.Serial, frame.Source,
                OriginalPostFilmMath.ToFilm(film.Value.Value, profile));
        }
    }
}
