Shader "Hidden/Blit Material"
{
    SubShader
    {
        Cull Off
        ZClip Off
        ZTest Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "BlitMaterial.hlsl"
            ENDHLSL
        }
    }
}