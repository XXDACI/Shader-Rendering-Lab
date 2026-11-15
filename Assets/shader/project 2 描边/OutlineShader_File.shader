Shader "Custom/OutlineShader_File"
{
    Properties
    {
        _TexColor("Texture Color", Color) = (1, 1, 1, 1)
        _Texture("Texture Map", 2D) = "white" {}

        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Float) = 2

        [Enum(Object,0,World,1,View,2,Clip,3)]
        _OutlineType ("Outline Space", Float) = 3

        [Enum(Original,0,RG,1,Octahedron,2)]
        _OutlineNormalMode ("Normal Mode", Float) = 0

        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex TexVert
            #pragma fragment TexFrag

            #include "OutlineShader.hlsl"

            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag

            #include "OutlineShader.hlsl"

            ENDHLSL
        }
    }
}
