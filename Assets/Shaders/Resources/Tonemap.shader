Shader "Hidden/Morrowind Tonemap"
{
    SubShader
    {
        Cull Off
        ZClip Off
        ZTest Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma editor_sync_compilation
            #pragma vertex VertexFullscreenTriangleMinimal
            #pragma fragment Fragment
            #pragma multi_compile _ FLIP
            #include "Tonemap.hlsl"
            ENDHLSL
        }
    }
}