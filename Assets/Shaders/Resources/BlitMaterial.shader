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
            #include "BlitMaterial.hlsl"
            ENDHLSL
        }
    }
}