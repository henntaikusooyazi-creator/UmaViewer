#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>
/// SyntheticSubstitute / TestOnly compositor. Does not consume CUTT data.
/// Owns only a material: callers own three distinct, created 2D RenderTextures.
/// RGB inputs are treated as completed opaque camera views; output alpha is always 1.
/// </summary>
public sealed class LiveDiagonalCompositeTestOnly : IDisposable
{
    public const string ShaderName = "Hidden/TestOnly/LiveDiagonalComposite";
    private Material material;

    public LiveDiagonalCompositeTestOnly(Shader shader = null)
    {
        shader = shader != null ? shader : Shader.Find(ShaderName);
        if (shader == null || shader.name != ShaderName || !shader.isSupported)
            throw new InvalidOperationException("TestOnly diagonal composite shader is missing or unsupported.");
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
    }

    /// <param name="center">Boundary anchor in normalized output UV, each component [0,1].</param>
    /// <param name="angleDegrees">Boundary tangent angle from +X, counterclockwise, [-180,180].</param>
    /// <param name="feather">Full boundary transition width as fraction of shorter output dimension, [0,1]. Zero is a hard cut.</param>
    /// <param name="invert">Swaps the mask sides without changing either camera or input texture.</param>
    public void Composite(RenderTexture viewA, RenderTexture viewB, RenderTexture output,
        Vector2 center, float angleDegrees, float feather, bool invert = false)
    {
        if (material == null) throw new ObjectDisposedException(nameof(LiveDiagonalCompositeTestOnly));
        ValidateTexture(viewA, nameof(viewA));
        ValidateTexture(viewB, nameof(viewB));
        ValidateTexture(output, nameof(output));
        if (viewA == viewB || viewA == output || viewB == output)
            throw new ArgumentException("TestOnly compositor requires three distinct RenderTextures.");
        if (!Finite(center.x) || !Finite(center.y) || center.x < 0 || center.x > 1 || center.y < 0 || center.y > 1)
            throw new ArgumentOutOfRangeException(nameof(center));
        if (!Finite(angleDegrees) || angleDegrees < -180 || angleDegrees > 180)
            throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        if (!Finite(feather) || feather < 0 || feather > 1)
            throw new ArgumentOutOfRangeException(nameof(feather));

        material.SetFloat("_ThreeRegions", 0);
        float angle = angleDegrees * Mathf.Deg2Rad;
        float shortest = Mathf.Min(output.width, output.height);
        material.SetTexture("_ViewB", viewB);
        material.SetVector("_Boundary", new Vector4(center.x, center.y, -Mathf.Sin(angle), Mathf.Cos(angle)));
        material.SetVector("_OutputScale", new Vector4(output.width / shortest, output.height / shortest, feather, invert ? 1 : 0));
        RenderTexture previousTarget = RenderTexture.active;
        bool previousSrgbWrite = GL.sRGBWrite;
        try
        {
            // Sample linearized RGB and encode only when the destination requests sRGB.
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear && output.sRGB;
            Graphics.Blit(viewA, output, material, 0);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            GL.sRGBWrite = previousSrgbWrite;
            material.SetTexture("_MainTex", null);
            material.SetTexture("_ViewB", null);
        }
    }

    // Synthetic output-space boundaries: x = anchor + slope * (y - 0.5).
    // Crop rectangles map output UV to an independent normalized source rectangle.
    public struct ThreeRegionSettings
    {
        public float LeftAnchor, RightAnchor, LeftSlope, RightSlope, Feather;
        public Rect CropA, CropB, CropC;
        public static ThreeRegionSettings Default => new ThreeRegionSettings
        {
            LeftAnchor = 0.34f, RightAnchor = 0.70f, LeftSlope = -0.30f, RightSlope = -0.18f,
            CropA = new Rect(0, 0, 1, 1), CropB = new Rect(0, 0, 1, 1), CropC = new Rect(0, 0, 1, 1)
        };
        public void Validate()
        {
            if (!Finite(LeftAnchor) || !Finite(RightAnchor) || !Finite(LeftSlope) || !Finite(RightSlope) ||
                !Finite(Feather) || Feather < 0 || Feather > 1)
                throw new ArgumentOutOfRangeException(nameof(ThreeRegionSettings));
            // Both endpoints suffice because the boundaries are linear. Retain three regions throughout.
            foreach (float y in new[] { -0.5f, 0.5f })
            {
                float left = LeftAnchor + LeftSlope * y, right = RightAnchor + RightSlope * y;
                if (left <= 0 || right >= 1 || right - left <= Feather)
                    throw new ArgumentException("Boundaries must stay ordered, inside the frame, and separated by feather.");
            }
            ValidateCrop(CropA); ValidateCrop(CropB); ValidateCrop(CropC);
        }
        static void ValidateCrop(Rect crop)
        {
            if (!Finite(crop.x) || !Finite(crop.y) || !Finite(crop.width) || !Finite(crop.height) ||
                crop.x < 0 || crop.y < 0 || crop.width <= 0 || crop.height <= 0 || crop.xMax > 1 || crop.yMax > 1)
                throw new ArgumentOutOfRangeException(nameof(crop));
        }
    }

    public void CompositeThree(RenderTexture viewA, RenderTexture viewB, RenderTexture viewC,
        RenderTexture output, ThreeRegionSettings settings)
    {
        if (material == null) throw new ObjectDisposedException(nameof(LiveDiagonalCompositeTestOnly));
        settings.Validate();
        var textures = new[] { viewA, viewB, viewC, output };
        for (int i = 0; i < textures.Length; i++)
        {
            ValidateTexture(textures[i], "textures");
            for (int j = 0; j < i; j++) if (textures[i] == textures[j])
                throw new ArgumentException("Three-region composition requires four distinct textures.");
        }
        material.SetFloat("_ThreeRegions", 1);
        material.SetTexture("_ViewB", viewB); material.SetTexture("_ViewC", viewC);
        material.SetVector("_RegionBoundaries", new Vector4(settings.LeftAnchor, settings.RightAnchor, settings.LeftSlope, settings.RightSlope));
        material.SetFloat("_RegionFeather", settings.Feather);
        material.SetVector("_CropA", CropVector(settings.CropA));
        material.SetVector("_CropB", CropVector(settings.CropB));
        material.SetVector("_CropC", CropVector(settings.CropC));
        var previous = RenderTexture.active;
        bool previousSrgb = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear && output.sRGB;
            Graphics.Blit(viewA, output, material, 0);
        }
        finally
        {
            RenderTexture.active = previous; GL.sRGBWrite = previousSrgb;
            material.SetTexture("_MainTex", null); material.SetTexture("_ViewB", null); material.SetTexture("_ViewC", null);
        }
    }
    static Vector4 CropVector(Rect r) => new Vector4(r.x, r.y, r.width, r.height);

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void ValidateTexture(RenderTexture texture, string argument)
    {
        if (texture == null) throw new ArgumentNullException(argument);
        if (!texture.IsCreated() || texture.width <= 0 || texture.height <= 0 ||
            texture.dimension != UnityEngine.Rendering.TextureDimension.Tex2D || texture.volumeDepth != 1 ||
            texture.antiAliasing != 1 || texture.bindTextureMS || texture.format == RenderTextureFormat.Depth ||
            texture.format == RenderTextureFormat.Shadowmap)
            throw new ArgumentException("Expected a created, single-sample 2D color RenderTexture.", argument);
    }

    public void Dispose()
    {
        if (material == null) return;
        UnityEngine.Object.DestroyImmediate(material);
        material = null;
    }
}
#endif