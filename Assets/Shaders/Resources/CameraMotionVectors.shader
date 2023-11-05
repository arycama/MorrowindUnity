Shader "Hidden/Camera Motion Vectors"
{
    SubShader
    {
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            Stencil
            {
                Ref 1
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "CameraMotionVectors.hlsl"
            ENDHLSL
          
        }
    }
}