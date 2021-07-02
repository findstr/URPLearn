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
                float3 worldPos: VAR_POSITION;
                float4 vertex : SV_POSITION;
            };

            struct surface {
                float4 color;
                float3 normal;
                float metallic;
                float smoothness;
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
                return o;
            }

            BRDF get_brdf(surface s) 
            {
                BRDF brdf;
                float oneMinusReflectivity = 1.0 - s.metallic;
                brdf.diffuse = s.color * OneMinusReflectivityMetallic(s.metallic);
                brdf.specular = lerp(kDielectricSpec.rgb, s.color, s.metallic);
                brdf.roughness = PerceptualRoughnessToRoughness(PerceptualSmoothnessToPerceptualRoughness(s.smoothness));
                return brdf;
            } 

            light get_direciontal_light(int idx) 
            {
                light l;
                l.color = light_directional_color[idx];
                l.direction = light_directional_direction[idx];
                return l;
            }

            float3 lighting_directional(surface s, BRDF brdf, int i) 
            {
                light l = get_direciontal_light(i);
                float3 diffuse = saturate(dot(s.normal, l.direction)) * l.color;
                //float4 specular = 
                return diffuse;
            }

            half4 frag (v2f i) : SV_TARGET0
            {
                float4 c = float4(0,0,0,1);
                surface s;
                s.color = _Color;
                s.normal = normalize(i.normal);
                s.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
                s.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
                BRDF brdf = get_brdf(s);
                for (int i = 0; i < light_directional_count; i++) {
                    c.rgb += lighting_directional(s, brdf, i);
                }
                return c;
            }
            ENDHLSL
        }
    }
}
