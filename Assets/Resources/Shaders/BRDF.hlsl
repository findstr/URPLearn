#ifndef BRDF_INCLUDED
#define BRDF_INCLUDED

#include "surface.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct BRDF {
    float3 diffuse;
    float3 specular;
    float roughness;
    float fresnel;
    float perceptualRoughness;
};

BRDF GetBRDF(surface s) 
{
    BRDF brdf;
    float oneMinusReflectivity = 1.0 - s.metallic;
    brdf.diffuse = s.color.rgb * OneMinusReflectivityMetallic(s.metallic);
    brdf.specular = lerp(kDielectricSpec.rgb, s.color.rgb, s.metallic);
    brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(s.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
    brdf.fresnel = saturate(s.smoothness + 1.0 - oneMinusReflectivity);
    return brdf;
} 

float3 IndirectBRDF(surface s, BRDF brdf, float3 diffuse, float3 specular)
{
    float fresnelStrength = s.fresnelStrength * Pow4(1.0 - saturate(dot(s.normal, s.viewdir)));
    float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
    reflection /= brdf.roughness * brdf.roughness + 1.0;
    return diffuse * brdf.diffuse + reflection;
}




#endif
