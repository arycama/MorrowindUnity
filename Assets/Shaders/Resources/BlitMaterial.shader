Shader "Hidden/Blit Material"
{
    SubShader
    {
        Pass
        {
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ FLIP
            #pragma multi_compile _ MSAA
            #pragma multi_compile _ DIRECT
            #pragma multi_compile _ RAYTRACED_DEPTH_OF_FIELD
            #pragma multi_compile _ BLOOM
            #include "BlitMaterial.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ FLIP
            #pragma multi_compile _ MSAA
            #pragma multi_compile _ RAYTRACED_DEPTH_OF_FIELD
            #pragma multi_compile _ BLOOM
            #define DEPTH
            #include "BlitMaterial.hlsl"
            ENDHLSL
        }
    }
}