Shader "LearnURP/Blinphong"
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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.TW1 = float4(worldTangent.xyz, worldPos.x);
                o.TW2 = float4(worldNormal.xyz, worldPos.z);
                o.TW3 = float4(worldBinormal.xyz, worldPos.y);

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
