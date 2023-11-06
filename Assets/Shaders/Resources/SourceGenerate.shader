Shader "ConvolutionBloom/SourceGenerate"
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
            #include "SourceGenerate.hlsl"
            ENDHLSL
        }
    }
}
