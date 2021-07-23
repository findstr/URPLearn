Shader "LearnURP/Base"
{
    Properties
    {
        _BaseMap("Albedo", 2D) = "white" {}
        _EmissionMap("Emission", 2D) = "white" {}
        _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR] _EmissionColor("Emission", Color) = (1.0, 1.0, 1.0, 1.0)
        _NormaMap("NormalMap", 2D) = "white" {}
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Fresnel("Fresnel", Range(0,1)) = 1
    }
    SubShader
    {
        HLSLINCLUDE
        #include "LitInput.hlsl"
        ENDHLSL

        LOD 100
        Pass
        {
            Tags {"LightMode" = "GBuffer" "RenderPipeline" = "UniversalRenderPipeline"  "RenderType"="Opaque"}
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols

            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _LIGHTS_PER_OBJECT
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE 
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "LitPass.hlsl"
        
            ENDHLSL
        }
        Pass {
            Tags {
                "LightMode" = "ShadowCaster"
            }
            ColorMask 0 
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _ALPHATEST_ON
            #pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            #include "ShadowCaster.hlsl"
            ENDHLSL
        }
        Pass {
            Tags {
                "LightMode" = "Meta"
            }
            Cull off
            
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma target 3.5
            #pragma vertex      MetaPassVertex
            #pragma fragment    MetaPassFragment

            #include "MetaPass.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}
