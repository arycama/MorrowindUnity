Shader "Hidden/Morrowind Deferred"
{
    SubShader
    {
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma require WaveMath
            #pragma multi_compile _ VOLUMETRIC_LIGHT_ON
            #pragma multi_compile _ SHADOWS_ON
            #pragma multi_compile _ POINT_LIGHTS_ON
            #pragma multi_compile _ MSAA_ON

            #pragma multi_compile _ RAYTRACED_OCCLUSION
            #pragma multi_compile _ RAYTRACED_SHADOWS
            #pragma multi_compile _ RAYTRACED_DIFFUSE

            #ifdef RAYTRACED_SHADOWS
                #define SCREEN_SPACE_SHADOWS
            #endif

            #include "Deferred.hlsl"
            ENDHLSL
        }
    }
}