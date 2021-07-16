#ifndef SURFACE_INCLUDED
#define SURFACE_INCLUDED

struct surface
{
    float3 position;
    float4 color;
    float3 normal;
    float metallic;
    float smoothness;
    float3 viewdir;
    float depth;
    float fresnelStrength;
    float dither;
};

#endif
