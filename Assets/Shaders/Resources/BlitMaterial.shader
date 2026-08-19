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
            #pragma multi_compile _ DEPTH DEPTH_MSAA_2 DEPTH_MSAA_4 DEPTH_MSAA_8
            #include "BlitMaterial.hlsl"
            ENDHLSL
        }
    }
}