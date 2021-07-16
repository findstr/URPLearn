#ifndef SHADOWS_PASS_INCLUDED
#define SHADOWS_PASS_INCLUDED

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Config.hlsl"
#include "Surface.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES  4
    #define DIRECTIONAL_FILTER_SETUP    SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES  9
    #define DIRECTIONAL_FILTER_SETUP    SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES  16
    #define DIRECTIONAL_FILTER_SETUP    SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOW_COUNT	4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)

    int _CascadeCount;
    float4 _ShadowDistance;
    float4 _ShadowAtlasSize;
    float4 _CascadeSphere[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];

CBUFFER_END

struct ShadowMask
{
    bool always;
    bool distance;
    float4 shadows;
};

struct CascadeInfo
{
    int cascadeIndex;
    float strength;
    float blend;
    ShadowMask shadowMask;
};

struct ShadowInfo
{
    float strength;
    int tiledIndex;
    float normaBias;
    int shadowMaskChannel;
};

float DistanceSquared(float3 a, float3 b)
{
    float3 n = a - b;
    return dot(n, n);
}

float FadeShadowStrength(float depth)
{
    float oneDivMaxDist = _ShadowDistance.y;
    float fade = _ShadowDistance.z;
    return saturate((1.0 - depth * oneDivMaxDist) * fade);
}

float FadeCascadeStrength(int i, float distSqr, float4 sphere)
{
    return saturate((1.0 - distSqr * _CascadeData[i].x) * _ShadowDistance.w);
}

CascadeInfo GetCascadeInfo(surface s)
{
    int i;
    CascadeInfo ci;
    ci.cascadeIndex = 0;
    ci.strength = 0.0;
    ci.blend = 1.0;
    ci.shadowMask.distance = false;
    ci.shadowMask.shadows = 1.0;
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeSphere[i];
        float dist = DistanceSquared(sphere.xyz, s.position.xyz);
        if (dist < sphere.w) {
            float fade = FadeCascadeStrength(i, dist, sphere);
            ci.strength = FadeShadowStrength(s.depth);
            if (i == _CascadeCount - 1) {
                ci.strength *= fade;
            } else {
                ci.blend = fade;
            }
#if defined(_CASCADE_BLEND_DITHER)
            if (fade < s.dither)
                i += 1;
#endif
            ci.cascadeIndex = i;
            break;
        }
    }
    #if !defined(_CASCADE_BLEND_SOFT)
        ci.blend = 1.0;
	#endif
    ci.cascadeIndex = i;
    return ci;
}

ShadowInfo GetShadowData(int light, CascadeInfo ci)
{
    ShadowInfo sd;
    float4 data =  _DirectionalLightShadowData[light].xyzw;
    sd.strength = data.x;
    sd.tiledIndex = data.y + ci.cascadeIndex;
    sd.normaBias = data.z;
    sd.shadowMaskChannel = data.w;
    return sd;
}

float FilterDirectionalShadow(float3 posSM)
{
#if defined(DIRECTIONAL_FILTER_SETUP)
    float  weights[DIRECTIONAL_FILTER_SAMPLES];
    float2 positions[DIRECTIONAL_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.yyxx;
    DIRECTIONAL_FILTER_SETUP(size, posSM.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
    {
        shadow += weights[i] * SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, float3(positions[i].xy, posSM.z));
    }
    return shadow;
#else
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, posSM);
#endif
}

float GetCascadedShadow(ShadowInfo sd, CascadeInfo ci, surface sf)
{
    float3 normalBias = sf.normal * (sd.normaBias * _CascadeData[ci.cascadeIndex].y);
    float3 posSM = mul(_DirectionalShadowMatrices[sd.tiledIndex], float4(sf.position + normalBias, 1.0)).xyz;
    float shadow = FilterDirectionalShadow(posSM);
    if (ci.blend < 1.0) {
        normalBias = sf.normal * (sd.normaBias * _CascadeData[ci.cascadeIndex + 1].y);
        posSM = mul(_DirectionalShadowMatrices[sd.tiledIndex+1], float4(sf.position + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(posSM), shadow, ci.blend);
    }
    return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel)
{
    float shadow = 1.0;
    if (mask.always || mask.distance) {
        if (channel >= 0)
            shadow = mask.shadows[channel];
    }
    return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
    float shadow = 1.0;
    if (mask.always || mask.distance)
        shadow = lerp(1.0, GetBakedShadow(mask, channel), strength);
    return shadow;
}

float MixBakedAndRealtimeShadows(CascadeInfo ci, float shadow, int channel, float strength)
{
    float baked = GetBakedShadow(ci.shadowMask, channel);
    if (ci.shadowMask.always)
    {
        shadow = lerp(1.0, shadow, ci.strength);
        shadow = min(baked, shadow);
        return lerp(1.0, shadow, strength);
    }
    if (ci.shadowMask.distance)
    {
        shadow = lerp(baked, shadow, ci.strength);
        return lerp(1.0, shadow, strength);
    }
    return lerp(1.0, shadow, strength * ci.strength);
}

float GetShadowAttenuation(int light, surface sf, CascadeInfo ci)
{
    float shadow;
    ShadowInfo sd = GetShadowData(light, ci);
    if (sd.strength <= 0.0) {
        shadow = 1.0;
    } else {
        shadow = GetCascadedShadow(sd, ci, sf);
        shadow = MixBakedAndRealtimeShadows(ci, shadow, sd.shadowMaskChannel, sd.strength);
    }
    return shadow;
}

#endif
