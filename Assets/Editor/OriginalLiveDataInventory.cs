using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gallop.Live.Cutt;
using UnityEngine;

// Explicit editor audit of already loaded, typed data. No file parsing, loading,
// key selection, interpolation, runtime registration, or inferred shader binding.
public static class OriginalLiveDataInventory
{
    [Serializable] public sealed class KeyRow
    {
        // Frame/attribute are meaningful only when present is true.
        public int index, frame, attribute;
        public bool present;
        public string type, payload;
    }
    [Serializable] public sealed class Track
    {
        // A null list has present=false/count=-1; an empty list has count=0.
        public string path, type;
        public int count, attribute, playMode;
        public bool present, hasAuthoredCameraNo;
        public int authoredCameraNo;
        public List<KeyRow> keys = new List<KeyRow>();
    }
    [Serializable] public sealed class Worksheet
    {
        public int index, targetCameraIndex;
        public bool present, enableAtRuntime;
    }
    [Serializable] public sealed class Report
    {
        public string scope = "LoadedTypedDataOnly; imported schema; no runtime/GPU binding proof";
        public int worksheets, keyCount, payloadCharacters;
        public bool includesPayload;
        public List<Track> tracks = new List<Track>();
        public List<Worksheet> worksheetRows = new List<Worksheet>();
    }
    public static Report Capture(LiveTimelineData data, bool includeKeyPayload = false, int maxKeys = 100000)
    {
        if (!data || data.worksheetList == null || data.worksheetList.Count > 64)
            throw new ArgumentException("Explicit loaded data and bounded worksheets required.");
        if (maxKeys < 1 || maxKeys > 100000) throw new ArgumentOutOfRangeException(nameof(maxKeys));
        var report = new Report { worksheets = data.worksheetList.Count, includesPayload = includeKeyPayload };
        for (int i = 0; i < data.worksheetList.Count; i++)
        {
            var sheet = data.worksheetList[i];
            report.worksheetRows.Add(new Worksheet { index = i, present = !!sheet,
                targetCameraIndex = sheet ? sheet.targetCameraIndex : 0, enableAtRuntime = sheet && sheet.enableAtRuntime });
            Walk(data.worksheetList[i], typeof(LiveTimelineWorkSheet), "worksheet[" + i + "]", 0,
                new List<object>(), report, maxKeys);
        }
        return report;
    }
    static void Walk(object value, Type declared, string path, int depth, List<object> ancestors, Report report, int cap)
    {
        if (depth > 16) throw new InvalidOperationException("Typed inventory depth bound.");
        if (value == null)
        {
            if (typeof(ILiveTimelineKeyDataList).IsAssignableFrom(declared))
                Add(report, new Track { path = path, type = declared.FullName, count = -1 });
            return;
        }
        if (value is ILiveTimelineKeyDataList list)
        {
            int count = list.Count;
            if (count < 0 || count > cap - report.keyCount) throw new InvalidOperationException("Typed key bound.");
            var track = new Track { path = path, type = value.GetType().FullName, present = true,
                count = count, attribute = (int)list.attribute, playMode = (int)list.playMode };
            foreach (var ancestor in ancestors)
            {
                var camera = ancestor.GetType().GetField("MultiCameraNo", BindingFlags.Public | BindingFlags.Instance);
                if (camera?.FieldType != typeof(int)) continue;
                track.hasAuthoredCameraNo = true; track.authoredCameraNo = (int)camera.GetValue(ancestor);
            }
            for (int i = 0; i < count; i++)
            {
                var key = list[i];
                string payload = key != null && report.includesPayload ? JsonUtility.ToJson(key) : null;
                if (payload != null && payload.Length > 65536) throw new InvalidOperationException("Typed key payload bound.");
                if (payload != null)
                {
                    if (payload.Length > 8 * 1024 * 1024 - report.payloadCharacters)
                        throw new InvalidOperationException("Total typed payload bound.");
                    report.payloadCharacters += payload.Length;
                }
                track.keys.Add(new KeyRow { index = i, present = key != null, frame = key?.frame ?? 0,
                    attribute = key == null ? 0 : (int)key.attribute, type = key?.GetType().FullName, payload = payload });
            }
            report.keyCount += count; Add(report, track); return;
        }
        var type = value.GetType();
        if (value is string || type.IsPrimitive || type.IsEnum ||
            (value is UnityEngine.Object && !(value is LiveTimelineWorkSheet))) return;
        if (ancestors.Any(x => ReferenceEquals(x, value))) throw new InvalidOperationException("Typed inventory cycle.");
        ancestors.Add(value);
        try
        {
            if (value is IList sequence)
            {
                if (sequence.Count > 4096) throw new InvalidOperationException("Typed group bound.");
                var element = type.IsArray ? type.GetElementType() : type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
                for (int i = 0; i < sequence.Count; i++) Walk(sequence[i], element, path + "[" + i + "]", depth + 1, ancestors, report, cap);
            }
            else if (type.Namespace == typeof(LiveTimelineWorkSheet).Namespace)
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => !f.IsNotSerialized && (f.IsPublic || f.IsDefined(typeof(SerializeField), true)))
                    .OrderBy(f => f.Name, StringComparer.Ordinal))
                    Walk(field.GetValue(value), field.FieldType, path + "." + field.Name, depth + 1, ancestors, report, cap);
            }
        }
        finally { ancestors.RemoveAt(ancestors.Count - 1); }
    }
    static void Add(Report report, Track track)
    {
        if (report.tracks.Count >= 4096) throw new InvalidOperationException("Typed track bound.");
        report.tracks.Add(track);
    }
}
