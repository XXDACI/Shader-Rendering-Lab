#ifndef STEP_BY_STEP_INCLUDED
#define STEP_BY_STEP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_Texture);
SAMPLER(sampler_Texture);

CBUFFER_START(UnityPerMaterial)

float4 _TexColor;
float4 _Texture_ST;
float _Cutoff;

float4 _OutlineColor;
float _OutlineWidth;
float _OutlineType;
float _OutlineNormalMode;

CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
    float2 uv2 : TEXCOORD1;
    float3 smoothNormalOS : TEXCOORD2;
};

struct Varying
{
    float4 positionHCS : SV_Position;
    float2 uv : TEXCOORD0;
};

float3 SafeNormalize(float3 value, float3 fallback)
{
    return dot(value, value) > 0.0000001 ? normalize(value) : fallback;
}

float GetOutlineMask(Attributes IN)
{
    return saturate(IN.color.a);
}

float3 TangentNormalToObjectNormal(Attributes IN, float3 normalTS)
{
    float3 normalOS = SafeNormalize(IN.normalOS, float3(0.0, 0.0, 1.0));
    float3 tangentOS = SafeNormalize(IN.tangentOS.xyz, float3(1.0, 0.0, 0.0));
    tangentOS = SafeNormalize(tangentOS - normalOS * dot(normalOS, tangentOS), float3(1.0, 0.0, 0.0));
    float3 bitangentOS = cross(normalOS, tangentOS) * (IN.tangentOS.w < 0.0 ? -1.0 : 1.0);

    float3 outlineNormalOS = tangentOS * normalTS.x + bitangentOS * normalTS.y + normalOS * normalTS.z;

    return SafeNormalize(outlineNormalOS, normalOS);
}

float3 RGDecode(float2 rg)
{
    float theta = (rg.x - 0.5) * 6.2831853;
    float phi = rg.y * 3.1415926;

    float sinPhi = sin(phi);

    float3 n;
    n.x = cos(theta) * sinPhi;
    n.y = cos(phi);
    n.z = sin(theta) * sinPhi;

    return normalize(n);
}

float3 OctahedronToUnitVector(float2 oct)
{
    oct = oct * 2.0 - 1.0;

    float3 n = float3(oct, 1.0 - dot(float2(1.0, 1.0), abs(oct)));

    if (n.z < 0.0)
    {
        n.xy = (1.0 - abs(n.yx)) * (n.xy >= 0.0 ? float2(1.0, 1.0) : float2(-1.0, -1.0));
    }

    return normalize(n);
}

float3 GetOutlineNormalTS(Attributes IN)
{
    switch (_OutlineNormalMode)
    {
        case 1:
            return RGDecode(IN.color.rg);

        case 2:
            return OctahedronToUnitVector(IN.uv2.xy);

        default:
            return SafeNormalize(IN.smoothNormalOS, float3(0.0, 0.0, 1.0));
    }
}

bool HasEncodedOutlineNormals(Attributes IN)
{
    // Encoder 会在 UV3 里写入单位长度的切线空间法线
    float lengthSquared = dot(IN.smoothNormalOS, IN.smoothNormalOS);
    return abs(lengthSquared - 1.0) < 0.01;
}

float3 GetOutlineNormalOS(Attributes IN)
{
    // Original 模式，或没有烘焙数据时，使用模型原始法线
    if (_OutlineNormalMode < 0.5 || !HasEncodedOutlineNormals(IN))
    {
        return SafeNormalize(IN.normalOS, float3(0, 0, 1));
    }

    // 有烘焙数据时，解码后从切线空间转回模型空间
    return TangentNormalToObjectNormal(IN, GetOutlineNormalTS(IN));
}

float4 OutLinePos_Object(Attributes IN)
{
    float mask = GetOutlineMask(IN);
    float3 position = IN.positionOS.xyz;
    float3 normal = GetOutlineNormalOS(IN);

    position += normal * _OutlineWidth * 0.01 * mask;

    return TransformObjectToHClip(position);
}

