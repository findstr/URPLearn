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
            #pragma enable_d3d11_debug_symbols
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

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

            struct surface {
                float4 color;
                float3 normal;
                float metallic;
                float smoothness;
                float3 viewdir;
            };

            struct light {
                float3 color;
                float3 direction;
            };

            struct BRDF {
                float3 diffuse;
                float3 specular;
                float roughness;
            };
 
            CBUFFER_START(_CustomLight)
                int light_directional_count;
                half4  light_directional_color[MAX_DIRECTIONAL_LIGHT];
                float4 light_directional_direction[MAX_DIRECTIONAL_LIGHT];
            CBUFFER_END

            BRDF get_brdf(surface s) 
            {
                BRDF brdf;
                float oneMinusReflectivity = 1.0 - s.metallic;
                brdf.diffuse = s.color.rgb * OneMinusReflectivityMetallic(s.metallic);
                brdf.specular = lerp(kDielectricSpec.rgb, s.color.rgb, s.metallic);
                brdf.roughness = PerceptualRoughnessToRoughness(PerceptualSmoothnessToPerceptualRoughness(s.smoothness));
                return brdf;
            } 

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
                    float norm = brdf.roughness * 4.0 + 2.0;
                    return r2 / (d2 * max(0.1, lh2)) * norm;
            }

            float3 light_radiance (surface s, light l) 
            {
	            return saturate(dot(s.normal, l.direction)) * l.color;
            }
 
            float3 direct_brdf(surface s, BRDF brdf, light l) 
            {
                return specular_strength(s, brdf, l) * brdf.specular + brdf.diffuse;
            }

            float3 lighting_directional(surface s, BRDF brdf, int i) 
            {
                light l = get_direciontal_light(i);
                return light_radiance(s, l) * direct_brdf(s, brdf, l);
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
                s.color = _Color;
                s.normal = normalize(i.normal);
                s.viewdir = normalize(_WorldSpaceCameraPos - i.positionWS);
                s.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
                s.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
                BRDF brdf = get_brdf(s);
                for (int i = 0; i < light_directional_count; i++) {
                    c.rgb += lighting_directional(s, brdf, i);
                }
                c.rgb = half3(1,0,0);
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
