Shader "Custom/shader_file"
{
    Properties
    {
        _ColorA("Color A", Color) = (1, 1, 1, 1)
        _TextureA("Texture A", 2D) = "white"{}
        _ColorAIntensity("Color A Intensity", Float) = 1

        _ColorB("Color B", Color) = (1, 1, 1, 1)
        _TextureB("Texture B", 2D) = "white"{}
        _ColorBIntensity("Color B Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Assets/shader/project 1 材质叠加/shader.hlsl"

            ENDHLSL
        }
    }
}