float4 OutLinePos_World(Attributes IN)
{
    float mask = GetOutlineMask(IN);
    float3 positionWorld = TransformObjectToWorld(IN.positionOS.xyz);

    float3 normalOS = GetOutlineNormalOS(IN);
    float3 normalWorld = TransformObjectToWorldNormal(normalOS);
    float3 normal = SafeNormalize(normalWorld, float3(0.0, 1.0, 0.0));

    positionWorld += normal * _OutlineWidth * 0.01 * mask;

    return TransformWorldToHClip(positionWorld);
}

float4 OutLinePos_View(Attributes IN)
{
    float mask = GetOutlineMask(IN);
    float3 positionWorld = TransformObjectToWorld(IN.positionOS.xyz);
    float3 positionView = TransformWorldToView(positionWorld);

    float3 normalOS = GetOutlineNormalOS(IN);
    float3 normalWorld = TransformObjectToWorldNormal(normalOS);
    float3 normalViewRaw = mul((float3x3) UNITY_MATRIX_V, normalWorld);
    float3 normalView = SafeNormalize(normalViewRaw, float3(0.0, 1.0, 0.0));

    positionView += normalView * _OutlineWidth * 0.01 * mask;

    return mul(UNITY_MATRIX_P, float4(positionView, 1.0));
}

float4 OutLinePos_Clip(Attributes IN)
{
    float mask = GetOutlineMask(IN);
    float3 positionWorld = TransformObjectToWorld(IN.positionOS.xyz);
    float3 positionView = TransformWorldToView(positionWorld);

    float3 normalOS = GetOutlineNormalOS(IN);
    float3 normalWorld = TransformObjectToWorldNormal(normalOS);
    float3 normalViewRaw = mul((float3x3) UNITY_MATRIX_V, normalWorld);
    float3 normalView = SafeNormalize(normalViewRaw, float3(0.0, 1.0, 0.0));

    float4 positionClip = TransformWorldToHClip(positionWorld);
    float4 offsetClip = mul(UNITY_MATRIX_P, float4(positionView + normalView * 0.01, 1.0));

    float2 positionNDC = positionClip.xy / positionClip.w;
    float2 offsetNDC = offsetClip.xy / offsetClip.w;

    float2 direction = offsetNDC - positionNDC;
    float directionLength = length(direction);

    if (directionLength > 0.00001 && mask > 0.00001)
    {
        direction /= directionLength;

        float2 pixelSize = 2.0 / _ScreenParams.xy;
        positionClip.xy += direction * pixelSize * _OutlineWidth * mask * positionClip.w;
    }

    return positionClip;
}

float4 OutLinePos(Attributes IN)
{
    switch (_OutlineType)
    {
        case 1:
            return OutLinePos_World(IN);

        case 2:
            return OutLinePos_View(IN);

        case 3:
            return OutLinePos_Clip(IN);

        default:
            return OutLinePos_Object(IN);
    }
}

Varying OutlineVert(Attributes IN)
{
    Varying OUT;

    OUT.positionHCS = OutLinePos(IN);
    OUT.uv = TRANSFORM_TEX(IN.uv, _Texture);

    return OUT;
}

half4 OutlineFrag(Varying IN) : SV_Target
{
    float4 texColor = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, IN.uv);
    float alpha = texColor.a * _TexColor.a;

    clip(alpha - _Cutoff);

    return _OutlineColor;
}

Varying TexVert(Attributes IN)
{
    Varying OUT;

    OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
    OUT.uv = TRANSFORM_TEX(IN.uv, _Texture);

    return OUT;
}

half4 TexFrag(Varying IN) : SV_Target
{
    float4 texColor = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, IN.uv);
    float4 finalColor = texColor * _TexColor;

    clip(finalColor.a - _Cutoff);

    return finalColor;
}

#endif
