Shader "Hidden/OriginalRendering/DepthCopy"
{
    Properties { _MainTex ("Current camera depth", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    UNITY_DECLARE_DEPTH_TEXTURE(_MainTex);
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float CopyBound(v2f_img input) : SV_Target { return SAMPLE_DEPTH_TEXTURE(_MainTex, input.uv); }
    float CopyCurrent(v2f_img input) : SV_Target { return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv); }
    ENDCG
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment CopyBound
            ENDCG
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment CopyCurrent
            ENDCG
        }
    }
}
