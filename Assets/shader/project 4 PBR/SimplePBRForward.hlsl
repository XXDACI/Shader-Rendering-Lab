#ifndef SHADER_PRACTICE_SIMPLE_PBR_ENVIRONMENT_INCLUDED
#define SHADER_PRACTICE_SIMPLE_PBR_ENVIRONMENT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half _Metallic;
half _Smoothness;
half _Occlusion;
CBUFFER_END

struct PBRAttributes
{
    float4 positionOS : POSITION;
    half3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PBRVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    half fogFactor : TEXCOORD3;
    
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

PBRVaryings PBRVert(PBRAttributes input)
{
    PBRVaryings output = (PBRVaryings) 0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

    output.positionCS = positions.positionCS;
    output.positionWS = positions.positionWS;
    output.normalWS = normals.normalWS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fogFactor = ComputeFogFactor(positions.positionCS.z);
    return output;
}
    
half DisneyDiffuse(half NdotV, half NdotL, half LdotH, half roughness)
{
    half fd90 = 0.5 + 2.0 * LdotH * LdotH * roughness;
    half lightScatter = 1.0 + (fd90 - 1.0) * pow(1.0 - NdotL, 5.0);
    half viewScatter = 1.0 + (fd90 - 1.0) * pow(1.0 - NdotV, 5.0);
    return lightScatter * viewScatter;
}

half DistributionGGX(half NdotH, half roughness)
{
    half r = max(roughness, 0.003);
    half r2 = r * r;
    half d = NdotH * NdotH * (r2 - 1.0h) + 1.0h;
    return r2 / max(PI * d * d, 0.0001h);
}

half GeometrySmith(half NdotV, half NdotL, half roughness)
{
    float k = (roughness + 1.0) * (roughness + 1.0) * 0.125;
    half lightTere = NdotL / lerp(NdotL, 1.0, k);
    half viewTere = NdotV / lerp(NdotV, 1.0, k);
    return lightTere * viewTere;
}

half3 FresnelSchlick(half VdotH, half3 f0)
{
    return f0 + (1.0 - f0) * exp2((-5.55473 * VdotH - 6.98316) * VdotH);
}

half3 FresnelSchlickRoughness(half NdotV, half3 f0, half roughness)
{
    half3 grazing = max((1.0h - roughness).xxx, f0);
    return f0 + (grazing - f0) * exp2((-5.55473 * NdotV - 6.98316) * NdotV);
}

half3 EvaluateDirectLight(
half3 albedo,
half metallic,
half roughness,
half3 f0,
half3 normalWS,
half3 viewDirectionWS,
Light light)
{
    half3 lightDirectionWS = SafeNormalize(light.direction);
    half3 halfDirectionWS = SafeNormalize(lightDirectionWS + viewDirectionWS);

    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    half NdotV = max(saturate(dot(normalWS, viewDirectionWS)), 0.0001h);
    half NdotH = saturate(dot(normalWS, halfDirectionWS));
    half VdotH = saturate(dot(viewDirectionWS, halfDirectionWS));
    half LdotH = saturate(dot(lightDirectionWS, halfDirectionWS));

    ///////////////////////////////BRDF////////////////////////////////////////////
    // D: GGX 法线分布
    half D = DistributionGGX(NdotH, roughness);
    // G: 几何遮蔽
    half G = GeometrySmith(NdotV, NdotL, roughness);
    // F: 菲尼尔
    half3 F = FresnelSchlick(VdotH, f0);
    // 镜面反射
    half3 specular = D * G * F / max(4.0h * NdotV * NdotL, 0.0001h);

    half3 kd = (1.0h - F) * (1.0h - metallic);
    half disneyDiffuse = DisneyDiffuse(NdotV, NdotL, LdotH, roughness);
    half3 diffuse = kd * albedo * disneyDiffuse / PI;
    half3 brdf = diffuse + specular;
    ///////////////////////////////////////////////////////////////////////////////
        
    half attenuation = light.distanceAttenuation * light.shadowAttenuation;
    return brdf * light.color * attenuation * NdotL;
}

half4 PBRFrag(PBRVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
    half3 albedo = baseSample.rgb;
    half metallic = saturate(_Metallic);
    half roughness = max(1.0h - saturate(_Smoothness), 0.045h);
    half3 normalWS = SafeNormalize(input.normalWS);
    half3 viewDirectionWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(input.positionWS));
    half3 f0 = lerp(0.04h.xxx, albedo, metallic);

    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half3 color = EvaluateDirectLight(
    albedo, metallic, roughness, f0,
    normalWS, viewDirectionWS, mainLight);
        
        

    half NdotV = saturate(dot(normalWS, viewDirectionWS));
    half3 environmentFresnel = FresnelSchlickRoughness(
    NdotV, f0, roughness);
    half3 environmentKd = (1.0h - environmentFresnel) * (1.0h - metallic);
    half3 indirectDiffuse = SampleSH(normalWS) * albedo * environmentKd;
    half3 reflectionWS = reflect(-viewDirectionWS, normalWS);

    half3 indirectSpecular = GlossyEnvironmentReflection(
    reflectionWS,
    input.positionWS,
    roughness,
    _Occlusion,
    GetNormalizedScreenSpaceUV(input.positionCS));


    color += indirectDiffuse * _Occlusion + indirectSpecular * environmentFresnel;
    color = MixFog(color, input.fogFactor);
    return half4(color, baseSample.a);
}
#endif
