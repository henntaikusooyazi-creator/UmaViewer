using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Typed bridge only: selection/time helpers and raw asset parsing remain upstream.
    public static class OriginalCompositeCueAdapter
    {
        public enum InputStage { SelectedKeys, EvaluatedProducer }
        [Flags]
        public enum Unresolved
        {
            None = 0, Offset = 1, ShaderAngles = 2, Fade = 4,
            EffectiveThickness = 8, Pass = 16, TextureMapping = 32, OtherPassRequired = 64
        }

        public readonly struct Context
        {
            public readonly int? TrackOrdinal, CompositeIndex;
            public readonly long? CurrentKeyToken, NextKeyToken, UpdateToken, ObservedUnityFrame;
            public Context(int? trackOrdinal, int? compositeIndex, long? currentKeyToken, long? nextKeyToken,
                long? updateToken = null, long? observedUnityFrame = null)
            {
                TrackOrdinal = trackOrdinal; CompositeIndex = compositeIndex;
                CurrentKeyToken = currentKeyToken; NextKeyToken = nextKeyToken;
                UpdateToken = updateToken; ObservedUnityFrame = observedUnityFrame;
            }
        }

        // Unknown components stay nullable. Rotation sources are deliberately not named
        // degrees/turns and are never converted with an inferred constant or kind table.
        public readonly struct CueValues
        {
            public readonly float? Thickness, LineAntialiasing, RotationSource, SecondaryAngleSource;
            public readonly Color? LineColor;
            public readonly Vector2? Offset;
            public readonly int? Kind, PassRaw;
            public CueValues(float? thickness, Color? lineColor, float? lineAntialiasing, Vector2? offset,
                float? rotationSource, float? secondaryAngleSource, int? kind, int? passRaw)
            {
                Thickness = thickness; LineColor = lineColor; LineAntialiasing = lineAntialiasing;
                Offset = offset; RotationSource = rotationSource; SecondaryAngleSource = secondaryAngleSource;
                Kind = kind; PassRaw = passRaw;
            }
        }

        // Fade is the evaluated setter result; effective thickness is the material input.
        // Neither is silently substituted with a key value or a timing approximation.
        public readonly struct DrawState
        {
            public readonly float? Fade, EffectiveThickness;
            public readonly Vector2? ShaderAngles;
            public DrawState(float? fade, float? effectiveThickness, Vector2? shaderAngles)
            { Fade = fade; EffectiveThickness = effectiveThickness; ShaderAngles = shaderAngles; }
        }
        public readonly struct TextureMapping
        {
            public readonly Vector4 ScaleOffset, MeshUvScaleOffset;
            public readonly float TexelSizeY, MipBias;
            public TextureMapping(Vector4 scaleOffset, float texelSizeY, Vector4 meshUvScaleOffset, float mipBias)
            { ScaleOffset = scaleOffset; TexelSizeY = texelSizeY; MeshUvScaleOffset = meshUvScaleOffset; MipBias = mipBias; }
        }

        public readonly struct Snapshot
        {
            public readonly Context Source;
            public readonly CueValues Values;
            public readonly DrawState Draw;
            public readonly TextureMapping? Texture;
            public readonly InputStage Stage;
            public readonly bool RequestedInterpolation, AppliedInterpolation;
            public readonly float? AppliedRatio;
            public int? WirePass => Values.PassRaw.HasValue ? unchecked((short)Values.PassRaw.Value) : (int?)null;
            internal Snapshot(Context source, CueValues values, DrawState draw, TextureMapping? texture,
                InputStage stage, bool requested, bool applied, float? ratio)
            {
                Source = source; Values = values; Draw = draw; Texture = texture; Stage = stage;
                RequestedInterpolation = requested; AppliedInterpolation = applied; AppliedRatio = ratio;
            }

            public Snapshot WithDrawState(DrawState draw)
            { return new Snapshot(Source, Values, draw, Texture, Stage, RequestedInterpolation, AppliedInterpolation, AppliedRatio); }

            public bool TryCreatePass0Parameters(out OriginalMaskCoverageGenerator.Parameters parameters, out Unresolved unresolved)
            {
                unresolved = Unresolved.None;
                if (!Values.Offset.HasValue) unresolved |= Unresolved.Offset;
                if (!Draw.ShaderAngles.HasValue) unresolved |= Unresolved.ShaderAngles;
                if (!Draw.Fade.HasValue) unresolved |= Unresolved.Fade;
                if (!Draw.EffectiveThickness.HasValue) unresolved |= Unresolved.EffectiveThickness;
                if (!Texture.HasValue) unresolved |= Unresolved.TextureMapping;
                if (!WirePass.HasValue) unresolved |= Unresolved.Pass;
                else if (WirePass.Value != 0) unresolved |= Unresolved.OtherPassRequired;
                parameters = default;
                if (unresolved != Unresolved.None) return false;
                Vector2 xy = Values.Offset.Value, zw = Draw.ShaderAngles.Value;
                TextureMapping t = Texture.Value;
                parameters = new OriginalMaskCoverageGenerator.Parameters(new Vector4(xy.x, xy.y, zw.x, zw.y),
                    Draw.EffectiveThickness.Value, Draw.Fade.Value, t.ScaleOffset, t.TexelSizeY, t.MeshUvScaleOffset, t.MipBias);
                return true;
            }
        }

        public static Snapshot FromSelectedKeys(Context source, CueValues current, CueValues? next,
            bool nextInterpolationPredicate, float ratio, DrawState draw, TextureMapping? texture)
        {
            bool applied = next.HasValue && nextInterpolationPredicate;
            CueValues value = applied ? Interpolate(current, next.Value, ratio) : current;
            return new Snapshot(source, value, draw, texture, InputStage.SelectedKeys,
                nextInterpolationPredicate, applied, applied ? ratio : (float?)null);
        }

        public static Snapshot FromEvaluatedProducer(Context source, CueValues values, DrawState draw, TextureMapping? texture)
        {
            return new Snapshot(source, values, draw, texture, InputStage.EvaluatedProducer, false, false, null);
        }

        static CueValues Interpolate(CueValues a, CueValues b, float ratio)
        {
            return new CueValues(L(a.Thickness, b.Thickness, ratio), L(a.LineColor, b.LineColor, ratio),
                L(a.LineAntialiasing, b.LineAntialiasing, ratio), L(a.Offset, b.Offset, ratio),
                L(a.RotationSource, b.RotationSource, ratio), L(a.SecondaryAngleSource, b.SecondaryAngleSource, ratio),
                a.Kind, a.PassRaw);
        }
        static float? L(float? a, float? b, float t)
        { return a.HasValue && b.HasValue ? a.Value + (b.Value - a.Value) * t : (float?)null; }
        static Vector2? L(Vector2? a, Vector2? b, float t)
        { return a.HasValue && b.HasValue ? a.Value + (b.Value - a.Value) * t : (Vector2?)null; }
        static Color? L(Color? a, Color? b, float t)
        {
            if (!a.HasValue || !b.HasValue) return null;
            Color x = a.Value, y = b.Value;
            return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t,
                x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }
    }
}
