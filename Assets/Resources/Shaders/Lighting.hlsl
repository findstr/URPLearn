#ifndef LIGHTING_INCLUDED
#define LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Config.hlsl"
#include "Surface.hlsl"
#include "Brdf.hlsl"
#include "Shadows.hlsl"

CBUFFER_START(_CustomLight)
    int light_directional_count;
    half4 light_directional_color[MAX_VISIBLE_LIGHT_COUNT];
    float4 light_directional_direction[MAX_VISIBLE_LIGHT_COUNT];
CBUFFER_END

struct light
{
    float3 color;
    float3 direction;
};


light get_direciontal_light(int idx)
{
    light l;
    l.color = light_directional_color[idx].rgb;
    l.direction = light_directional_direction[idx].xyz;
    return l;
}

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

float3 light_radiance(surface s, light l, int light, CascadeInfo ci)
{
    return saturate(dot(s.normal, l.direction)) * l.color * GetShadowAttenuation(light, s, ci);
}
 
float3 direct_brdf(surface s, BRDF brdf, light l)
{
    return specular_strength(s, brdf, l) * brdf.specular + brdf.diffuse;
}

float3 lighting_directional(surface s, BRDF brdf, int i, CascadeInfo ci)
{
    light l = get_direciontal_light(i);
    return light_radiance(s, l, i, ci) * direct_brdf(s, brdf, l);
}

#endif
