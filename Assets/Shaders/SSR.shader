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
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

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
            float4x4 _MatrixVP;
            float3 _CameraPos;
            float4 _MainTex_ST;
            float4 _ProjectionArgs;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            TEXTURE2D(_GPosition);
            TEXTURE2D(_GNormal);
            TEXTURE2D(_GDiffuse);
            TEXTURE2D(_GDepth);
            SAMPLER(sampler_GDiffuse);

	        half2 GetScreenUV(float3 pos) 
		    {
                float4 uv = mul(_MatrixVP, float4(pos, 1.0));
                //uv.xy = uv.xy * 0.5 + 0.5;
                return uv.xy;
		    } 
		    float3 GetGBufferPos(half2 uv)
			{
                return SAMPLE_TEXTURE2D(_GPosition, sampler_GDiffuse, uv).xyz;
			}
		    float GetGBufferDepth(half2 uv)
		    {
                float d = SAMPLE_TEXTURE2D(_GDepth, sampler_GDiffuse, uv).x;
		        return d;
		    }  
		    half3 GetGBufferNormal(half2 uv) 
            {
                return SAMPLE_TEXTURE2D(_GNormal, sampler_GDiffuse, uv).xyz;
		    }
            half3 GetGBufferDiffuse(half2 uv)
            {
                return SAMPLE_TEXTURE2D(_GDiffuse, sampler_GDiffuse, uv).xyz;
	        }
		    float GetViewDepth(float3 pos) 
		    {
                return mul(_MatrixVP, float4(pos, 1.0)).w;
		    }

		    bool raymarch(float3 ori, float3 dir, out half2 hit, out float x) 
		    {
                for (float i = 1; i < 10.0; i += 0.1) {
                    float3 pos = ori + dir * i;
                    half2 uv = GetScreenUV(pos);
                    if (GetViewDepth(pos) > (GetGBufferDepth(uv) + 0.001)) {
                        hit = uv;
                        x = GetViewDepth(pos);//GetGBufferDepth(uv);
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

            half4 frag (v2f i) : SV_Target
            {
                float debug = 0.0;
                float2 hit = float2(0,0);
                half2 uv = GetScreenUV(i.worldPos.xyz);

                float2 Guv = SAMPLE_TEXTURE2D(_GDepth, sampler_GDiffuse, i.uv.xy).xy;

                float3 worldPos = GetGBufferPos(uv);
                float depth = GetGBufferDepth(uv);
                //for (float i = 0.1; i < 10.0; i += 0.1) {
                float2 xy = GetScreenUV(float3(worldPos + half3(0,1,0) * 0.5));
                debug = GetViewDepth(worldPos + half3(0,1,0) * 0.5) / 100.0;
                depth = GetGBufferDepth(uv) / 100.0;

                /*
                half3 N = GetGBufferNormal(uv);
                float3 viewDir = normalize(_CameraPos.xyz - worldPos.xyz);
                half3 reflDir = reflect(-viewDir, N);
                half3 L = GetGBufferDiffuse(uv);
                */
                half3 Lindir = half3(0.0, 0.0, 0.0);
                /*
                if (raymarch(worldPos, half3(0,1,0), hit, debug))
                    Lindir = float3(debug, 0,0); 
		        */
                    //Lindir = float3(depth, 0,0); 
		        if (debug > (depth + 0.001))
                        Lindir = half3(1,0,0);
                else
                        Lindir = half3(0,0,0);
		        half3 L = abs(Lindir);
            /*
                float4 bump = SAMPLE_TEXTURE2D(_NormaMap, sampler_MainTex, i.uv);
                float3 normal = UnpackNormal(bump);

                normal = normalize(normal.x * normalize(i.TW1.xyz) + normal.y * normalize(i.TW2.xyz) + normal.z * normalize(i.TW3.xyz));

                Light l = GetMainLight();
                float3 ldir = l.direction;

                half4 col = SAMPLE_TEXTURE2D(_GDiffuse, sampler_MainTex, i.uv);
                float3 vdir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                float3 hdir = normalize(ldir + vdir);

                float power = saturate(dot(ldir, normal));
                half3 diff = col.rgb * l.color.rgb * power;

                half3 spec = pow(saturate(dot(hdir, normal)), 128) * 0.1;

                return half4(diff + spec + unity_AmbientSky.rgb * col.rgb, col.a);
                return SAMPLE_TEXTURE2D(_GDiffuse, sampler_GDiffuse, i.uv);
                */
                //N = TransformWorldToViewDir(N, true);
                //return half4(i.uv.x, 0, 0, 1.0);
                return half4(GetGBufferDiffuse(Guv), 1.0);
            }
            ENDHLSL
        }
    }
}
