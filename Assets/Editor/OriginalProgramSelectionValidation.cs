using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gallop.Live.Cutt;
using Gallop.Live.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Selector = Gallop.Live.Rendering.OriginalPostProcessProgramSelection;

public static class OriginalProgramSelectionValidation
{
    [Serializable] sealed class Report { public int checks, pixels, selections, cacheHits; public string status, failure; public bool artificial = true; }
    static Report report;
    static readonly List<Object> owned = new List<Object>();
    static readonly string[] space = { "COLOR_ONLY", "MODE_LERP", "MODE_VIGNETTE_ADD" };
    static T Own<T>(T v) where T : Object { owned.Add(v); return v; }
    static void Check(bool v, string why) { report.checks++; if (!v) throw new InvalidOperationException(why); }
    static void Reject<T>(Action a) where T : Exception { bool caught = false; try { a(); } catch (T) { caught = true; } Check(caught, "Expected rejection " + typeof(T).Name); }
    static Selector.Candidate Row(string id, bool eligible, params string[] keys) => new Selector.Candidate(id, keys, eligible);
    static Selector Table(int pass = 17) => new Selector(pass, space, space, new[] {
        Row("soft", true, "MODE_VIGNETTE_ADD", "COLOR_ONLY"), Row("bright", true, "MODE_LERP") }, new Selector.Profile(2, 7), 16);
    static void MathChecks()
    {
        var table = Table(); var first = table.Select(new[] { "MODE_VIGNETTE_ADD" });
        Check(first.Program.Id == "soft" && first.Score == -5 && !first.CacheHit, "Explicit profile permits an unrequested candidate keyword.");
        Check(first.Requested.SequenceEqual(new[] { "MODE_VIGNETTE_ADD" }) && first.Program.Keywords.Contains("COLOR_ONLY"), "Candidate declaration never enables a requested bit.");
        Check(table.Select(new[] { "FOREIGN", "MODE_VIGNETTE_ADD" }).CacheHit && table.CacheCount == 1, "Namespace projection and canonical cache key.");
        Check(table.Select(new[] { "MODE_LERP" }).Program.Id == "bright", "Current keyword change selects another program.");
        var filtered = new Selector(0, space, new[] { "MODE_VIGNETTE_ADD" }, new[] { Row("filtered", true, "MODE_VIGNETTE_ADD") }, new Selector.Profile(1, 3), 2);
        filtered.Select(new[] { "MODE_VIGNETTE_ADD" });
        Check(filtered.Select(new[] { "MODE_VIGNETTE_ADD", "MODE_LERP" }).CacheHit && filtered.CacheCount == 1, "Pass normalization precedes cache lookup.");
        Reject<ArgumentException>(() => new Selector(0, space, new[] { "FOREIGN" }, new[] { Row("x", true) }, new Selector.Profile(1, 2), 1));
        Reject<ArgumentException>(() => new Selector(0, space, new[] { "MODE_LERP" }, new[] { Row("x", true, "COLOR_ONLY") }, new Selector.Profile(1, 2), 1));
        Reject<ArgumentNullException>(() => new Selector(0, space, null, new[] { Row("x", true) }, new Selector.Profile(1, 2), 1));
        var tie = new Selector(0, space, space, new[] { Row("excluded", false), Row("first", true), Row("second", true) }, new Selector.Profile(3, 5), 2);
        Check(tie.Select(Array.Empty<string>()).Program.Id == "first", "Excluded rows are skipped; equal score retains ordered first.");
        tie.Select(new[] { "COLOR_ONLY" }); Reject<InvalidOperationException>(() => tie.Select(new[] { "MODE_LERP" }));
        Check(tie.CacheCount == 2 && tie.Select(Array.Empty<string>()).CacheHit, "A full cache keeps hits but does not silently evict.");
        var none = new Selector(0, space, space, new[] { Row("off", false) }, new Selector.Profile(1, 3), 1);
        Reject<NotSupportedException>(() => none.Select(Array.Empty<string>())); Check(none.CacheCount == 0, "Failed selection is not cached.");
        var input = new[] { "MODE_LERP" }; var row = new Selector.Candidate("copy", input, true); input[0] = "CHANGED";
        Check(row.Keywords[0] == "MODE_LERP", "Caller mutation cannot alter candidates.");
        Reject<ArgumentException>(() => new Selector.Candidate("bad id", Array.Empty<string>(), true));
        foreach (var bad in new[] { new string[] { null }, new[] { " " }, new[] { "A B" }, new[] { "A\0B" }, new[] { "A", "A" }, new[] { new string('x', 129) }, Enumerable.Range(0, 257).Select(i => "K" + i).ToArray() })
        { Reject<ArgumentException>(() => new Selector.Candidate("bad", bad, true)); Reject<ArgumentException>(() => table.Select(bad)); }
        Reject<ArgumentNullException>(() => table.Select(null)); Reject<ArgumentNullException>(() => new Selector.Candidate("bad", null, true));
        Reject<ArgumentOutOfRangeException>(() => new Selector.Profile(0, 1)); Reject<ArgumentOutOfRangeException>(() => new Selector.Profile(1, -1));
        Reject<ArgumentException>(() => new Selector(0, space, space, new[] { Row("x", true, "FOREIGN") }, new Selector.Profile(1, 2), 1));
        Reject<ArgumentException>(() => new Selector(0, space, space, new[] { Row("x", true), Row("x", true) }, new Selector.Profile(1, 2), 1));
        Reject<ArgumentException>(() => new Selector(0, space, space, Array.Empty<Selector.Candidate>(), new Selector.Profile(1, 2), 1));
        Reject<ArgumentOutOfRangeException>(() => new Selector(-1, space, space, new[] { Row("x", true) }, new Selector.Profile(1, 2), 1));
        Reject<ArgumentOutOfRangeException>(() => new Selector(0, space, space, new[] { Row("x", true) }, new Selector.Profile(1, 2), 257));
        var many = Enumerable.Range(0, 129).Select(i => Row("p" + i, true));
        Reject<ArgumentException>(() => new Selector(0, space, space, many, new Selector.Profile(1, 2), 1));
        var extreme = new Selector(0, space, space, new[] { Row("large", true, space) }, new Selector.Profile(int.MaxValue, int.MaxValue), 1);
        Check(extreme.Select(space).Score == 3L * int.MaxValue, "Scoring does not wrap at int32.");
        foreach (int mode in Enumerable.Range(1, 7))
        {
            var initial = new[] { "MODE_NONE", "MODE_SCREENBLEND", "MASK_VIGNETTE", "BLEND_ADD", "COLOR_ONLY", "UNRELATED" };
            var value = new PostFilmUpdateInfo { filmMode = (PostFilmMode)mode, filmPower = 1, color0 = Color.white };
            var legacy = OriginalPostFilmRouteAudit.CaptureGlobalTransition(value, 17, initial);
            var current = new HashSet<string>(initial, StringComparer.Ordinal);
            Gallop.RenderPipeline.ScreenOverlayRender.Render.VisitNormalFilmKeywordCommands(mode, (name, enable) => { if (enable) current.Add(name); else current.Remove(name); });
            Check(current.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(legacy.after), "Runtime description preserves the established keyword command semantics.");
        }
    }
    sealed class Probe : IOriginalCameraFramePostProcessor
    {
        public Func<OriginalCameraFrame, RenderTexture> Callback;
        public RenderTexture Process(OriginalCameraFrame frame) => Callback(frame);
    }
    static byte[] Read(RenderTexture rt) { var r = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32); r.WaitForCompletion(); if (r.hasError) throw new Exception("GPU readback"); return r.GetData<byte>().ToArray(); }
    static void Pipeline(bool urp)
    {
        if (urp) { var data = Own(ScriptableObject.CreateInstance<UniversalRendererData>()); var asset = Own(UniversalRenderPipelineAsset.Create(data)); asset.msaaSampleCount = 1; asset.supportsHDR = false; GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset; }
        else { GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null; }
        var go = Own(new GameObject("Synthetic.Program.Control")); go.SetActive(false); var control = go.AddComponent<LiveTimelineControl>();
        var camera = Own(new GameObject("Synthetic.Program.Camera")).AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var source = Own(new Material(Shader.Find("Hidden/OriginalRendering/FilmKeywordValidation")));
        var shader = Shader.Find("Hidden/OriginalRendering/PostProcessValidation");
        var soft = Own(new Material(shader)); soft.SetVector("_Offset", new Vector4(.25f, .5f, .75f, 1));
        var bright = Own(new Material(shader)); bright.SetVector("_Offset", new Vector4(.75f, .25f, .125f, 1));
        var softProgram = new OriginalPostProcessChain.Program(soft, 0); var brightProgram = new OriginalPostProcessChain.Program(bright, 0);
        var bridge = new OriginalLiveTimelineBridge(); bridge.UseConfirmedOwnerClock(50);
        bridge.UseConfirmedPostFilm(new OriginalPostFilmMath.ConfirmedProfile(50, 1, 2, 2, Vector2.one, Vector2.one, true)); control.OriginalCompositeBridge = bridge;
        var keys = new LiveTimelineKeyPostFilmDataList();
        keys.thisList.Add(new LiveTimelineKeyPostFilmData { frame = 0, filmMode = PostFilmMode.VignetteAdd, filmPower = 1, color0 = Color.white });
        keys.thisList.Add(new LiveTimelineKeyPostFilmData { frame = 100, filmMode = PostFilmMode.Lerp, filmPower = 1, color0 = Color.white, interpolateType = LiveCameraInterpolateType.Linear });
        keys.thisList.Add(new LiveTimelineKeyPostFilmData { frame = 200, filmMode = PostFilmMode.Lerp, filmPower = 1, color0 = Color.white, attribute = (LiveTimelineKeyAttribute)0x100000 });
        keys.thisList.Add(new LiveTimelineKeyPostFilmData { frame = 300, filmMode = PostFilmMode.None, filmPower = 0, color0 = Color.white });
        var capture = typeof(LiveTimelineControl).GetMethod("CapturePostFilm", BindingFlags.NonPublic | BindingFlags.Instance);
        var groups = new Dictionary<int, Selector> { { 17, Table(17) }, { 18, Table(18) } };
        var entries = new[] {
            new OriginalPostProcessProgramRegistry.Registration(groups[17], "soft", softProgram),
            new OriginalPostProcessProgramRegistry.Registration(groups[17], "bright", brightProgram),
            new OriginalPostProcessProgramRegistry.Registration(groups[18], "soft", brightProgram),
            new OriginalPostProcessProgramRegistry.Registration(groups[18], "bright", brightProgram) };
        var registry = new OriginalPostProcessProgramRegistry(entries);
        entries[0] = null;
        Check(registry.Count == 4 && registry.Resolve(groups[17].Select(new[] { "MODE_VIGNETTE_ADD" })) == softProgram &&
            registry.Resolve(groups[18].Select(new[] { "MODE_VIGNETTE_ADD" })) == brightProgram, "Identical IDs in different passes resolve independently and caller array mutation has no effect.");
        Reject<NotSupportedException>(() => registry.Resolve(Table(17).Select(new[] { "MODE_VIGNETTE_ADD" })));
        Reject<ArgumentNullException>(() => registry.Resolve(null));
        Reject<ArgumentException>(() => new OriginalPostProcessProgramRegistry.Registration(groups[17], "foreign", softProgram));
        Reject<ArgumentNullException>(() => new OriginalPostProcessProgramRegistry.Registration(null, "soft", softProgram));
        Reject<ArgumentNullException>(() => new OriginalPostProcessProgramRegistry.Registration(groups[17], "soft", null));
        Reject<ArgumentNullException>(() => new OriginalPostProcessProgramRegistry(null));
        Reject<ArgumentException>(() => new OriginalPostProcessProgramRegistry(Array.Empty<OriginalPostProcessProgramRegistry.Registration>()));
        Reject<ArgumentException>(() => new OriginalPostProcessProgramRegistry(new[] { entries[1], entries[1] }));
        Reject<ArgumentException>(() => new OriginalPostProcessProgramRegistry(new OriginalPostProcessProgramRegistry.Registration[] { null }));
        var partial = new OriginalPostProcessProgramRegistry(new[] { entries[1] });
        Reject<NotSupportedException>(() => partial.Resolve(groups[17].Select(new[] { "MODE_VIGNETTE_ADD" })));
        Reject<ArgumentException>(() => new OriginalPostProcessProgramRegistry(Enumerable.Range(0, 129).Select(i =>
            new OriginalPostProcessProgramRegistry.Registration(Table(i), "soft", softProgram))));
        Check(groups[17].Select(new[] { "MODE_LERP" }).SourcePass == 17, "Miss and hit decisions preserve the originating pass.");
        var provider = new OriginalLivePostFilmProgramProvider(() => bridge.LastFrame, 1, 17, source, space, pass => groups.TryGetValue(pass, out var group) ? group : null);
        OriginalLivePostFilmProgramProvider.Observation observation = null, first = null; OriginalCameraFrame current = default, firstFrame = default;
        int resolutions = 0, renders = 0; byte[] firstPixels = null, changedPixels = null;
        using (var chain = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(() =>
        {
            resolutions++; observation.ValidateCurrent(current);
            return registry.Resolve(observation.Selection);
        }, 32, 16, null, new OriginalPostProcessChain.Binding("_MainTex", -1))))
        using (var producer = new OriginalCameraProducer(32, 16))
        {
            var probe = new Probe { Callback = frame =>
            {
                if (renders == 4)
                {
                    int cacheCount = groups[17].CacheCount + groups[18].CacheCount;
                    var noFilm = provider.Resolve(frame); noFilm.ValidateCurrent(frame);
                    Check(!noFilm.FilmDraw && noFilm.Selection == null && noFilm.SourcePass == -1 && cacheCount == groups[17].CacheCount + groups[18].CacheCount, "Film-none does not invent a default program or cache entry.");
                    renders++; return frame.Color;
                }
                current = frame; observation = provider.Resolve(frame); observation.ValidateCurrent(frame); report.selections++; if (observation.Selection.CacheHit) report.cacheHits++;
                Check(observation.FilmDraw && observation.SourcePass == (renders == 3 ? 18 : 17), "Natural Film inverse selects the explicit source pass.");
                string id = renders < 2 ? "soft" : "bright"; Check(observation.Selection.Program.Id == id, "Actual Control Film drives program selection.");
                var output = chain.Process(frame.Source, frame.Color);
                using (var reference = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(id == "soft" ? soft : bright, 0, 32, 16, null, new OriginalPostProcessChain.Binding("_MainTex", -1))))
                {
                    var actual = Read(output); var expected = Read(reference.Process(frame.Source, frame.Color));
                    Check(actual.SequenceEqual(expected), "Selected program matches an independent fixed-program GPU draw."); report.pixels += actual.Length / 4;
                    if (renders == 0) { firstPixels = actual; first = observation; firstFrame = frame; } if (renders == 2) changedPixels = actual;
                }
                if (renders == 1) Check(observation.Selection.CacheHit, "Repeated current request uses the same catalog cache.");
                renders++; return output;
            } };
            producer.SetInputFramePostProcessor(probe, Shader.Find("Hidden/OriginalRendering/DepthCopy"));
            foreach (int frame in new[] { 0, 1, 100, 200, 300 })
            {
                bridge.CaptureOwnerTimes(3 + frame, 2 + frame); bridge.BeginFrame(frame);
                bridge.CaptureSelected(0, frame, new LiveTimelineKeyMultiCameraPositionData(), null, null, camera);
                bridge.CapturePostEffect(default); capture.Invoke(control, new object[] { keys, frame, 1, camera, 17 }); bridge.CompleteFrame();
                producer.RenderInput(camera, bridge.LastFrame.Serial);
            }
            Check(chain.ProcessCount == 4 && resolutions == 4 && !firstPixels.SequenceEqual(changedPixels), "Exactly one resolution per draw and visible RGB changes.");
            Reject<InvalidOperationException>(() => first.ValidateCurrent(firstFrame));
            var snapshot = OriginalPostProcessKeywordState.Capture(current, source, space);
            source.EnableKeyword("COLOR_ONLY"); Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(current)); source.DisableKeyword("COLOR_ONLY");
            snapshot = OriginalPostProcessKeywordState.Capture(current, source, space);
            Shader.EnableKeyword("COLOR_ONLY"); Reject<InvalidOperationException>(() => snapshot.ValidateCurrent(current)); Shader.DisableKeyword("COLOR_ONLY");
            var before = RenderTexture.active; bool srgb = GL.sRGBWrite; int count = chain.ProcessCount;
            Reject<InvalidOperationException>(() => chain.Process(camera, current.Color));
            Check(chain.ProcessCount == count && RenderTexture.active == before && GL.sRGBWrite == srgb, "Foreign keyword state has no stale draw and restores render state.");
            producer.SetInputFramePostProcessor(null, null);
        }
        using (var missing = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(() => null, 32, 16, null)))
        {
            var input = Own(new RenderTexture(32, 16, 0)); input.Create(); var before = RenderTexture.active;
            Reject<InvalidOperationException>(() => missing.Process(camera, input)); Check(missing.ProcessCount == 0 && RenderTexture.active == before, "Missing registered program does not advance success."); input.Release();
        }
        var guardedInput = Own(new RenderTexture(32, 16, 0)); guardedInput.Create();
        var guardedProgram = new OriginalPostProcessChain.Program(source, 0);
        using (var guarded = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(() => guardedProgram, 32, 16, null)))
        {
            Reject<InvalidOperationException>(() => guarded.Process(camera, guardedInput, (stage, material) => material.EnableKeyword("COLOR_ONLY"), null));
            Check(guarded.ProcessCount == 0, "Prepare cannot replace the registered program keyword state."); source.DisableKeyword("COLOR_ONLY");
        }
        using (var guarded = new OriginalPostProcessChain(new OriginalPostProcessChain.Pass(() => guardedProgram, 32, 16, null,
            new OriginalPostProcessChain.Binding("_MainTex", () => { source.EnableKeyword("COLOR_ONLY"); return guardedInput; }))))
        {
            var before = RenderTexture.active; bool srgb = GL.sRGBWrite;
            Reject<InvalidOperationException>(() => guarded.Process(camera, guardedInput));
            Check(guarded.ProcessCount == 0 && RenderTexture.active == before && GL.sRGBWrite == srgb, "Binding resolution cannot replace a registered program before draw."); source.DisableKeyword("COLOR_ONLY");
        }
        guardedInput.Release();
    }
    public static void Run()
    {
        var a = Environment.GetCommandLineArgs(); int at = Array.IndexOf(a, "-integrationOutput"); if (at < 0) throw new ArgumentException("External output required"); string output = a[at + 1]; Directory.CreateDirectory(output);
        report = new Report(); var g = GraphicsSettings.defaultRenderPipeline; var q = QualitySettings.renderPipeline; var active = RenderTexture.active; bool srgb = GL.sRGBWrite; var globals = space.ToDictionary(k => k, Shader.IsKeywordEnabled); int exit = 0;
        try { foreach (var name in space) Shader.DisableKeyword(name); MathChecks(); Pipeline(false); Pipeline(true); report.status = "CurrentFilmProgramSelectionValidated_TestOnly"; }
        catch (Exception e) { report.status = "Failed"; report.failure = e.ToString(); Debug.LogException(e); exit = 1; }
        finally { foreach (var item in globals) { if (item.Value) Shader.EnableKeyword(item.Key); else Shader.DisableKeyword(item.Key); } QualitySettings.renderPipeline = q; GraphicsSettings.defaultRenderPipeline = g; RenderTexture.active = active; GL.sRGBWrite = srgb; foreach (var item in owned) if (item) Object.DestroyImmediate(item); owned.Clear(); File.WriteAllText(Path.Combine(output, "program-selection.json"), JsonUtility.ToJson(report, true)); EditorApplication.Exit(exit); }
    }
}
