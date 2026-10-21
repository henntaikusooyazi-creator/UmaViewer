Shader "Hidden/Gallop/OriginalMultiCamera"
{
    Properties { _MainTex ("Explicit input", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _ScaleOffset;
        float _Opacity;
        float4 sampleInput(v2f_img i) { return tex2D(_MainTex, i.uv * _ScaleOffset.xy + _ScaleOffset.zw); }
        float4 straight(v2f_img i) : SV_Target { float4 v=sampleInput(i); v.a *= _Opacity; return v; }
        float4 captured(v2f_img i) : SV_Target { return sampleInput(i) * _Opacity; }
        float4 final(v2f_img i) : SV_Target { return tex2D(_MainTex, i.uv); }
        float4 copy(v2f_img i) : SV_Target { return sampleInput(i); }
        ENDCG
        // Observed mask draw: RGB SrcAlpha/InvSrcAlpha, alpha One/One.
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One One
            ColorMask RGBA
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment straight
            ENDCG
        }
        // Explicit import of already blended measured layers; do not multiply RGB twice.
        // This pass does not claim to reconstruct unrecorded raw camera RGB.
        Pass
        {
            Blend One OneMinusSrcAlpha, One One
            ColorMask RGBA
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment captured
            ENDCG
        }
        // Observed FinalComposite: keep destination alpha (RGB write mask).
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment final
            ENDCG
        }
        Pass
        {
            Blend Off
            ColorMask RGBA
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment copy
            ENDCG
        }
    }
    Fallback Off
}
