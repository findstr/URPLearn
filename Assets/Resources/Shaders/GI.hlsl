#ifndef X_GI_INCLUDED
#define X_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "surface.hlsl"
#include "Shadows.hlsl"

#if defined(LIGHTMAP_ON)

#define GI_ATTRIBUTE_DATA           float2 lightMapUV:TEXCOORD1;
#define GI_VARYINGS_DATA            float2 lightMapUV:VAR_LIGHT_MAP_UV;
#define GI_TRANSFER_DATA(ii, oo)    oo.lightMapUV = ii.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
#define GI_FRAGMENT_DATA(i)         i.lightMapUV.xy

#else

#define GI_ATTRIBUTE_DATA
#define GI_VARYINGS_DATA
#define GI_TRANSFER_DATA(ii, oo)
#define GI_FRAGMENT_DATA(i) 0.0

#endif

struct GI
{
    float3 diffuse;
    float3 specular;
    ShadowMask shadowMask;
};

float3 SampleLightMap(float2 uv)
{
#if defined(LIGHTMAP_ON)
    return SampleSingleLightmap(
        TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap),
        uv, 
        float4(1,1,0,0),
#if defined(UNITY_LIGHTMAP_FULL_HDR)
        false,
#else
        true,
#endif
        float4(LIGHTMAP_HDR_MULTIPLIER,LIGHTMAP_HDR_EXPONENT, 0,0)
    );
#else
    return 0.0;
#endif
}

float3 SampleLightProbe(surface s)
{
#if defined(LIGHTMAP_ON)
    return 0.0;   
#else
    float4 coefficients[7];
    coefficients[0] = unity_SHAr;
    coefficients[1] = unity_SHAg;
    coefficients[2] = unity_SHAb;
    coefficients[3] = unity_SHBr;
    coefficients[4] = unity_SHBg;
    coefficients[5] = unity_SHBb;
    coefficients[6] = unity_SHC;
    return max(0.0, SampleSH9(coefficients, s.normal));
#endif
}

float4 SampleBakedShadows(float2 lightMapUV)
{
#if defined(LIGHTMAP_ON)
    return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV);
#else
    return unity_ProbesOcclusion;
#endif
}

float3 SampleEnvironment(surface s, BRDF brdf)
{
    float3 uvw = reflect(-s.viewdir, s.normal);
    float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    float4 env = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, uvw, mip);
    return DecodeHDREnvironment(env, unity_SpecCube0_HDR);
}

GI GetGI(float2 lightMapUV, surface s, BRDF brdf)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(s);
    gi.specular = SampleEnvironment(s, brdf);
    gi.shadowMask.distance = false;
    gi.shadowMask.always = false;
    gi.shadowMask.shadows = 1.0;
#if defined(_SHADOW_MASK_ALWAYS)
    gi.shadowMask.always = true;
    gi.shadowMask.shadows = SampleBakedShadows(lightMapUV);
#elif defined(_SHADOW_MASK_DISTANCE)
    gi.shadowMask.distance = true;
    gi.shadowMask.shadows = SampleBakedShadows(lightMapUV);
#endif
    return gi;
}


#endif
