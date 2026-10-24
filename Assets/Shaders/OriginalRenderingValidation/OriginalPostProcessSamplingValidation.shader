Shader "Hidden/OriginalRendering/PostProcessSamplingValidation"
{
    Properties { _MainTex ("Artificial mip input", 2D) = "white" {} }
    SubShader { Pass {
        Cull Off ZTest Always ZWrite Off Blend Off
        HLSLPROGRAM
        #pragma target 4.0
        #pragma vertex vert_img
        #pragma fragment frag
        #include "UnityCG.cginc"
        Texture2D<float4> _MainTex;
        SamplerState point_clamp_sampler;
        float4 _Sampling;
        float4 frag(v2f_img input) : SV_Target { return _MainTex.SampleBias(point_clamp_sampler, input.uv, _Sampling.x); }
        ENDHLSL
    } }
}
