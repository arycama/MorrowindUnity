Shader "Hidden/Blit Material"
{
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ FLIP
            #include "BlitMaterial.hlsl"
            ENDHLSL
        }
    }
}