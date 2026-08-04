Shader "Hidden/Morrowind Deferred"
{
    SubShader
    {
        Pass
        {
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Greater

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma use_dxc
            #pragma require WaveMath
            #pragma multi_compile _ VOLUMETRIC_LIGHT_ON
            #include "Deferred.hlsl"
            ENDHLSL
        }
    }
}