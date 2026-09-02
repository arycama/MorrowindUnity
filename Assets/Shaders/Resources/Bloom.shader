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
            #pragma use_dxc
            #include "Bloom.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Downsample"
        
            HLSLPROGRAM
            #pragma vertex VertexFullscreenTriangleMinimal
            #pragma fragment FragmentDownsample
            #pragma use_dxc
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
            #pragma use_dxc
            #include "Bloom.hlsl"
            ENDHLSL
        }
    }
}