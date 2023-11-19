Shader "Hidden/Bloom"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        
        Pass
        {
            Name "Downsample"
        
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentDownsample
            #include "Bloom.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
        
            Name "Upsample"
            
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentUpsample
            #include "Bloom.hlsl"
            ENDHLSL
        }
    }
}