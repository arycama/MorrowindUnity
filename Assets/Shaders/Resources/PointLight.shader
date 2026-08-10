Shader "Hidden/Morrowind Point Light"
{
    SubShader
    {
        ColorMask 0
        ZWrite Off

        Pass
        {
            // Intersecting lights, inverted. (Note the ztest can be greater.. but we want to write for volumetrics)
            Cull Front
            ZTest Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma use_dxc
            #include "PointLight.hlsl"
            ENDHLSL
        }

        Pass
        {
            // Non-intersecting lights, normal
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma use_dxc
            #include "PointLight.hlsl"
            ENDHLSL
        }
    }
}