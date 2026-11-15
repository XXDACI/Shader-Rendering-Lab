Shader "Custom/VertexColorPreview_Header"
{
    Properties
    {
        _ColorMultiplier ("Color Multiplier", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "VertexColorPreviewPass"

            Cull Back

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Assets/shader/project 3 VertexColorPreview/VertexColorPreviewPass.hlsl"

            ENDHLSL
        }
    }

    FallBack Off
}