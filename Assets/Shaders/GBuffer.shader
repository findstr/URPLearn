Shader "LearnURP/GBuffer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NormaMap("NormalMap", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
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
                float3 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 TW1: TEXCOORD1;
                float4 TW2: TEXCOORD2;
                float4 TW3: TEXCOORD3;
            };

            struct pixel {
                float4 GPosition:SV_TARGET0;
                half4 GNormal:SV_TARGET1;
                half4 GDiffuse:SV_TARGET2;
                float4 GDepth:SV_TARGET3;
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _MainTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            TEXTURE2D(_NormaMap);
            SAMPLER(sampler_MainTex);

            v2f vert (appdata v)
            {
                v2f o;

                float3 worldNormal = TransformObjectToWorldNormal(v.normal);
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                float3 worldTangent = TransformObjectToWorldDir(v.tangent.xyz);
                float3 worldBinormal = normalize(cross(worldNormal, worldTangent) * v.tangent.w);

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex).xy;
                o.uv.z = o.vertex.w;

                o.TW1 = float4(worldTangent.xyz, worldPos.x);
                o.TW2 = float4(worldBinormal.xyz, worldPos.y);
                o.TW3 = float4(worldNormal.xyz, worldPos.z);

                return o;
            }

            pixel frag (v2f i) 
            {
                pixel p;

                float3 worldPos = float3(i.TW1.w, i.TW2.w, i.TW3.w);
                float3 worldNormal = i.TW3.xyz;

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
                float4 bump = SAMPLE_TEXTURE2D(_NormaMap, sampler_MainTex, i.uv.xy);

                float3 normal = UnpackNormal(bump);

                normal = normalize(normal.x * normalize(i.TW1.xyz) + normal.y * normalize(i.TW2.xyz) + normal.z * normalize(i.TW3.xyz));

                Light l = GetMainLight();
                float3 ldir = l.direction;

                half3 albedo = col.rgb * (1 - _Color.a)  + _Color.rgb * _Color.a;
                half3 diffuse = albedo * l.color.rgb * max(dot(ldir, worldNormal), 0.0) / M_PI;

                p.GPosition = float4(worldPos, 1.0);
                p.GNormal = half4(worldNormal, 0.0);
                p.GDiffuse = half4(diffuse, 1);
                p.GDepth = float4(i.uv.z, i.uv.z, i.uv.z, 1);
                return p;
            }
            ENDHLSL
        }
    }
}
