using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ShadowPass
{
    struct ShadowLight
    {
        public int lightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    };

	private const int max_shadow_count = Config.MAX_DIRECTIONAL_LIGHT_COUNT;
    private const int max_cascade_count = Config.MAX_CASCADE_COUNT;
    private static int shader_prop_shadow_distance = Shader.PropertyToID("_ShadowDistance");
    private static int shader_prop_shadow_atlas_size = Shader.PropertyToID("_ShadowAtlasSize");
    private static int shader_prop_shadow_atlas = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int shader_prop_shadow_matrices = Shader.PropertyToID("_DirectionalShadowMatrices");
    private static int shader_prop_shadow_data = Shader.PropertyToID("_DirectionalLightShadowData");
    private static int shader_prop_othershadow_data = Shader.PropertyToID("_OtherLightShadowData");
    private static int shader_prop_cascade_count = Shader.PropertyToID("_CascadeCount");
    private static int shader_prop_cascade_sphere = Shader.PropertyToID("_CascadeSphere");
    private static int shader_prop_cascade_data = Shader.PropertyToID("_CascadeData");

    static string[] shadowFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };
    static string[] cascadeBlendKeywords = {
		"_CASCADE_BLEND_SOFT",
		"_CASCADE_BLEND_DITHER"
	};

    static string[] shadowMaskKeywords = {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE",
    };

    private int directShadowCount = 0;
    private int otherShadowCount = 0;
    private bool useShadowMask;
    private ShadowLight[] shadow_lights = new ShadowLight[max_shadow_count];
    private Matrix4x4[] shadow_matrices = new Matrix4x4[max_shadow_count * max_cascade_count];
    private Vector4[] directShadowData = new Vector4[max_shadow_count];
    private Vector4[] cascade_sphere = new Vector4[max_cascade_count];
    private Vector4[] cascade_data = new Vector4[max_cascade_count];
    private Vector4[] otherLightShadowData = new Vector4[Config.MAX_OTHER_LIGHT_COUNT];

    private void SetKeywords(CommandBuffer cmd, string[] keywords, int enable)
    {
        for (int i = 0; i < keywords.Length; i++) {
            if (i == enable)
                cmd.EnableShaderKeyword(keywords[i]);
            else
                cmd.DisableShaderKeyword(keywords[i]);
        }
    }

    void SetupDirectionalLight(RenderContext ctx, int idx, Light light, int i)
    {
        var cull = ctx.cull_result;
             if (light.shadows != LightShadows.None && light.shadowStrength > 0f && cull.GetShadowCasterBounds(i, out Bounds b)) {
                    float maskChannel = -1;
                    LightBakingOutput lightBaking = light.bakingOutput;
                    shadow_lights[directShadowCount] = new ShadowLight {
                        lightIndex = i, 
                        slopeScaleBias = light.shadowBias, 
                        nearPlaneOffset = light.shadowNearPlane
                    };
                    bool use = (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask);
                    if (use == true) {
                        useShadowMask = true;
                        maskChannel = lightBaking.occlusionMaskChannel;
                    }
                    directShadowData[idx] = new Vector4(light.shadowStrength, 
                        directShadowCount * ctx.shadow_setting.directional.cascadeCount, 
                        light.shadowNormalBias, maskChannel);
                    directShadowCount++;
                } else {
                    directShadowData[idx] = new Vector4(0, -1, -1);
                }
    }

    void SetupOtherLight(int idx, Light light)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
            return ;
        var bo = light.bakingOutput;
        if (bo.lightmapBakeType == LightmapBakeType.Mixed && bo.mixedLightingMode == MixedLightingMode.Shadowmask) {
            useShadowMask = true;
            otherShadowCount++;
            otherLightShadowData[idx] = new Vector4(light.shadowStrength, 0f, 0f, bo.occlusionMaskChannel);
        } else {
            otherLightShadowData[idx] = new Vector4(0f, 0f, 0f, -1f);
        }
    }

    private bool Prepare(RenderContext ctx)
    {
		var lights = ctx.cull_result.visibleLights;
        int directIdx = 0, otherIdx = 0;
		directShadowCount = 0;
        otherShadowCount = 0;
        useShadowMask = false;
		for (int i = 0; i < lights.Length && directShadowCount < max_shadow_count; i++) {
			VisibleLight vl = lights[i];
            Light light = vl.light;
			switch (vl.lightType) {
			case LightType.Directional:
                if (directIdx < Config.MAX_DIRECTIONAL_LIGHT_COUNT) 
                    SetupDirectionalLight(ctx, ++directIdx, light, i);
                break;
            case LightType.Point:
            case LightType.Spot:
                if (otherIdx < Config.MAX_OTHER_LIGHT_COUNT)
                    SetupOtherLight(otherIdx++, light);
                break;
            default:
                break;
            }
        }
        return (directShadowCount + otherShadowCount) > 0;
    }

    private void init_buffer(RenderContext ctx)
    {
        int atlas_size = (int)ctx.shadow_setting.directional.atlasSize;
        var cb = ctx.command_begin("ShadowBuffer");
        cb.GetTemporaryRT(shader_prop_shadow_atlas, atlas_size, atlas_size, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        cb.SetRenderTarget(shader_prop_shadow_atlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cb.ClearRenderTarget(true, false, Color.clear);
        ctx.command_end();
    }
    private void cleanup(RenderContext ctx)
    {
        var cb = ctx.command_begin("ShadowClean");
        cb.ReleaseTemporaryRT(shader_prop_shadow_atlas);
        ctx.command_end();
    }

    private Vector2 shadowmap_tile(int split_count, int idx)
    {
        return new Vector2(idx % split_count, idx / split_count);
    }

    private Matrix4x4 shadowmap_matrix(Matrix4x4 m, Vector2 offset, int split_count)
    {
        if (SystemInfo.usesReversedZBuffer) {
			m.m20 = -m.m20;
			m.m21 = -m.m21;
			m.m22 = -m.m22;
			m.m23 = -m.m23;
		}
        float scale = 1.0f / split_count;
        m.m00 = (0.5f * m.m00 + (0.5f + offset.x) * m.m30) * scale;
		m.m01 = (0.5f * m.m01 + (0.5f + offset.x) * m.m31) * scale;
		m.m02 = (0.5f * m.m02 + (0.5f + offset.x) * m.m32) * scale;
		m.m03 = (0.5f * m.m03 + (0.5f + offset.x) * m.m33) * scale;

		m.m10 = (0.5f * m.m10 + (0.5f + offset.y) * m.m30) * scale;
		m.m11 = (0.5f * m.m11 + (0.5f + offset.y) * m.m31) * scale;
		m.m12 = (0.5f * m.m12 + (0.5f + offset.y) * m.m32) * scale;
		m.m13 = (0.5f * m.m13 + (0.5f + offset.y) * m.m33) * scale;

		m.m20 = 0.5f * m.m20 + (0.5f * m.m30);
		m.m21 = 0.5f * m.m21 + (0.5f * m.m31);
		m.m22 = 0.5f * m.m22 + (0.5f * m.m32);
		m.m23 = 0.5f * m.m23 + (0.5f * m.m33);

        return m;
    }

    private void set_cascade_data(RenderContext ctx, int i, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)ctx.shadow_setting.directional.filter + 1f);
        cullingSphere.w *= cullingSphere.w;
        cascade_sphere[i] = cullingSphere;
        cascade_data[i] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }

    private void draw_shadow(RenderContext ctx)
    {
        var cull = ctx.cull_result;
        var setting = ctx.shadow_setting;
        int cascade_count = setting.directional.cascadeCount;
        int tile_count = directShadowCount * cascade_count;
        int split_count = tile_count <= 1 ? 1 : (tile_count <= 4 ? 2 : 4);
        int atlasSize = (int)setting.directional.atlasSize;
        int tile_size = atlasSize / split_count;
        for (int i = 0; i < directShadowCount; i++) {
            int tile_start = i * cascade_count;
            var light_index = shadow_lights[i].lightIndex;
            var slopeBias = shadow_lights[i].slopeScaleBias;
            float cullingFactor = Mathf.Max(0f, 0.8f - setting.directional.cascadeFade);
            for (int j = 0; j < cascade_count; j++) { 
                var view_offset = shadowmap_tile(split_count, tile_start + j);
                var view_port = view_offset * tile_size;
                cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    light_index, j, cascade_count, ctx.shadow_setting.directional.cascadeRatios, tile_size, 
                    shadow_lights[i].nearPlaneOffset, out Matrix4x4 V, out Matrix4x4 P, out ShadowSplitData split_data);
                split_data.shadowCascadeBlendCullingFactor = cullingFactor;
                var sds = new ShadowDrawingSettings(cull, light_index) {
                    splitData = split_data 
                };
                if (i == 0) { 
                    set_cascade_data(ctx, j, split_data.cullingSphere, tile_size);
                }
                var cb = ctx.command_begin("ShadowDraw");
                cb.SetViewport(new Rect(view_port.x, view_port.y, tile_size, tile_size));
                shadow_matrices[tile_start + j] = shadowmap_matrix(P * V, view_offset, split_count);
                cb.SetViewProjectionMatrices(V, P);
                cb.SetGlobalDepthBias(0f, slopeBias);
                ctx.command_end();
                ctx.ctx.DrawShadows(ref sds);
                cb = ctx.command_begin("ShadowDraw");
                cb.SetGlobalDepthBias(0f, 0f);
                ctx.command_end();
            }
        }
        var cmd = ctx.command_begin("ShadowVariable");
        float f = 1 - setting.directional.cascadeFade;
        cmd.SetGlobalVector(shader_prop_shadow_distance, 
            new Vector4(setting.maxDistance, 1f/setting.maxDistance, 1f/setting.distanceFade, 1 / (1 - f * f)));
        cmd.SetGlobalMatrixArray(shader_prop_shadow_matrices, shadow_matrices);
        cmd.SetGlobalVectorArray(shader_prop_shadow_data, directShadowData);
        cmd.SetGlobalVector(shader_prop_shadow_atlas_size, new Vector4(atlasSize, 1f / atlasSize));
        cmd.SetGlobalInt(shader_prop_cascade_count, cascade_count);
        cmd.SetGlobalVectorArray(shader_prop_cascade_sphere, cascade_sphere);
        cmd.SetGlobalVectorArray(shader_prop_cascade_data, cascade_data);
        cmd.SetGlobalVectorArray(shader_prop_othershadow_data, otherLightShadowData);
        SetKeywords(cmd, shadowFilterKeywords, (int)setting.directional.filter - 1);
        SetKeywords(cmd, cascadeBlendKeywords, (int)setting.directional.cascadeBlend - 1);
        SetKeywords(cmd, shadowMaskKeywords, useShadowMask ? (QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1) : -1);
        ctx.command_end();
    }

    public void draw(RenderContext ctx)
    {
        if(!Prepare(ctx)) { 
            var xcb = ctx.command_begin("Shadow");
            xcb.GetTemporaryRT(shader_prop_shadow_atlas, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            xcb.ReleaseTemporaryRT(shader_prop_shadow_atlas);
            ctx.command_end();
            return ;
        }
        init_buffer(ctx);
        draw_shadow(ctx);
        cleanup(ctx);
        return ;
    }
}
