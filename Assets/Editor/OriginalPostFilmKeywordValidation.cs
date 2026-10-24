using System;
using System.Linq;
using Gallop.ImageEffect;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEngine;
using UnityEngine.Rendering;

// Executes the existing Render.Blit with artificial geometry/state. This does
// not observe an original process or infer the effective native variant.
public static class OriginalPostFilmKeywordValidation
{
    public static void Check(Action<bool> check)
    {
        var value = new PostFilmUpdateInfo { filmMode = PostFilmMode.Add, filmPower = 1, color0 = Color.white };
        var before = new[] { "MODE_LERP", "BLEND_ADD", "MASK_VIGNETTE", "COLOR_ONLY", "UNRELATED_KEYWORD" };
        var transition = OriginalPostFilmRouteAudit.CaptureGlobalTransition(value, 0, before);
        check(transition.after.SequenceEqual(new[] { "COLOR_ONLY", "MASK_VIGNETTE", "MODE_ADD", "UNRELATED_KEYWORD" }));
        check(before[0] == "MODE_LERP" && transition.before.Contains("BLEND_ADD"));
        check(!transition.runtime_contract && !transition.OriginalVerified);
        var empty = OriginalPostFilmRouteAudit.CaptureGlobalTransition(value, 0, Array.Empty<string>());
        check(empty.after.SequenceEqual(new[] { "MODE_ADD" }));
        var invalid = value; invalid.filmMode = PostFilmMode.None;
        check(OriginalPostFilmRouteAudit.CaptureGlobalTransition(invalid, 0, before).after.SequenceEqual(transition.before));
        foreach (var bad in new[] { new[] { "DUP", "DUP" }, new[] { " " }, new[] { "A B" }, new string[] { null },
            new[] { new string('x', 129) }, Enumerable.Range(0, 257).Select(i => "K" + i).ToArray() })
        {
            bool rejected = false;
            try { OriginalPostFilmRouteAudit.CaptureGlobalTransition(value, 0, bad); }
            catch (ArgumentException) { rejected = true; }
            check(rejected);
        }
        bool missing = false;
        try { OriginalPostFilmRouteAudit.CaptureGlobalTransition(value, 0, null); }
        catch (ArgumentNullException) { missing = true; }
        check(missing);

        var shader = Shader.Find("Hidden/OriginalRendering/FilmKeywordValidation");
        check(shader != null && shader.isSupported);
        var material = new Material(shader);
        var source = new RenderTexture(4, 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var targets = new[] { new RenderTexture(4, 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear),
            new RenderTexture(4, 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) };
        var names = new[] { "MODE_LERP", "MODE_ADD", "MODE_MUL", "MODE_NONE", "MODE_VIGNETTE_LERP", "MODE_VIGNETTE_ADD",
            "MODE_VIGNETTE_MUL", "MODE_MONOCHROME", "MODE_SCREENBLEND", "MODE_VIGNETT_SCREENBLEND",
            "BLEND_NONE", "BLEND_LERP", "BLEND_ADD", "BLEND_MUL", "MASK_VIGNETTE", "COLOR_ONLY" };
        var saved = names.Select(Shader.IsKeywordEnabled).ToArray();
        var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = false;
            source.Create(); foreach (var target in targets) target.Create();
            var renderer = new ScreenOverlayRender.Render();
            // Each buffer contains two distinct draws. Global changes after the
            // first draw must not retroactively affect that draw's output.
            foreach (bool retained in new[] { false, true })
            foreach (bool localColor in new[] { false, true })
            using (var cmd = new CommandBuffer { name = "Artificial Film keyword sequence" })
            {
                foreach (var name in names) Shader.DisableKeyword(name);
                if (localColor) material.EnableKeyword("COLOR_ONLY"); else material.DisableKeyword("COLOR_ONLY");
                if (retained) { cmd.EnableShaderKeyword("COLOR_ONLY"); cmd.EnableShaderKeyword("MASK_VIGNETTE"); }
                cmd.EnableShaderKeyword("MODE_LERP"); cmd.EnableShaderKeyword("BLEND_ADD");
                var parameter = new ScreenOverlayRender.Parameter { PostFilmMode = ScreenOverlay.Overlay.PostFilmMode.Add,
                    PostFilmPower = 1, PostFilmColor0 = Color.white };
                renderer.Blit(ref parameter, cmd, RenderTextureHandle.Make(source), RenderTextureHandle.Make(targets[0]), material, 0);
                cmd.DisableShaderKeyword("COLOR_ONLY"); cmd.DisableShaderKeyword("MASK_VIGNETTE");
                parameter.InverseVignette = true;
                renderer.Blit(ref parameter, cmd, RenderTextureHandle.Make(source), RenderTextureHandle.Make(targets[1]), material, 0);
                check(!Shader.IsKeywordEnabled("MODE_ADD")); // Recording is not execution.
                Graphics.ExecuteCommandBuffer(cmd);
                check(Shader.IsKeywordEnabled("MODE_ADD") && !Shader.IsKeywordEnabled("MODE_LERP") &&
                    !Shader.IsKeywordEnabled("BLEND_ADD") && !Shader.IsKeywordEnabled("MASK_VIGNETTE") && !Shader.IsKeywordEnabled("COLOR_ONLY"));
                check(material.IsKeywordEnabled("COLOR_ONLY") == localColor); // Material/global evidence stays separate.
                for (int draw = 0; draw < 2; draw++)
                {
                    var read = AsyncGPUReadback.Request(targets[draw], 0, TextureFormat.RGBA32); read.WaitForCompletion();
                    check(!read.hasError); var bytes = read.GetData<byte>();
                    byte r = draw == 0 ? (byte)32 : (byte)159;
                    byte g = (localColor || (draw == 0 && retained)) ? (byte)191 : (byte)64;
                    byte b = draw == 0 && retained ? (byte)191 : (byte)64;
                    check(Enumerable.Range(0, 8).All(i => bytes[i * 4] == r && bytes[i * 4 + 1] == g &&
                        bytes[i * 4 + 2] == b && bytes[i * 4 + 3] == 255));
                }
            }
        }
        finally
        {
            RenderTexture.active = active; GL.sRGBWrite = srgb;
            for (int i = 0; i < names.Length; i++) { if (saved[i]) Shader.EnableKeyword(names[i]); else Shader.DisableKeyword(names[i]); }
            UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(source);
            foreach (var target in targets) UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
