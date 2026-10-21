// SyntheticSubstitute / TestOnly. No CUTT schema or runtime interpretation.
Shader "Hidden/TestOnly/LiveDiagonalComposite"
{
    Properties
    {
        _MainTex ("View A", 2D) = "black" {}
        _ViewB ("View B", 2D) = "black" {}
        _ViewC ("View C", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            ColorMask RGBA
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _ViewB;
            sampler2D _ViewC;
            float4 _ViewC_TexelSize, _CropA, _CropB, _CropC, _RegionBoundaries;
            float _ThreeRegions, _RegionFeather;
            float4 _MainTex_TexelSize;
            float4 _ViewB_TexelSize;
            float4 _Boundary;
            float4 _OutputScale;

            float4 frag(v2f_img input) : SV_Target
            {
                if (_ThreeRegions > 0.5)
                {
                    float2 a = _CropA.xy + input.uv * _CropA.zw;
                    float2 b = _CropB.xy + input.uv * _CropB.zw;
                    float2 c = _CropC.xy + input.uv * _CropC.zw;
                    #if UNITY_UV_STARTS_AT_TOP
                    if (_MainTex_TexelSize.y < 0) a.y = 1 - a.y;
                    if (_ViewB_TexelSize.y < 0) b.y = 1 - b.y;
                    if (_ViewC_TexelSize.y < 0) c.y = 1 - c.y;
                    #endif
                    float2 edges = _RegionBoundaries.xy + _RegionBoundaries.zw * (input.uv.y - 0.5);
                    float2 masks = _RegionFeather > 0 ? smoothstep(edges - _RegionFeather * 0.5, edges + _RegionFeather * 0.5, input.uv.xx) : step(edges, input.uv.xx);
                    return float4(lerp(lerp(tex2D(_MainTex, a).rgb, tex2D(_ViewB, b).rgb, masks.x), tex2D(_ViewC, c).rgb, masks.y), 1);
                }
                float2 uvA = input.uv;
                float2 uvB = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) uvA.y = 1 - uvA.y;
                if (_ViewB_TexelSize.y < 0) uvB.y = 1 - uvB.y;
                #endif
                float distanceToBoundary = dot((input.uv - _Boundary.xy) * _OutputScale.xy, _Boundary.zw);
                float mask;
                if (_OutputScale.z > 0)
                    mask = smoothstep(-0.5 * _OutputScale.z, 0.5 * _OutputScale.z, distanceToBoundary);
                else
                    mask = step(0.0, distanceToBoundary);
                if (_OutputScale.w > 0.5) mask = 1.0 - mask;
                float3 rgb = lerp(tex2D(_MainTex, uvA).rgb, tex2D(_ViewB, uvB).rgb, mask);
                return float4(rgb, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}