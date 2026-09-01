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
            #pragma use_dxc
            #pragma multi_compile _ FLIP
            #pragma multi_compile _ MSAA
            #pragma multi_compile _ DIRECT
            #pragma multi_compile _ DEPTH_OF_FIELD
            #include "BlitMaterial.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma use_dxc
            #pragma multi_compile _ FLIP
            #pragma multi_compile _ MSAA
            #pragma multi_compile _ DEPTH_OF_FIELD
            #define DEPTH
            #include "BlitMaterial.hlsl"
            ENDHLSL
        }
    }
}