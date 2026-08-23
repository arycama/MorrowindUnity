Shader "Hidden/Morrowind Deferred"
{
    SubShader
    {
        Pass
        {
            Blend One One
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
            #pragma multi_compile _ RAYTRACING_ON
            #pragma multi_compile _ MSAA_ON

            #ifdef RAYTRACING_ON
                #define SCREEN_SPACE_SHADOWS
            #endif

            #include "Deferred.hlsl"
            ENDHLSL
        }
    }
}