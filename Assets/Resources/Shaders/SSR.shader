Shader "LearnURP/SSR"
{
    Properties
    {
        _CameraPos("CameraPos", Vector) = (0,0,0,0)
        _MainTex ("Texture", 2D) = "white" {}
        _GPosition("GPosition", 2D) = "white" {}
        _GNormal("GNormal", 2D) = "white" {}
        _GTangent("GTangent", 2D) = "white" {}
        _GDiffuse("GDiffuse", 2D) = "white" {}
        _GDepth("GDepth", 2D) = "white" {}
    }
    SubShader
    {
        Tags {"RenderPipeline" = "UniversalRenderPipeline"  "RenderType"="Opaque"}
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

            #define M_PI 3.1415926535897932384626433832795

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal: NORMAL; 
                float4 tangent: TANGENT;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos: TEXCOORD1;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4x4 _MATRIX_VP;
            float3 _CameraPos;
            float4 _MainTex_ST;
            float4 _ProjectionArgs;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            TEXTURE2D(_GPosition);
            TEXTURE2D(_GNormal);
            TEXTURE2D(_GDiffuse);
            TEXTURE2D(_GDepth);
            SAMPLER(sampler_GPosition);
            SAMPLER(sampler_GDepth);

	        half2 GetScreenUV(float3 pos) 
		    {
                float4 uv = mul(_MATRIX_VP, float4(pos, 1.0));
                uv.xy /= uv.w;
                uv.xy = uv.xy * 0.5 + 0.5;
                uv.y = 1.0 - uv.y;
                return uv.xy;
		    } 
		    float3 GetGBufferPos(half2 uv)
			{
                return SAMPLE_TEXTURE2D(_GPosition, sampler_GPosition, uv).xyz;
			}
		    float GetGBufferDepth(half2 uv)
		    {
                if (uv.x < 0 || uv.x > 1.0 || uv.y < 0 || uv.y > 1.0)
                    return 10000.0;
                float d = SAMPLE_TEXTURE2D(_GDepth, sampler_GDepth, uv).x;
                if (d < 0.01)
                    d = 10000.0;
		        return d;
		    }  
		    half3 GetGBufferNormal(half2 uv) 
            {
                return SAMPLE_TEXTURE2D(_GNormal, sampler_GPosition, uv).xyz;
		    }
            half3 GetGBufferDiffuse(half2 uv)
            {
                return SAMPLE_TEXTURE2D(_GDiffuse, sampler_GPosition, uv).xyz;
	        }
		    float GetViewDepth(float3 pos) 
		    {
                return mul(_MATRIX_VP, float4(pos, 1.0)).w;
		    }

		    bool raymarch(float3 ori, float3 dir, out half2 hit)
		    {
                for (float i = 0.1; i < 5.0; i += 0.1) {
                    float3 pos = ori + dir * i;
                    half2 uv = GetScreenUV(pos);
                    if (GetViewDepth(pos) > (GetGBufferDepth(uv) + 0.001)) {
                        hit = uv;
                        return true;
		            }
		        }
                return false;
		    }       

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = TransformObjectToWorld(v.vertex.xyz);
                return o;
            }

            half3 EvalDiffuse(float3 wi, half2 uv) {
                half3 albedo = GetGBufferDiffuse(uv);
                half3 N = GetGBufferNormal(uv);
                half3 diffuse = max(dot(N, wi), 0) * albedo / M_PI;
                return diffuse;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 hit = float2(0,0);
                float3 worldPos = GetGBufferPos(uv);
                half3 N = GetGBufferNormal(uv);
                Light l = GetMainLight();
                float3 ldir = l.direction;
                float3 wi = normalize(l.direction.xyz);
                float3 wo = normalize(_CameraPos.xyz - worldPos.xyz);
                half3 albedo = GetGBufferDiffuse(uv) / (max(dot(N, wi), 0) * l.color.rgb);
                half3 L = GetGBufferDiffuse(uv);
                half3 Lindir = half3(0,0,0);
                half3 reflDir = reflect(-wo, N);
                if (raymarch(worldPos, reflDir, hit)) {
                    float3 hitPos = GetGBufferPos(hit);
                    float3 indir = normalize(hitPos - worldPos);
                    Lindir = albedo * max(dot(N, indir), 0) * GetGBufferDiffuse(hit);
                }
                L = L + Lindir;
                return half4(L, 1.0);
            }
            ENDHLSL
        }
    }
}
