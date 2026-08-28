Shader "Hidden/Blit Material"
{
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma use_dxc
            #pragma multi_compile _ FLIP
            #pragma multi_compile _ DEPTH
            #pragma multi_compile _ MSAA
            #pragma multi_compile _ DIRECT
            #pragma multi_compile _ DEPTH_OF_FIELD
            #include "BlitMaterial.hlsl"
            ENDHLSL
        }
    }
}