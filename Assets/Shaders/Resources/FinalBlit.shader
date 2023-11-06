Shader "ConvolutionBloom/FinalBlit"
{
    SubShader
    {
        Cull Off 
        ZWrite Off 
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "FinalBlit.hlsl"
            ENDHLSL
        }
    }
}
