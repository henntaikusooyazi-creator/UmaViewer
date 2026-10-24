Shader "Hidden/OriginalRendering/FilmKeywordValidation"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        float4 result(float passValue)
        {
            float color = 0.25, mask = 0.25, mode = 0;
            #if defined(COLOR_ONLY)
                color = 0.75;
            #endif
            #if defined(MASK_VIGNETTE)
                mask = 0.75;
            #endif
            #if defined(MODE_ADD) && !defined(MODE_LERP) && !defined(BLEND_ADD)
                mode = 1;
            #endif
            return float4(passValue, color, mask, mode);
        }
        float4 first(v2f_img i) : SV_Target { return result(0.125); }
        float4 inverse(v2f_img i) : SV_Target { return result(0.625); }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment first
            #pragma multi_compile __ MODE_LERP MODE_ADD
            #pragma multi_compile __ BLEND_ADD
            #pragma multi_compile __ MASK_VIGNETTE
            #pragma multi_compile __ COLOR_ONLY
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment inverse
            #pragma multi_compile __ MODE_LERP MODE_ADD
            #pragma multi_compile __ BLEND_ADD
            #pragma multi_compile __ MASK_VIGNETTE
            #pragma multi_compile __ COLOR_ONLY
            ENDCG
        }
    }
}
