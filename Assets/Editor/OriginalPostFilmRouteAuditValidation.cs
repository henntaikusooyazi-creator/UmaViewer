using System;
using System.IO;
using System.Linq;
using Gallop.Live.Cutt;
using UnityEditor;
using UnityEngine;

public static class OriginalPostFilmRouteAuditValidation
{
    [Serializable] sealed class Report { public int checks; public string failure; public bool artificial = true; }
    public static void Run()
    {
        var report = new Report(); int exit = 0;
        Action<bool> check = v => { report.checks++; if (!v) throw new Exception("Film route audit assertion " + report.checks); };
        Action<Action> reject = a => { bool caught = false; try { a(); } catch (ArgumentException) { caught = true; } catch (NotSupportedException) { caught = true; } check(caught); };
        try
        {
            var value = new PostFilmUpdateInfo { filmPower = 1, color0 = Color.white };
            string[] expected = { null, "MODE_LERP", "MODE_ADD", "MODE_MUL", "MODE_VIGNETTE_LERP", "MODE_VIGNETTE_ADD", "MODE_VIGNETTE_MUL", "MODE_MONOCHROME" };
            for (int mode = 0; mode < expected.Length; mode++)
            {
                value.filmMode = (PostFilmMode)mode;
                var route = OriginalPostFilmRouteAudit.Capture(value, 40);
                check(route.filmDraw == (mode != 0));
                check(route.enabledMode == expected[mode]);
                if (mode == 0) { check(route.pass == -1 && route.disabledModes.Length == 0); continue; }
                check(route.pass == 40 && !route.runtime_contract && !route.OriginalVerified);
                check(!route.disabledModes.Contains(expected[mode]) && route.disabledModes.Contains("MODE_NONE"));
                check(route.retainedBlendSlot == "MASK_VIGNETTE" && !route.disabledBlendKeywords.Contains(route.retainedBlendSlot));
                value.inverseVignette = true;
                check(OriginalPostFilmRouteAudit.Capture(value, 40).pass == 41); value.inverseVignette = false;
            }
            value.filmMode = PostFilmMode.Add; value.filmPower = 0;
            check(!OriginalPostFilmRouteAudit.Capture(value, 40).filmDraw);
            value.filmMode = PostFilmMode.Mul;
            check(OriginalPostFilmRouteAudit.Capture(value, 40).filmDraw);
            value.filmMode = PostFilmMode.Monochrome; value.color0 = Color.clear;
            check(!OriginalPostFilmRouteAudit.Capture(value, 40).filmDraw);
            value.color0 = Color.white; check(OriginalPostFilmRouteAudit.Capture(value, 40).filmDraw);
            value.layerMode = LiveTimelineKeyPostFilmData.LayerMode.UVMovie;
            reject(() => OriginalPostFilmRouteAudit.Capture(value, 40)); value.layerMode = 0;
            value.filmMode = (PostFilmMode)99; reject(() => OriginalPostFilmRouteAudit.Capture(value, 40)); value.filmMode = PostFilmMode.Add;
            value.colorType = (PostColorType)99; reject(() => OriginalPostFilmRouteAudit.Capture(value, 40)); value.colorType = 0;
            value.filmPower = float.NaN; reject(() => OriginalPostFilmRouteAudit.Capture(value, 40)); value.filmPower = 1;
            value.color0 = new Color(1, 1, 1, float.PositiveInfinity); reject(() => OriginalPostFilmRouteAudit.Capture(value, 40)); value.color0 = Color.white;
            reject(() => OriginalPostFilmRouteAudit.Capture(value, -1)); reject(() => OriginalPostFilmRouteAudit.Capture(value, int.MaxValue));
            OriginalPostFilmKeywordValidation.Check(check);
        }
        catch (Exception e) { report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally
        {
            var a = Environment.GetCommandLineArgs(); int at = Array.IndexOf(a, "-integrationOutput");
            if (at < 0 || at + 1 >= a.Length) throw new ArgumentException("External output required");
            Directory.CreateDirectory(a[at + 1]); File.WriteAllText(Path.Combine(a[at + 1], "route-validation.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit);
        }
    }
}
