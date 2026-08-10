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
            #pragma require WaveMath
            #pragma multi_compile _ VOLUMETRIC_LIGHT_ON
            #pragma multi_compile _ SHADOWS_ON
            #pragma multi_compile _ POINT_LIGHTS_ON
            #include "Deferred.hlsl"
            ENDHLSL
        }
    }
}