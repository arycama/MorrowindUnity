Shader"Hidden/Temporal AA"
{
    SubShader
    {
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "TemporalAA.hlsl"
            ENDHLSL
          
        }
    }
}