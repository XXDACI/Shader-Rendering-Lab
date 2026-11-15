// 方便快速编译 防止重复编译+包体变大
#ifndef STEP_BY_STEP_SHADER_INCLUDED
#define STEP_BY_STEP_SHADER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// 贴图声明
sampler2D _TextureA;

sampler2D _TextureB;

// Material Properties
CBUFFER_START(UnityPerMaterial)

float4 _ColorA;
float4 _ColorB;

float4 _TextureA_ST;
float4 _TextureB_ST;

float  _ColorAIntensity;
float  _ColorBIntensity;

CBUFFER_END

// vertex input
struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv         : TEXCOORD0;
};

// vertex out -> fragment input
struct Varyings
{
    float4 positionHCS : SV_Position;
    float2 uvA         : TEXCOORD0;
    float2 uvB         : TEXCOORD1;
};

Varyings Vert(Attributes IN)
{
    Varyings OUT;
    
    OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
    OUT.uvA = IN.uv * _TextureA_ST.xy + _TextureA_ST.zw;
    OUT.uvB = IN.uv * _TextureB_ST.xy + _TextureB_ST.zw;
    
    return OUT;
}

float4 Color_Intensity(float4 texColor, float4 intColor, float intensity)
{
    float4 layer;
    
    layer.rgb = texColor.rgb * intColor.rgb * intensity;
    layer.a = texColor.a * intColor.a;
    
    return layer;
}

float4 Frag(Varyings IN) : SV_Target
{
    float4 texAColor = tex2D(_TextureA, IN.uvA);
    float4 texBColor = tex2D(_TextureB, IN.uvB);

    float4 layerA = Color_Intensity(
    texAColor,
    _ColorA,
    _ColorAIntensity
    );
    
    float4 layerB = Color_Intensity(
    texBColor,
    _ColorB,
    _ColorBIntensity
    );

    float4 finalColor;
    
    finalColor.rgb = saturate(layerA.rgb + layerB.rgb);
    finalColor.a = saturate(layerA.a + layerB.a);
    
    return finalColor;

}

#endif