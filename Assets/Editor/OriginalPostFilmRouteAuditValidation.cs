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
            var effective = new[] { "FX_B", "FX_A" };
            var lookup = new[] { "FX_A", "FX_B" };
            var selected = new[] { "FX_A", "FX_B", "OPTION_C" };
            var declaration = new[] { "OPTION_C", "FX_B", "FX_A" };
            var comparison = OriginalPostFilmRouteAudit.CompareProgramKeywords(effective, lookup, selected, declaration);
            check(comparison.effectiveMatchesLookupKey && !comparison.lookupKeyMatchesSelectedProgram && comparison.selectedProgramMatchesDeclaration);
            check(comparison.effectiveOnly.Length == 0 && comparison.lookupOnly.Length == 0 && comparison.lookupUnselected.Length == 0);
            check(comparison.selectedUnrequested.SequenceEqual(new[] { "OPTION_C" }) && comparison.declarationOnly.Length == 0 && comparison.selectedUndeclared.Length == 0);
            check(!comparison.runtime_contract && !comparison.OriginalVerified);
            effective[0] = lookup[0] = selected[0] = declaration[0] = "CHANGED";
            check(comparison.effective[0] == "FX_A" && comparison.lookupKey[0] == "FX_A" && comparison.selectedKey[0] == "FX_A" && comparison.declaration[0] == "FX_A");
            comparison = OriginalPostFilmRouteAudit.CompareProgramKeywords(new[] { "FX_A" }, new[] { "FX_B" }, new[] { "FX_C" }, new[] { "FX_D" });
            check(!comparison.effectiveMatchesLookupKey && !comparison.lookupKeyMatchesSelectedProgram && !comparison.selectedProgramMatchesDeclaration);
            check(comparison.effectiveOnly.SequenceEqual(new[] { "FX_A" }) && comparison.lookupOnly.SequenceEqual(new[] { "FX_B" }));
            check(comparison.lookupUnselected.SequenceEqual(new[] { "FX_B" }) && comparison.selectedUnrequested.SequenceEqual(new[] { "FX_C" }));
            check(comparison.declarationOnly.SequenceEqual(new[] { "FX_D" }) && comparison.selectedUndeclared.SequenceEqual(new[] { "FX_C" }));
            var empty = Array.Empty<string>();
            check(OriginalPostFilmRouteAudit.CompareProgramKeywords(empty, empty, empty, empty).selectedProgramMatchesDeclaration);
            check(!OriginalPostFilmRouteAudit.CompareProgramKeywords(new[] { "FX" }, new[] { "fx" }, empty, empty).effectiveMatchesLookupKey);
            reject(() => OriginalPostFilmRouteAudit.CompareProgramKeywords(null, lookup, selected, declaration));
            reject(() => OriginalPostFilmRouteAudit.CompareProgramKeywords(effective, null, selected, declaration));
            reject(() => OriginalPostFilmRouteAudit.CompareProgramKeywords(effective, lookup, null, declaration));
            reject(() => OriginalPostFilmRouteAudit.CompareProgramKeywords(effective, lookup, selected, null));
            foreach (var bad in new[] { new string[] { null }, new[] { " " }, new[] { "A B" }, new[] { "FX", "FX" }, new[] { new string('X', 129) }, Enumerable.Range(0, 257).Select(i => "FX_" + i).ToArray() })
            {
                reject(() => OriginalPostFilmRouteAudit.CompareProgramKeywords(bad, empty, empty, empty));
                reject(() => OriginalPostFilmRouteAudit.CompareProgramKeywords(empty, bad, empty, empty));
                reject(() => OriginalPostFilmRouteAudit.CompareProgramKeywords(empty, empty, bad, empty));
                reject(() => OriginalPostFilmRouteAudit.CompareProgramKeywords(empty, empty, empty, bad));
            }
            var maximum = Enumerable.Range(0, 256).Select(i => "FX_" + i).ToArray();
            check(OriginalPostFilmRouteAudit.CompareProgramKeywords(maximum, maximum, maximum, maximum).effectiveMatchesLookupKey);
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
