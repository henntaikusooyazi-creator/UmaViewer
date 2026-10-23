Shader "Hidden/OriginalRendering/NaturalPostProcessValidation"
{
    Properties { _MainTex ("Color", 2D) = "black" {} _Depth ("Depth", 2D) = "black" {} }
    SubShader { Pass {
        Cull Off ZWrite Off ZTest Always
        CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex, _Depth;
        float _Parameter;
        float4 frag(v2f_img input) : SV_Target
        {
            float4 color = tex2D(_MainTex, input.uv);
            float depth = tex2D(_Depth, input.uv).r;
            return float4(color.rgb * .5 + float3(depth * .2, _Parameter * .1, .03), color.a);
        }
        ENDCG
    } }
}
