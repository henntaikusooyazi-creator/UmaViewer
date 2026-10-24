Shader "Hidden/OriginalRendering/PostProcessDrawValidation"
{
    SubShader { Pass {
        Cull Off ZTest Always ZWrite Off Blend Off
        HLSLPROGRAM
        #pragma target 4.0
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        float4x4 _DrawModel, _DrawVP;
        float4 _DrawTexelSize, _DrawUvScaleOffset;
        Texture2D<float4> _MainTex;
        SamplerState point_clamp_sampler;
        struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
        struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
        Output vert(Input input)
        {
            Output result;
            result.position = mul(_DrawVP, mul(_DrawModel, input.vertex));
            result.uv = input.uv * _DrawUvScaleOffset.xy + _DrawUvScaleOffset.zw;
            if (_DrawTexelSize.y < 0) result.uv.y = 1 - result.uv.y;
            return result;
        }
        float4 frag(Output input) : SV_Target { return _MainTex.SampleLevel(point_clamp_sampler, input.uv, 0); }
        ENDHLSL
    } }
}
