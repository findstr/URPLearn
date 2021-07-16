#ifndef X_LIT_PASS_INCLUDED
#define X_LIG_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Common.hlsl"
#include "surface.hlsl"
#include "BRDF.hlsl"
#include "GI.hlsl"
#include "Lighting.hlsl"
#include "Shadows.hlsl"

#define M_PI                        3.1415926535897932384626433832795

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float3 normal: NORMAL;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float3 normal: VAR_NORMAL;
    float3 positionWS: VAR_POSITION;
    float4 positionCS: SV_POSITION;
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

v2f LitPassVertex (appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    GI_TRANSFER_DATA(v, o);
    o.uv.xy = TransformBaseUV(v.uv);
    o.normal = TransformObjectToWorldNormal(v.normal);
    o.positionCS = TransformObjectToHClip(v.vertex.xyz);
    o.positionWS = TransformObjectToWorld(v.vertex.xyz);
    return o;
}

half4 LitPassFragment (v2f i) : SV_TARGET0
{
    surface s;
    float4 c = float4(0,0,0,1);
    UNITY_SETUP_INSTANCE_ID(i);
    ClipLOD(i.positionCS.xy, unity_LODFade.x);
    s.position = i.positionWS;
    s.color = GetBase(i.uv.xy);
    s.normal = normalize(i.normal);
    s.viewdir = normalize(_WorldSpaceCameraPos - i.positionWS);
    s.metallic = GetMetallic(i.uv);
    s.smoothness = GetSmoothness(i.uv);
    s.fresnelStrength = GetFresnel(i.uv);
    s.depth = -TransformWorldToView(i.positionWS).z;
    s.dither = InterleavedGradientNoise(i.positionCS.xy, 0);
    BRDF brdf = GetBRDF(s);
    GI gi = GetGI(GI_FRAGMENT_DATA(i), s, brdf);
    c.rgb = GetLighting(s, brdf, gi) + GetEmission(i.uv.xy);
    return c;
}


#endif
