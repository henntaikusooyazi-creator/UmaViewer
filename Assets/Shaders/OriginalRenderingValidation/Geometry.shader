Shader "Hidden/TestOnly/OriginalRenderingGeometry"
{
    Properties { _TestColor ("Artificial linear color", Vector) = (1, 1, 1, 1) }
    SubShader
    {
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _TestColor;
            float4 vert(float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
            float4 frag() : SV_Target { return _TestColor; }
            ENDCG
        }
    }
}
