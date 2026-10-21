Shader "Hidden/Gallop/OriginalMaskCoverage"
{
    Properties { _MainTex ("Explicit camera/color input", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex coverageVertex
            #pragma fragment coveragePixel
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Transform, _TextureScaleOffset, _MeshUvScaleOffset;
            float _Thickness, _Fade, _TextureTexelSizeY, _MipBias;
            struct CoverageVaryings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 boundary : TEXCOORD1;
            };
            CoverageVaryings coverageVertex(appdata_img v)
            {
                CoverageVaryings o;
                o.position = UnityObjectToClipPos(v.vertex);
                float2 uv = mad(v.texcoord.xy, _MeshUvScaleOffset.xy, _MeshUvScaleOffset.zw);
                uv = mad(uv, _TextureScaleOffset.xy, _TextureScaleOffset.zw);
                if (_TextureTexelSizeY < 0) uv.y = 1 - uv.y;
                uv += _Transform.xy;
                o.uv = uv;
                float2 centered = uv - .5;
                float sine, cosine;
                // Exact float32 literal retained in the saved VS.
                sincos((_Transform.z + _Transform.w) * 6.283185005187988, sine, cosine);
                o.boundary.x = mad(centered.y, cosine, centered.x * sine) + .5;
                sincos((_Transform.z - _Transform.w) * 6.283185005187988, sine, cosine);
                o.boundary.y = mad(centered.y, cosine, centered.x * sine) + .5;
                return o;
            }
            float4 coveragePixel(CoverageVaryings i) : SV_Target
            {
                float halfWidth = mad(_Thickness, .5, .5);
                float above = min(i.boundary.x - halfWidth, i.boundary.y - halfWidth);
                float ramp = min(halfWidth - min(i.boundary.x, i.boundary.y), _Thickness);
                ramp = max(ramp, 0);
                ramp = (_Thickness - ramp) / _Thickness;
                float coverage = above >= 0 ? 1 : ramp;
                // Preserve PS0's inclusive clipping, fade and source-alpha independence.
                // No epsilon, angle conversion, saturate(fade), or invented feather.
                coverage *= i.uv.x >= 0 ? 1 : 0;
                coverage *= i.uv.x <= 1 ? 1 : 0;
                coverage *= i.uv.y >= 0 ? 1 : 0;
                coverage *= i.uv.y <= 1 ? 1 : 0;
                coverage *= _Fade;
                return float4(tex2Dbias(_MainTex, float4(i.uv, 0, _MipBias)).rgb, coverage);
            }
            ENDCG
        }
    }
    Fallback Off
}
