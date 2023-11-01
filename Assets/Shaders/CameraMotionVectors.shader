Shader "Hidden/Camera Motion Vectors"
{
    SubShader
    {
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            //Stencil
            //{
            //    Ref 0
            //    Comp Equal
            //    ReadMask 6
            //}

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma enable_d3d11_debug_symbols
            #include "CameraMotionVectors.hlsl"
            ENDHLSL
          
        }
    }
}