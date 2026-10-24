using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gallop.ImageEffect;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;

// Explains current Viewer code for one evaluated Film. This does not select an
// original program, establish capability owners, or register a renderer.
public static class OriginalPostFilmRouteAudit
{
    [Serializable] public sealed class Route
    {
        public string scope = "CurrentViewerCodeOnly; explicit base pass; retained keywords unresolved";
        public bool filmDraw, inverse;
        public int pass = -1;
        public string enabledMode;
        public string[] disabledModes, disabledBlendKeywords;
        public string retainedBlendSlot;
        public bool runtime_contract = false, OriginalVerified = false;
    }
    static string[] Family(string name)
    {
        var field = typeof(ScreenOverlayRender.Render).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        if (!(field?.GetValue(null) is string[] values) || values.Length == 0)
            throw new InvalidOperationException("Current Viewer keyword family unavailable.");
        return (string[])values.Clone();
    }
    public static Route Capture(PostFilmUpdateInfo value, int basePass)
    {
        if (basePass < 0 || basePass == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(basePass));
        if ((int)value.layerMode != 0) throw new NotSupportedException("Movie bindings and retained state need separate evidence.");
        if ((int)value.filmMode < 0 || (int)value.filmMode > 7 || (int)value.colorType < 0 || (int)value.colorType > 3)
            throw new NotSupportedException("Unknown Film mode or color layout.");
        if (float.IsNaN(value.filmPower) || float.IsInfinity(value.filmPower) ||
            float.IsNaN(value.color0.a) || float.IsInfinity(value.color0.a))
            throw new ArgumentException("Finite evaluated validity inputs required.");
        var parameter = new ScreenOverlayRender.Parameter { PostFilmMode = (ScreenOverlay.Overlay.PostFilmMode)value.filmMode,
            PostFilmPower = value.filmPower, PostFilmColor0 = value.color0 };
        var route = new Route { filmDraw = parameter.IsValidity, inverse = value.inverseVignette,
            disabledModes = Array.Empty<string>(), disabledBlendKeywords = Array.Empty<string>() };
        if (!route.filmDraw) return route; // Caller chooses its separate default/copy path.
        var modes = Family("SHADER_KEYWORD_MODE"); var blend = Family("SHADER_KEYWORD_BLEND");
        int mode = (int)value.filmMode;
        if (mode >= modes.Length) throw new InvalidOperationException("Current mode family is incomplete.");
        route.pass = checked(basePass + (value.inverseVignette ? 1 : 0));
        route.enabledMode = mode > 0 ? modes[mode] : null;
        route.disabledModes = modes.Where((_, i) => i != mode).ToArray();
        route.disabledBlendKeywords = blend.Skip(1).ToArray();
        route.retainedBlendSlot = blend[0]; // SetShaderKeyword(0) does not clear slot zero.
        return route;
    }

    [Serializable] public sealed class GlobalTransition
    {
        public string scope = "CurrentViewerCommandBufferCodeOnly; explicit initial global keywords";
        public Route route;
        public string[] before, after;
        public bool runtime_contract = false, OriginalVerified = false;
    }

    // Apply records global keyword commands. Material.shaderKeywords alone
    // cannot supply their initial state or account for commands queued earlier.
    // This models just those commands; it never resolves Unity's variant.
    public static GlobalTransition CaptureGlobalTransition(PostFilmUpdateInfo value,
        int basePass, IEnumerable<string> initialGlobalKeywords)
    {
        if (initialGlobalKeywords == null) throw new ArgumentNullException(nameof(initialGlobalKeywords));
        var keywords = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        foreach (var keyword in initialGlobalKeywords)
        {
            if (++count > 256 || string.IsNullOrWhiteSpace(keyword) || keyword.Length > 128 ||
                keyword.Any(char.IsWhiteSpace) || !keywords.Add(keyword))
                throw new ArgumentException("A bounded, unique explicit global keyword set is required.", nameof(initialGlobalKeywords));
        }
        var route = Capture(value, basePass);
        var before = keywords.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        if (route.filmDraw)
        {
            keywords.ExceptWith(route.disabledModes);
            keywords.ExceptWith(route.disabledBlendKeywords);
            if (route.enabledMode != null) keywords.Add(route.enabledMode);
        }
        return new GlobalTransition { route = route, before = before,
            after = keywords.OrderBy(k => k, StringComparer.Ordinal).ToArray() };
    }
}
