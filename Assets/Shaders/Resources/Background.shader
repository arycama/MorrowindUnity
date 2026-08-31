Shader "Hidden/Background"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Equal

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma use_dxc
            #pragma multi_compile _ VOLUMETRIC_LIGHT_ON
            #include "Background.hlsl"
            ENDHLSL
        }
    }
}