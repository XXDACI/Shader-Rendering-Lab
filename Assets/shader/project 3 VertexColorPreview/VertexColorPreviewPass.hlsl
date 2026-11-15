#ifndef VERTEX_COLOR_PREVIEW_PASS_INCLUDED
#define VERTEX_COLOR_PREVIEW_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _ColorMultiplier;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float4 color : COLOR;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    half4 color : COLOR;
};

Varyings Vert(Attributes IN)
{
    Varyings OUT;

    OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

    OUT.color = IN.color;

    return OUT;
}

half4 Frag(Varyings IN) : SV_Target
{
    half4 finalColor = IN.color * _ColorMultiplier;

    return finalColor;
}

#endif