#ifndef CUSTOM_META_PASS_INCLUDED
#define X_META_PASS_INCLUDED

#include "surface.hlsl"
#include "shadows.hlsl"
#include "lighting.hlsl"
#include "BRDF.hlsl"

bool4 unity_MetaFragmentControl;
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

struct Attribute
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    float2 lightMapUV : TEXCOORD1;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
};

Varyings MetaPassVertex(Attribute input)
{
    Varyings o;
    input.positionOS.xy = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0.0;
    o.positionCS = TransformWorldToHClip(input.positionOS);
    o.baseUV = TransformBaseUV(input.baseUV);
    return o;
}

float4 MetaPassFragment(Varyings input) : SV_TARGET
{
    surface s;
    BRDF brdf;
    float4 meta = 0.0;
    ZERO_INITIALIZE(surface, s);
    s.color = GetBase(input.baseUV);
    s.metallic = GetMetallic(input.baseUV);
    s.smoothness = GetSmoothness(input.baseUV);
    brdf = GetBRDF(s);
    if (unity_MetaFragmentControl.x) {
        meta = float4(brdf.diffuse, 1.0);
        meta.rgb += brdf.specular * brdf.roughness * 0.5;
        meta.rgb = min(PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue);
    } else if (unity_MetaFragmentControl.y) {
        meta = float4(GetEmission(input.baseUV), 1.0);
    }
    return meta;
}


#endif
