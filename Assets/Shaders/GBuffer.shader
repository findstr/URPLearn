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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 uv : TEXCOORD0;
                float3 worldPos: TEXCOORD1;
                float3 worldNormal: TEXCOORD2;
            };

            struct pixel {
                half4 GPosition:SV_TARGET0;
                half4 GNormal:SV_TARGET1;
                half4 GDiffuse:SV_TARGET2;
                half4 GDepth:SV_TARGET3;
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
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex).xy;
                o.uv.z = o.vertex.w;
                o.worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                
                return o;
            }

            pixel frag (v2f i) 
            {
                pixel p;
                p.GPosition = half4(i.worldPos, 1.0);
                p.GNormal = half4(i.worldNormal, 0.0);
                p.GDiffuse = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
                p.GDepth = i.uv.z;
                return p;
            }
            ENDHLSL
        }
    }
}
