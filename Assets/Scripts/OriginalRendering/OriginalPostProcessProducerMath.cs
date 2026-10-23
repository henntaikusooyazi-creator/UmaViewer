using System;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Confirmed arithmetic only. Program selection, texture layout, capability
    // gates and timeline-to-overlay evaluation remain the caller's responsibility.
    public static class OriginalPostProcessProducerMath
    {
        public sealed class ConfirmedProfile
        {
            public readonly float SamplingScale, BloomPixelScale;
            public ConfirmedProfile(float samplingScale, float bloomPixelScale)
            {
                if (!Finite(samplingScale) || samplingScale <= 0 || !Finite(bloomPixelScale) || bloomPixelScale <= 0)
                    throw new ArgumentException("Explicit positive finite producer dependencies are required.");
                SamplingScale = samplingScale; BloomPixelScale = bloomPixelScale;
            }
        }

        public readonly struct BloomDiffusionValues
        {
            public readonly Vector4 PixelSize, Color, InitialBloom, BlurBloom;
            public readonly float BloomDofWeight, BloomIsScreenBlend;
            internal BloomDiffusionValues(Vector4 pixelSize, Vector4 color, Vector4 initialBloom,
                Vector4 blurBloom, float weight, float screenBlend)
            { PixelSize = pixelSize; Color = color; InitialBloom = initialBloom; BlurBloom = blurBloom;
              BloomDofWeight = weight; BloomIsScreenBlend = screenBlend; }
        }

        public readonly struct FilmValues
        {
            public readonly float Power, DepthPower, DepthClip, IsWithoutDepth, IsInverseVignette;
            public readonly Vector4 Offset, Option, Color0, Color1, Color2, Color3, Roll, Scale;
            internal FilmValues(FilmInputs value, int width, int height)
            {
                Power = value.PostFilmPower; DepthPower = value.DepthPower;
                DepthClip = value.DepthClip > 1f ? 0f : Single(1f - value.DepthClip);
                IsWithoutDepth = value.IsEnableDepth ? 0f : 1f;
                IsInverseVignette = value.InverseVignette ? 1f : 0f;
                Offset = new Vector4(value.PostFilmOffsetParam.x, value.PostFilmOffsetParam.y, 0, 0);
                Option = value.PostFilmOptionParam;
                Color0 = value.PostFilmColor0; Color1 = value.PostFilmColor1;
                Color2 = value.PostFilmColor2; Color3 = value.PostFilmColor3;
                var roll = value.RollParameter; roll.z = Single(Single(width) / Single(height));
                Roll = roll; Scale = value.ScaleParameter;
            }
        }

        // Explicit evaluated overlay state. This DTO does not select a timeline
        // key, interpolate a value, generate a movie mapping or supply defaults.
        public struct FilmInputs
        {
            public float PostFilmPower, DepthPower, DepthClip;
            public Vector2 PostFilmOffsetParam;
            public Vector4 PostFilmOptionParam, PostFilmColor0, PostFilmColor1, PostFilmColor2, PostFilmColor3;
            public Vector4 RollParameter, ScaleParameter;
            public int LayerMode;
            public bool IsExistMovieMask, IsEnableDepth, InverseVignette;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        // Force the store after each SSE-width operation, including Mono hosts
        // which can otherwise retain a wider intermediate across a float local.
        static float Single(float value) => BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
        static void Dimensions(int width, int height)
        { if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("Positive source dimensions are required."); }

        public static BloomDiffusionValues EvaluateBloomDiffusion(PostEffectUpdateInfo_BloomDiffusion input,
            int width, int height, ConfirmedProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Dimensions(width, height);
            float scaledWidth = Single(Single(width) * profile.SamplingScale);
            float scaledHeight = Single(Single(height) * profile.SamplingScale);
            if (!Finite(scaledWidth) || !Finite(scaledHeight) || scaledWidth < 1 || scaledHeight < 1 ||
                (double)scaledWidth >= 2147483648d || (double)scaledHeight >= 2147483648d)
                throw new ArgumentOutOfRangeException("The explicit sampling dimensions cannot be represented.");
            float blur = Single(input.diffusionBlurSize * profile.SamplingScale);
            var pixel = new Vector4(Single(blur / Single((int)scaledWidth)),
                Single(blur / Single((int)scaledHeight)), 0, 0);
            var color = new Vector4(input.diffusionBright, input.diffusionSaturation,
                input.diffusionContrast, input.diffusionThreshold);
            var initial = new Vector4(Single(input.BloomBlurSize * profile.SamplingScale), 0,
                input.threshold, input.intensity);
            float aspect = Single(Single(width) / Single(height));
            var bloom = new Vector4(Single(Single(input.BloomBlurSize / aspect) * profile.BloomPixelScale),
                Single(input.BloomBlurSize * profile.BloomPixelScale), input.threshold, input.intensity);
            return new BloomDiffusionValues(pixel, color, initial, bloom, input.bloomDofWeight,
                (int)input.BloomBlendMode == 0 ? 1f : 0f);
        }

        // This is the explicit prefilter branch's parameter, not a default for
        // missing Bloom payload or a decision to schedule that branch.
        public static Vector4 SourcePrefilterParameter => new Vector4(0, 0, 0, 1);

        public static FilmValues EvaluateFilm(FilmInputs input, int width, int height)
        {
            Dimensions(width, height);
            if ((int)input.LayerMode != 0 || input.IsExistMovieMask)
                throw new NotSupportedException("Movie-layer preparation requires a separately qualified provider.");
            return new FilmValues(input, width, height);
        }
    }
}
