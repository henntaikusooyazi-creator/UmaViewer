Shader "Hidden/OriginalRendering/DepthFixture"
{
    Properties { _Color ("Fixture identity", Color) = (1,0,0,1) }
    CGINCLUDE
    #include "UnityCG.cginc"
    float4 _Color;
    float4 vert(float4 position : POSITION) : SV_POSITION { return UnityObjectToClipPos(position); }
    float4 color() : SV_Target { return _Color; }
    float depth() : SV_Target { return 0; }
    ENDCG
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment color
            ENDCG
        }
        Pass
        {
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment depth
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment color
            ENDCG
        }
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment depth
            ENDCG
        }
    }
}
