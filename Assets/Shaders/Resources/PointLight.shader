Shader "Hidden/Point Light"
{
    SubShader
    {
        //Cull Front
        //ZClip Off
        ZWrite Off

        Pass
        {
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma use_dxc
            #pragma require waveMath
            #include "PointLight.hlsl"
            ENDHLSL
        }
    }
}