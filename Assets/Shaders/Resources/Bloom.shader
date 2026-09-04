Shader "Hidden/Morrowind Bloom"
{
    SubShader
    {
        Cull Off
        ZClip Off
        ZTest Off
        ZWrite Off

        Pass
        {
            Name "Downsample First"
        
            HLSLPROGRAM
            #pragma vertex VertexFullscreenTriangleMinimal
            #pragma fragment FragmentDownsample
            #define FIRST
            #include "Bloom.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Downsample"
        
            HLSLPROGRAM
            #pragma vertex VertexFullscreenTriangleMinimal
            #pragma fragment FragmentDownsample
            #include "Bloom.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
        
            Name "Upsample"
            
            HLSLPROGRAM
            #pragma vertex VertexFullscreenTriangleMinimal
            #pragma fragment FragmentUpsample
            #include "Bloom.hlsl"
            ENDHLSL
        }
    }
}