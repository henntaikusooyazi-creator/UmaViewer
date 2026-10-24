using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gallop.Live.Cutt;
using UnityEditor;
using UnityEngine;

public static class OriginalLiveDataInventoryValidation
{
    [Serializable] sealed class Result { public int checks; public string failure; public bool artificial = true; }
    public static void Run()
    {
        var result = new Result(); var data = ScriptableObject.CreateInstance<LiveTimelineData>();
        var sheet = ScriptableObject.CreateInstance<LiveTimelineWorkSheet>(); int exit = 0;
        Action<bool> check = value => { result.checks++; if (!value) throw new Exception("Typed inventory assertion " + result.checks); };
        Action<Action> reject = action => { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } check(rejected); };
        try
        {
            reject(() => OriginalLiveDataInventory.Capture(null));
            reject(() => OriginalLiveDataInventory.Capture(data));
            data.worksheetList = new List<LiveTimelineWorkSheet>();
            check(OriginalLiveDataInventory.Capture(data).keyCount == 0);
            var keys = new LiveTimelineKeyPostFilmDataList();
            keys.thisList.Add(new LiveTimelineKeyPostFilmData { frame = 20, filmMode = PostFilmMode.Add, filmPower = .25f });
            keys.thisList.Add(null);
            keys.thisList.Add(new LiveTimelineKeyPostFilmData { frame = 10, filmPower = .75f });
            sheet.postFilmKeys = keys; sheet.postFilm2Keys = keys; sheet.targetCameraIndex = 3; sheet.enableAtRuntime = true;
            data.worksheetList.Add(sheet);
            var report = OriginalLiveDataInventory.Capture(data);
            check(report.keyCount == 6 && report.worksheets == 1 && !report.includesPayload);
            check(report.worksheetRows[0].present && report.worksheetRows[0].targetCameraIndex == 3 && report.worksheetRows[0].enableAtRuntime);
            var track = report.tracks.Single(x => x.path.EndsWith(".postFilmKeys"));
            check(track.present && track.count == 3 && track.keys.Select(x => x.frame).SequenceEqual(new[] { 20, 0, 10 }));
            check(!track.keys[1].present && track.keys[1].type == null);
            check(track.keys.All(x => x.payload == null));
            check(report.tracks.Single(x => x.path.EndsWith(".postFilm3Keys")).count == -1);
            check(keys.TimeKeyIndex.key == null && keys.thisList[0].frame == 20);
            var detailed = OriginalLiveDataInventory.Capture(data, true);
            check(detailed.tracks.Single(x => x.path.EndsWith(".postFilmKeys")).keys[0].payload.Contains("filmPower"));
            check(JsonUtility.FromJson<OriginalLiveDataInventory.Report>(JsonUtility.ToJson(detailed)).keyCount == 6);
            reject(() => OriginalLiveDataInventory.Capture(data, false, 5));
            reject(() => OriginalLiveDataInventory.Capture(data, false, 0));
            reject(() => OriginalLiveDataInventory.Capture(data, false, 100001));
            var nested = new LiveTimelineKeyMultiCameraPostFilmDataList();
            nested.thisList.Add(new LiveTimelineKeyPostFilmData { frame = 90 });
            sheet.postFilm1MultiCameraKeys = new List<LiveTimelineMultiCameraPostFilmData> {
                new LiveTimelineMultiCameraPostFilmData { MultiCameraNo = 7, keys = nested } };
            var grouped = OriginalLiveDataInventory.Capture(data);
            check(grouped.keyCount == 7);
            check(grouped.tracks.Single(x => x.path.EndsWith(".postFilm1MultiCameraKeys[0].keys")).keys[0].frame == 90);
            check(grouped.tracks.Single(x => x.path.EndsWith(".postFilm1MultiCameraKeys[0].keys")).hasAuthoredCameraNo &&
                grouped.tracks.Single(x => x.path.EndsWith(".postFilm1MultiCameraKeys[0].keys")).authoredCameraNo == 7);
            check(!grouped.tracks.Single(x => x.path.EndsWith(".postFilmKeys")).hasAuthoredCameraNo);
            data.worksheetList = Enumerable.Repeat(sheet, 65).ToList();
            reject(() => OriginalLiveDataInventory.Capture(data));
            data.worksheetList = new List<LiveTimelineWorkSheet> { sheet };
            var large = new LiveTimelineKeyPostFilmDataList();
            large.thisList.Add(new LiveTimelineKeyPostFilmData { BlinkLightName = new string('x', 65537) });
            sheet.postFilmKeys = large; sheet.postFilm2Keys = null; sheet.postFilm1MultiCameraKeys = null;
            reject(() => OriginalLiveDataInventory.Capture(data, true));
            large.thisList.Clear();
            var key = new LiveTimelineKeyPostFilmData { BlinkLightName = new string('x', 60000) };
            for (int i = 0; i < 150; i++) large.thisList.Add(key);
            reject(() => OriginalLiveDataInventory.Capture(data, true));
        }
        catch (Exception e) { result.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(sheet);
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-integrationOutput");
            if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("Explicit output required");
            Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], "inventory-validation.json"), JsonUtility.ToJson(result, true));
            EditorApplication.Exit(exit);
        }
    }
}
