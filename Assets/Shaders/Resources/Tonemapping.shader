Shader"Hidden/Tonemapping"
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
            #include "Tonemapping.hlsl"
            ENDHLSL
          
        }
    }
}