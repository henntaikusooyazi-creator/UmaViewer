using System;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Key selection and interpolation-helper evaluation belong to Control.
    // Movie and BlinkLight producers require their own qualified inputs.
    public static class OriginalPostFilmMath
    {
        public sealed class ConfirmedProfile
        {
            public readonly float FrameRate, SinMultiplier, CosMultiplier, CosDivisor;
            public readonly Vector2 RollTail, ScaleTail;
            public readonly bool EnableDepth;
            public ConfirmedProfile(float frameRate, float sinMultiplier, float cosMultiplier, float cosDivisor,
                Vector2 rollTail, Vector2 scaleTail, bool enableDepth)
            {
                if (!Finite(frameRate) || frameRate <= 0 || !Finite(sinMultiplier) || !Finite(cosMultiplier) ||
                    !Finite(cosDivisor) || cosDivisor == 0 || !Finite(rollTail.x) || !Finite(rollTail.y) ||
                    !Finite(scaleTail.x) || !Finite(scaleTail.y)) throw new ArgumentException("Explicit finite Film dependencies required.");
                FrameRate = frameRate; SinMultiplier = sinMultiplier; CosMultiplier = cosMultiplier;
                CosDivisor = cosDivisor; RollTail = rollTail; ScaleTail = scaleTail; EnableDepth = enableDepth;
            }
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static float F(float value) => BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
        static float Lerp(float a, float b, float t) => F(a + F(F(b - a) * t));
        static Color Lerp(Color a, Color b, float t) => new Color(Lerp(a.r, b.r, t), Lerp(a.g, b.g, t), Lerp(a.b, b.b, t), Lerp(a.a, b.a, t));
        static void Supported(LiveTimelineKeyPostFilmData key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if ((int)key.layerMode != 0 || ((int)key.attribute & 0x1e00000) != 0)
                throw new NotSupportedException("Movie or BlinkLight Film inputs have no qualified provider.");
            if ((int)key.filmMode < 0 || (int)key.filmMode > 7 || (int)key.colorType < 0 || (int)key.colorType > 3)
                throw new NotSupportedException("Unknown Film mode or color layout.");
        }
        public static PostFilmUpdateInfo Evaluate(LiveTimelineKeyPostFilmData current,
            LiveTimelineKeyPostFilmData next, float? usedRatio, int currentFrame, ConfirmedProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Supported(current);
            if (usedRatio.HasValue && (next == null || !Finite(usedRatio.Value)))
                throw new ArgumentException("A finite used ratio requires a selected next key.");
            if (usedRatio.HasValue) Supported(next);
            float t = usedRatio.GetValueOrDefault();
            float Scalar(float a, float b) => usedRatio.HasValue ? Lerp(a, b, t) : a;
            Color ColorValue(Color a, Color b) => usedRatio.HasValue ? Lerp(a, b, t) : a;
            var other = next ?? current;
            Color c0 = ColorValue(current.color0, other.color0), c1 = ColorValue(current.color1, other.color1);
            var result = new PostFilmUpdateInfo
            {
                filmMode = current.filmMode, colorType = current.colorType,
                filmPower = Scalar(current.filmPower, other.filmPower),
                filmOffsetParam = new Vector2(Scalar(current.filmOffsetParam.x, other.filmOffsetParam.x), Scalar(current.filmOffsetParam.y, other.filmOffsetParam.y)),
                filmOptionParam = new Vector4(Scalar(current.filmOptionParam.x, other.filmOptionParam.x), Scalar(current.filmOptionParam.y, other.filmOptionParam.y), Scalar(current.filmOptionParam.z, other.filmOptionParam.z), Scalar(current.filmOptionParam.w, other.filmOptionParam.w)),
                depthPower = Scalar(current.depthPower, other.depthPower), DepthClip = Scalar(current.DepthClip, other.DepthClip),
                colorBlendFactor = Scalar(current.colorBlendFactor, other.colorBlendFactor),
                movieTime = F(F(F(unchecked(currentFrame - current.frame)) / profile.FrameRate) * Scalar(current.movieSpeed, other.movieSpeed)),
                RollAngle = Scalar(current.RollAngle, other.RollAngle),
                FilmScale = new Vector2(Scalar(current.FilmScale.x, other.FilmScale.x), Scalar(current.FilmScale.y, other.FilmScale.y)),
                layerMode = current.layerMode, colorBlend = current.colorBlend, movieResId = current.movieResId,
                movieFrameOffset = current.movieFrameOffset, movieReverse = ((int)current.attribute & 0x80000) != 0,
                inverseVignette = ((int)current.attribute & 0x100000) != 0
            };
            switch (current.colorType)
            {
                case PostColorType.ColorAll: result.color0 = result.color1 = result.color2 = result.color3 = c0; break;
                case PostColorType.Color2TopBottom: result.color0 = result.color1 = c0; result.color2 = result.color3 = c1; break;
                case PostColorType.Color2LeftRight: result.color0 = result.color2 = c0; result.color1 = result.color3 = c1; break;
                case PostColorType.Color4: result.color0 = c0; result.color1 = c1; result.color2 = ColorValue(current.color2, other.color2); result.color3 = ColorValue(current.color3, other.color3); break;
            }
            return result;
        }
        public static OriginalPostProcessProducerMath.FilmInputs ToFilm(PostFilmUpdateInfo value, ConfirmedProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if ((int)value.layerMode != 0) throw new NotSupportedException("Movie preparation is unresolved.");
            return new OriginalPostProcessProducerMath.FilmInputs
            {
                PostFilmPower = value.filmPower, DepthPower = value.depthPower, DepthClip = value.DepthClip,
                PostFilmOffsetParam = value.filmOffsetParam, PostFilmOptionParam = value.filmOptionParam,
                PostFilmColor0 = value.color0, PostFilmColor1 = value.color1, PostFilmColor2 = value.color2, PostFilmColor3 = value.color3,
                RollParameter = new Vector4(Mathf.Sin(F(value.RollAngle * profile.SinMultiplier)),
                    Mathf.Cos(F(F(value.RollAngle * profile.CosMultiplier) / profile.CosDivisor)), profile.RollTail.x, profile.RollTail.y),
                ScaleParameter = new Vector4(F(1f / value.FilmScale.x), F(1f / value.FilmScale.y), profile.ScaleTail.x, profile.ScaleTail.y),
                LayerMode = (int)value.layerMode, IsEnableDepth = profile.EnableDepth, InverseVignette = value.inverseVignette
            };
        }
    }
}
