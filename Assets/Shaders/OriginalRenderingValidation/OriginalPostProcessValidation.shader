Shader "Hidden/OriginalRendering/PostProcessValidation"
{
    Properties { _MainTex ("Input", 2D) = "black" {} _OtherTex ("Other", 2D) = "black" {} }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _OtherTex;
            float4 _Gain, _OtherGain, _Offset;
            float4 frag(v2f_img i) : SV_Target
            { return tex2D(_MainTex, i.uv) * _Gain + tex2D(_OtherTex, i.uv) * _OtherGain + _Offset; }
            ENDCG
        }
    }
}
