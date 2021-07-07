#ifndef SHADOWS_PASS_INCLUDED
#define SHADOWS_PASS_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Config.hlsl"
#include "Surface.hlsl"

#define MAX_SHADOW_COUNT	4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	
    float4x4 _DirectionalShadowMatrices[MAX_VISIBLE_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_VISIBLE_LIGHT_COUNT];

CBUFFER_END

struct XShadowData
{
    float strength;
    int tiledIndex;
};

XShadowData GetShadowData(int light)
{
    XShadowData sd;
    float2 data =  _DirectionalLightShadowData[light].xy;
    sd.strength = data.x;
    sd.tiledIndex = data.y;
    return sd;
}

float GetShadowAttenuation(int light, surface sf)
{
    XShadowData sd = GetShadowData(light);
    if (sd.strength <= 0.0)
        return 1.0;
    float3 posSM = mul(_DirectionalShadowMatrices[light], float4(sf.position, 1.0)).xyz;
    float shadow = SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, posSM);
    return lerp(1.0, shadow, sd.strength);
}

#endif
