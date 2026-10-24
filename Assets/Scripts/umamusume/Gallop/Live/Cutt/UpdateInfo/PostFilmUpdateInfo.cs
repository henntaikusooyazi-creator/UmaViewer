using UnityEngine;

namespace Gallop.Live.Cutt
{
    [System.Serializable]
    public struct PostFilmUpdateInfo
    {
        public PostFilmMode filmMode;
        public PostColorType colorType;
        public float filmPower;
        public Vector2 filmOffsetParam;
        public Vector4 filmOptionParam;
        public Color color0, color1, color2, color3;
        public float depthPower, DepthClip;
        public LiveTimelineKeyPostFilmData.LayerMode layerMode;
        public LiveTimelineKeyPostFilmData.ColorBlend colorBlend;
        public bool inverseVignette;
        public float colorBlendFactor;
        public int movieResId, movieFrameOffset;
        public float movieTime;
        public bool movieReverse;
        public float RollAngle;
        public Vector2 FilmScale;
    }
}
