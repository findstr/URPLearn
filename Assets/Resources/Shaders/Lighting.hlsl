#ifndef LIGHTING_INCLUDED
#define LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Config.hlsl"
#include "Surface.hlsl"
#include "Brdf.hlsl"
#include "Shadows.hlsl"
#include "GI.hlsl"

CBUFFER_START(_CustomLight)
    int light_directional_count;
    half4 light_directional_color[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 light_directional_direction[MAX_DIRECTIONAL_LIGHT_COUNT];
    
    int _OtherLightCount;
    float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];

CBUFFER_END

struct light
{
    float3 color;
    float3 direction;
	float attenuation;
};


float square(float v)
{
    return v * v;
}

float specular_strength(surface s, BRDF brdf, light l)
{
    float3 h = SafeNormalize(l.direction + s.viewdir);
    float nh2 = square(saturate(dot(s.normal, h)));
    float lh2 = square(saturate(dot(l.direction, h)));
    float r2 = square(brdf.roughness);
    float d2 = square(nh2 * (r2 - 1.0) + 1.00001);
    float norm = brdf.roughness * 3.0 + 2.0;
    return r2 / (d2 * max(0.1, lh2)) * norm;
}

float3 LightRadiance(surface s, light l)
{
	return saturate(dot(s.normal, l.direction)) * l.color * l.attenuation;    
}
 
float3 direct_brdf(surface s, BRDF brdf, light l)
{
    return specular_strength(s, brdf, l) * brdf.specular + brdf.diffuse;
}

float3 LightingDirectional(surface s, BRDF brdf, light l)
{
    return LightRadiance(s, l) * direct_brdf(s, brdf, l);
}

int GetDirectionalLightCount()
{
    return light_directional_count;
}

int GetOtherLightCount()
{
    return _OtherLightCount;
}

light GetDirectionalLight(int idx, surface s, CascadeInfo ci)
{
	light l;
	l.color = light_directional_color[idx].rgb;
	l.direction = light_directional_direction[idx].xyz;
	l.attenuation = GetShadowAttenuation(idx, s, ci);
	return l;
}

light GetOtherLight(int idx, surface s, CascadeInfo ci)
{
	light l;
    
	OtherShadowInfo osi = GetOtherShadowData(idx);
    
	float3 dir = _OtherLightDirections[idx].xyz;
	float3 ray = _OtherLightPositions[idx].xyz - s.position;
	float4 spotAngles = _OtherLightSpotAngles[idx];
	l.color = _OtherLightColors[idx].rgb;
	l.direction = normalize(ray);
    
    
    float distSqr = max(dot(ray, ray), 0.000001);
    float rangeAttenuation = square(saturate(1.0 - square(distSqr * _OtherLightPositions[idx].w)));
	float spotAttenuation = square(saturate(dot(dir, l.direction) * spotAngles.x + spotAngles.y));
    
	l.attenuation = spotAttenuation * rangeAttenuation / distSqr * GetOtherShadowAttenuation(osi, s, ci);
	return l;
}

float3 GetLighting(surface s, BRDF brdf, GI gi)
{
    CascadeInfo ci = GetCascadeInfo(s);
    ci.shadowMask = gi.shadowMask;
    float3 color = IndirectBRDF(s, brdf, gi.diffuse, gi.specular);
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
		light l = GetDirectionalLight(i, s, ci);
		color += LightingDirectional(s, brdf, l);
	}
#if defined(_LIGHTS_PER_OBJECT)
    for (int j = 0; j < min(unity_LightData.y, 8); j++) {
        int lightIndex = unity_LightIndices[(uint)j/4][(uint)j%4];
        light l = GetOtherLight(lightIndex, s, ci);
		color += LightingDirectional(s, brdf, l);
    }
#else
	for (int j = 0; j < GetOtherLightCount(); j++) {
		light l = GetOtherLight(j, s, ci);
		color += LightingDirectional(s, brdf, l);
	}
#endif
	return color;
}

#endif
