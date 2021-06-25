Shader "LearnURP/SSR"
{
    Properties
    {
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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 TW1: TEXCOORD1;
                float4 TW2: TEXCOORD2;
                float4 TW3: TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _MainTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            TEXTURE2D(_GPosition);
            TEXTURE2D(_GNormal);
            TEXTURE2D(_GDiffuse);
            TEXTURE2D(_GDepth);
            SAMPLER(sampler_GDiffuse);



            v2f vert (appdata v)
            {
                v2f o;

                float3 worldNormal = TransformObjectToWorldNormal(v.normal);
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                float3 worldTangent = TransformObjectToWorldDir(v.tangent.xyz);
                float3 worldBinormal = normalize(cross(worldNormal, worldTangent) * v.tangent.w);

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.TW1 = float4(worldTangent.xyz, worldPos.x);
                o.TW2 = float4(worldNormal.xyz, worldPos.z);
                o.TW3 = float4(worldBinormal.xyz, worldPos.y);

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
            /*
                float3 worldPos = float3(i.TW1.w, i.TW2.w, i.TW3.w);
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
                */
                return SAMPLE_TEXTURE2D(_GDiffuse, sampler_GDiffuse, i.uv);
            }
            ENDHLSL
        }
    }
}
