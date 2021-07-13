#ifndef BRDF_INCLUDED
#define BRDF_INCLUDED

#include "surface.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct BRDF {
    float3 diffuse;
    float3 specular;
    float roughness;
};

BRDF GetBRDF(surface s) 
{
    BRDF brdf;
    float oneMinusReflectivity = 1.0 - s.metallic;
    brdf.diffuse = s.color.rgb * OneMinusReflectivityMetallic(s.metallic);
    brdf.specular = lerp(kDielectricSpec.rgb, s.color.rgb, s.metallic);
    brdf.roughness = PerceptualRoughnessToRoughness(PerceptualSmoothnessToPerceptualRoughness(s.smoothness));
    return brdf;
} 


#endif
