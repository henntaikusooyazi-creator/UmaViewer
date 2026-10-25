using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Synchronous Viewer state. This does not establish original global writers,
    // pending external command-buffer state, or a source-to-Viewer clock contract.
    public sealed class OriginalPostProcessKeywordState
    {
        readonly Material material;
        readonly Shader shader;
        readonly string[] names, globals, locals, allLocals;
        readonly Camera camera;
        readonly long renderSerial, selectedSerial;
        public ReadOnlyCollection<string> GlobalKeywords { get; }
        public ReadOnlyCollection<string> MaterialKeywords { get; }
        OriginalPostProcessKeywordState(OriginalCameraFrame frame, Material source, string[] declared)
        {
            material = source; shader = source.shader; names = declared;
            camera = frame.Source; renderSerial = frame.RenderSerial; selectedSerial = frame.SelectedSerial;
            globals = names.Where(Shader.IsKeywordEnabled).ToArray();
            allLocals = OriginalPostProcessProgramSelection.Snapshot(source.shaderKeywords);
            var space = new HashSet<string>(names, StringComparer.Ordinal);
            locals = allLocals.Where(space.Contains).ToArray();
            GlobalKeywords = Array.AsReadOnly(globals); MaterialKeywords = Array.AsReadOnly(locals);
        }
        public static OriginalPostProcessKeywordState Capture(OriginalCameraFrame frame, Material material,
            IEnumerable<string> declaredKeywords)
        {
            if (!frame.Source || frame.SelectedSerial <= 0 || frame.RenderSerial <= 0 || !material || !material.shader)
                throw new ArgumentException("A current camera update and source material are required.");
            return new OriginalPostProcessKeywordState(frame, material, OriginalPostProcessProgramSelection.Snapshot(declaredKeywords));
        }
        public void ValidateCurrent(OriginalCameraFrame frame)
        {
            if (!camera || frame.Source != camera || frame.RenderSerial != renderSerial || frame.SelectedSerial != selectedSerial ||
                !material || material.shader != shader || !allLocals.SequenceEqual(OriginalPostProcessProgramSelection.Snapshot(material.shaderKeywords)) ||
                !globals.SequenceEqual(names.Where(Shader.IsKeywordEnabled)))
                throw new InvalidOperationException("Keyword state is stale, foreign, or changed before consumption.");
        }
    }

    public sealed class OriginalLivePostFilmProgramProvider
    {
        public sealed class Observation
        {
            public readonly bool FilmDraw;
            public readonly int SourcePass;
            public readonly OriginalPostProcessProgramSelection.Decision Selection;
            public readonly OriginalPostProcessKeywordState Keywords;
            public readonly OriginalLiveTimelineBridge.FilmSelection Film;
            readonly OriginalLiveTimelineBridge.Frame selected;
            readonly Func<OriginalLiveTimelineBridge.Frame> current;
            internal Observation(OriginalLiveTimelineBridge.Frame selected, Func<OriginalLiveTimelineBridge.Frame> current,
                OriginalLiveTimelineBridge.FilmSelection film, OriginalPostProcessKeywordState keywords,
                bool draw, int pass, OriginalPostProcessProgramSelection.Decision selection)
            { this.selected = selected; this.current = current; Film = film; Keywords = keywords; FilmDraw = draw; SourcePass = pass; Selection = selection; }
            public void ValidateCurrent(OriginalCameraFrame frame)
            {
                if (!ReferenceEquals(selected, current()) || selected.Serial != frame.SelectedSerial || Film.Camera != frame.Source)
                    throw new InvalidOperationException("Program observation belongs to a different selected update.");
                Keywords.ValidateCurrent(frame);
            }
        }
        readonly Func<OriginalLiveTimelineBridge.Frame> current;
        readonly Func<int, OriginalPostProcessProgramSelection> catalog;
        readonly Material source;
        readonly Shader sourceShader;
        readonly string[] declared;
        readonly int layer, basePass;
        public OriginalLivePostFilmProgramProvider(Func<OriginalLiveTimelineBridge.Frame> current,
            int layer, int basePass, Material source, IEnumerable<string> declaredKeywords,
            Func<int, OriginalPostProcessProgramSelection> catalog)
        {
            this.current = current ?? throw new ArgumentNullException(nameof(current));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (layer < 1 || layer > 3 || basePass < 0 || basePass == int.MaxValue || !source || !source.shader)
                throw new ArgumentException("Explicit layer, source pass and material are required.");
            this.layer = layer; this.basePass = basePass; this.source = source; sourceShader = source.shader;
            declared = OriginalPostProcessProgramSelection.Snapshot(declaredKeywords);
        }
        public Observation Resolve(OriginalCameraFrame frame)
        {
            var selected = current();
            if (!source || source.shader != sourceShader || selected == null || selected.Serial != frame.SelectedSerial || !frame.Source ||
                !selected.TryGetFilm(frame.Source, layer, out var film) || film.Serial != selected.Serial || !film.Value.HasValue)
                throw new InvalidOperationException("Current selected Film, camera and layer are required.");
            var value = film.Value.Value;
            if ((int)value.layerMode != 0 || (int)value.filmMode < 0 || (int)value.filmMode > 7 ||
                (int)value.colorType < 0 || (int)value.colorType > 3)
                throw new NotSupportedException("This Film route has no qualified program provider.");
            if (float.IsNaN(value.filmPower) || float.IsInfinity(value.filmPower) || float.IsNaN(value.color0.a) || float.IsInfinity(value.color0.a))
                throw new ArgumentException("Finite Film validity inputs are required.");
            var keywords = OriginalPostProcessKeywordState.Capture(frame, source, declared);
            var parameter = new ScreenOverlayRender.Parameter { PostFilmMode = (Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode)value.filmMode,
                PostFilmPower = value.filmPower, PostFilmColor0 = value.color0 };
            if (!parameter.IsValidity) return new Observation(selected, current, film, keywords, false, -1, null);
            int pass = checked(basePass + (value.inverseVignette ? 1 : 0));
            var group = catalog(pass) ?? throw new NotSupportedException("Source pass has no registered program table.");
            if (group.SourcePass != pass || !group.DeclaredKeywords.SequenceEqual(declared))
                throw new InvalidOperationException("Program table belongs to a different pass or keyword space.");
            var effective = new HashSet<string>(keywords.GlobalKeywords, StringComparer.Ordinal);
            ScreenOverlayRender.Render.VisitNormalFilmKeywordCommands((int)value.filmMode,
                (name, enable) => { if (enable) effective.Add(name); else effective.Remove(name); });
            effective.UnionWith(keywords.MaterialKeywords);
            var decision = group.Select(effective);
            var observation = new Observation(selected, current, film, keywords, true, pass, decision);
            observation.ValidateCurrent(frame); return observation;
        }
    }
}
