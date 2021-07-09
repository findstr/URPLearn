Shader "LearnURP/Base"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _NormaMap("NormalMap", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags {"LightMode" = "GBuffer" "RenderPipeline" = "UniversalRenderPipeline"  "RenderType"="Opaque"}
        LOD 100

        Pass
        {
            HLSLPROGRAM
            //#pragma enable_d3d11_debug_symbols

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "surface.hlsl"
            #include "Lighting.hlsl"
            #include "Shadows.hlsl"

            #define M_PI 3.1415926535897932384626433832795
            #define MAX_DIRECTIONAL_LIGHT 4

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal: NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normal: VAR_NORMAL;
                float3 positionWS: VAR_POSITION;
                float4 vertex : SV_POSITION;
            };

            BRDF get_brdf(surface s) 
            {
                BRDF brdf;
                float oneMinusReflectivity = 1.0 - s.metallic;
                brdf.diffuse = s.color.rgb * OneMinusReflectivityMetallic(s.metallic);
                brdf.specular = lerp(kDielectricSpec.rgb, s.color.rgb, s.metallic);
                brdf.roughness = PerceptualRoughnessToRoughness(PerceptualSmoothnessToPerceptualRoughness(s.smoothness));
                return brdf;
            } 

            CBUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST);
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color);
                UNITY_DEFINE_INSTANCED_PROP(float, _Metallic);
                UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness);
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex).xy;
                o.normal = v.normal;
                o.positionWS = TransformObjectToWorld(v.vertex.xyz);
                return o;
            }
            half4 frag (v2f i) : SV_TARGET0
            {
                float4 c = float4(0,0,0,1);
                surface s;
                s.position = i.positionWS;
                s.color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
                s.normal = normalize(i.normal);
                s.viewdir = normalize(_WorldSpaceCameraPos - i.positionWS);
                s.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
                s.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
                s.depth = -TransformWorldToView(i.positionWS).z;
                BRDF brdf = get_brdf(s);
                CascadeInfo ci = GetCascadeInfo(s);
                for (int i = 0; i < light_directional_count; i++) {
                    c.rgb += lighting_directional(s, brdf, i, ci);
                }
                return c;
            }
            ENDHLSL
        }
        Pass {
            Tags {
                "LightMode" = "ShadowCaster"
            }
            ColorMask 0 
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma target 3.5
            #pragma shader_feature _ALPHATEST_ON
            #pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            #include "ShadowCaster.hlsl"
            ENDHLSL
        }
    }
}
