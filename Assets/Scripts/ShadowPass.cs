using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ShadowPass
{
    struct ShadowDirecionalLight {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    };
    struct ShadowOtherLight {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public float nearPlaneOffset;
        public bool isPoint;
    };

	private const int maxDirectionalShadow = Config.MAX_DIRECTIONAL_LIGHT_COUNT;
    private const int maxOtherShadow = Config.MAX_OTHER_LIGHT_COUNT;
    private const int maxCascadeCount = Config.MAX_CASCADE_COUNT;
    private static int _ShadowDistance = Shader.PropertyToID("_ShadowDistance");
    private static int _ShadowAtlasSize = Shader.PropertyToID("_ShadowAtlasSize");
    private static int _DirectionalShadowAtlas = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int _DirectionalShadowMatrices = Shader.PropertyToID("_DirectionalShadowMatrices");
    private static int _DirectionalShadowData = Shader.PropertyToID("_DirectionalLightShadowData");
    private static int _CascadeCount = Shader.PropertyToID("_CascadeCount");
    private static int _CascadeSphere = Shader.PropertyToID("_CascadeSphere");
    private static int _CascadeData = Shader.PropertyToID("_CascadeData");
    private static int _OtherShadowData = Shader.PropertyToID("_OtherShadowData");
    private static int _OtherShadowAtlas = Shader.PropertyToID("_OtherShadowAtlas");
    private static int _OtherShadowMatrices = Shader.PropertyToID("_OtherShadowMatrices");
    private static int _OtherShadowTiles = Shader.PropertyToID("_OtherShadowTiles");
    private static int _ShadowPancaking = Shader.PropertyToID("_ShadowPancaking");

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
    private ShadowDirecionalLight[] directionalShadowLight = new ShadowDirecionalLight[maxDirectionalShadow];
    private ShadowOtherLight[] otherShadowLight = new ShadowOtherLight[maxOtherShadow];
    private Matrix4x4[] directionalShadowMatrices = new Matrix4x4[maxDirectionalShadow * maxCascadeCount];
    private Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxOtherShadow];
    private Vector4[] directionalShadowData = new Vector4[maxDirectionalShadow];
    private Vector4[] cascadeSphere = new Vector4[maxCascadeCount];
    private Vector4[] cascadeData = new Vector4[maxCascadeCount];
    private Vector4[] otherShadowData = new Vector4[Config.MAX_OTHER_LIGHT_COUNT];
    private Vector4[] otherShadowTiles = new Vector4[Config.MAX_OTHER_LIGHT_COUNT];

    private void SetKeywords(CommandBuffer cmd, string[] keywords, int enable)
    {
        for (int i = 0; i < keywords.Length; i++) {
            if (i == enable)
                cmd.EnableShaderKeyword(keywords[i]);
            else
                cmd.DisableShaderKeyword(keywords[i]);
        }
    }

    void SetupDirectionalLight(RenderData ctx, int idx, Light light, int visibleLightIdx)
    {
        var cull = ctx.cullResults;
        if (light.shadows != LightShadows.None && light.shadowStrength > 0f && cull.GetShadowCasterBounds(visibleLightIdx, out Bounds b)) {
                float maskChannel = -1;
                LightBakingOutput lightBaking = light.bakingOutput;
                directionalShadowLight[directShadowCount] = new ShadowDirecionalLight {
                    visibleLightIndex = visibleLightIdx, 
                    slopeScaleBias = light.shadowBias, 
                    nearPlaneOffset = light.shadowNearPlane
                };
                bool use = (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask);
                if (use == true) {
                    useShadowMask = true;
                    maskChannel = lightBaking.occlusionMaskChannel;
                }
                directionalShadowData[idx] = new Vector4(light.shadowStrength, 
                    directShadowCount * ctx.shadowSettings.directional.cascadeCount, 
                    light.shadowNormalBias, maskChannel);
                directShadowCount++;
        } else {
            directionalShadowData[idx] = new Vector4(0, -1, -1);
        }
    }

    void SetupOtherLight(RenderData ctx, int idx, Light light, int visibleLightIdx)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f) { 
            otherShadowData[idx] = new Vector4(0, 0f, 0f, -1);
            return ;
        }
        var cull = ctx.cullResults;
        var bo = light.bakingOutput;
        bool isPoint = light.type == LightType.Point;
        float maskChannel = -1f;
        if (bo.lightmapBakeType == LightmapBakeType.Mixed && bo.mixedLightingMode == MixedLightingMode.Shadowmask) { 
            useShadowMask = true;
            maskChannel = light.bakingOutput.occlusionMaskChannel;
        }
        int newLightCount = otherShadowCount + (isPoint ? 6 : 1);
        if (newLightCount > maxOtherShadow || !cull.GetShadowCasterBounds(visibleLightIdx, out var b)) { 
            otherShadowData[idx] = new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            return ;
        }
        otherShadowLight[otherShadowCount] = new ShadowOtherLight {
                visibleLightIndex = visibleLightIdx,
                slopeScaleBias = light.shadowBias,
                normalBias = light.shadowNormalBias,
                nearPlaneOffset = light.shadowNearPlane,
                isPoint = isPoint,
        };
        otherShadowData[idx] = new Vector4(light.shadowStrength, otherShadowCount, isPoint ? 1f : 0f, bo.occlusionMaskChannel);
        otherShadowCount = newLightCount;
        return ;
    }

    private bool Prepare(RenderData ctx)
    {
		var lights = ctx.cullResults.visibleLights;
        int directIdx = 0, otherIdx = 0;
		directShadowCount = 0;
        otherShadowCount = 0;
        useShadowMask = false;
		for (int i = 0; i < lights.Length && directShadowCount < maxDirectionalShadow; i++) {
			VisibleLight vl = lights[i];
            Light light = vl.light;
			switch (vl.lightType) {
			case LightType.Directional:
                if (directIdx < Config.MAX_DIRECTIONAL_LIGHT_COUNT) 
                    SetupDirectionalLight(ctx, directIdx++, light, i);
                break;
            case LightType.Point:
            case LightType.Spot:
                if (otherIdx < Config.MAX_OTHER_LIGHT_COUNT)
                    SetupOtherLight(ctx, otherIdx++, light, i);
                break;
            default:
                break;
            }
        }
        return (directShadowCount + otherShadowCount) > 0;
    }

    private void Cleanup(RenderData ctx)
    {
        var cb = ctx.command_begin("ShadowClean");
        cb.ReleaseTemporaryRT(_DirectionalShadowAtlas);
        if (otherShadowCount > 0)
            cb.ReleaseTemporaryRT(_OtherShadowAtlas);
        ctx.command_end();
    }

    private Vector2 TileViewport(int split_count, int idx)
    {
        return new Vector2(idx % split_count, idx / split_count);
    }

    private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split_count)
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

    private void SetCascadeData(RenderData ctx, int i, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)ctx.shadowSettings.directional.filter + 1f);
        cullingSphere.w *= cullingSphere.w;
        cascadeSphere[i] = cullingSphere;
        cascadeData[i] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }

    private void RenderDirectionalShadows(RenderData ctx)
    {
        var setting = ctx.shadowSettings;
        int atlasSize = (int)setting.directional.atlasSize;

        var cb = ctx.command_begin("ShadowBuffer");
        cb.GetTemporaryRT(_DirectionalShadowAtlas, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        cb.SetRenderTarget(_DirectionalShadowAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cb.ClearRenderTarget(true, false, Color.clear);
        cb.SetGlobalFloat(_ShadowPancaking, 1f);
        ctx.command_end();

        var cull = ctx.cullResults;
        int cascade_count = setting.directional.cascadeCount;
        int tile_count = directShadowCount * cascade_count;
        int split_count = tile_count <= 1 ? 1 : (tile_count <= 4 ? 2 : 4);
        int tile_size = atlasSize / split_count;
        for (int i = 0; i < directShadowCount; i++) {
            int tile_start = i * cascade_count;
            var light_index = directionalShadowLight[i].visibleLightIndex;
            var slopeBias = directionalShadowLight[i].slopeScaleBias;
            float cullingFactor = Mathf.Max(0f, 0.8f - setting.directional.cascadeFade);
            for (int j = 0; j < cascade_count; j++) { 
                var view_offset = TileViewport(split_count, tile_start + j);
                var view_port = view_offset * tile_size;
                cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    light_index, j, cascade_count, ctx.shadowSettings.directional.cascadeRatios, tile_size, 
                    directionalShadowLight[i].nearPlaneOffset, out Matrix4x4 V, out Matrix4x4 P, out ShadowSplitData split_data);
                split_data.shadowCascadeBlendCullingFactor = cullingFactor;
                var sds = new ShadowDrawingSettings(cull, light_index) {
                    splitData = split_data 
                };
                if (i == 0) { 
                    SetCascadeData(ctx, j, split_data.cullingSphere, tile_size);
                }
                directionalShadowMatrices[tile_start + j] = ConvertToAtlasMatrix(P * V, view_offset, split_count);
                cb = ctx.command_begin("ShadowDraw");
                cb.SetViewport(new Rect(view_port.x, view_port.y, tile_size, tile_size));
                cb.SetViewProjectionMatrices(V, P);
                cb.SetGlobalDepthBias(0f, slopeBias);
                ctx.command_end();
                ctx.ctx.DrawShadows(ref sds);
                cb = ctx.command_begin("ShadowDraw");
                cb.SetGlobalDepthBias(0f, 0f);
                ctx.command_end();
            }
        } 
    }

    private void SetOtherTileData(RenderData rd, int idx, Vector2 offset, float scale, float bias)
    {
        float border = 1f / (float)rd.shadowSettings.other.atlasSize * 0.5f;
        Vector4 data = Vector4.zero;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[idx] = data;
    }

    private void RenderSpotShadow(RenderData rd, int idx, int splitCount, int tileSize)
    {
        ShadowOtherLight light = otherShadowLight[idx];
        var shadowSettings = new ShadowDrawingSettings(rd.cullResults, light.visibleLightIndex);
        rd.cullResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.visibleLightIndex, 
            out Matrix4x4 V, out Matrix4x4 P, out ShadowSplitData splitData);
        float texelSize = 2f / (tileSize * P.m00);
        float filterSize = texelSize * ((float)rd.shadowSettings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        shadowSettings.splitData = splitData;
        var viewOffset = TileViewport(splitCount, idx);
        var viewPort = viewOffset * tileSize;
        SetOtherTileData(rd, idx, viewOffset, 1f / splitCount, bias);
        otherShadowMatrices[idx] = ConvertToAtlasMatrix(P * V, viewOffset, splitCount);
        var cb = rd.command_begin("RenderSpotShadow");
        cb.SetViewport(new Rect(viewPort.x, viewPort.y, tileSize, tileSize));
        cb.SetViewProjectionMatrices(V, P);
        cb.SetGlobalDepthBias(0f, light.slopeScaleBias);
        rd.command_end();

        rd.ctx.DrawShadows(ref shadowSettings);

        cb = rd.command_begin("RenderSpotShadow");
        cb.SetGlobalDepthBias(0f, 0f);
        rd.command_end();
    }

    private void RenderPointShadow(RenderData rd, int idx, int splitCount, int tileSize)
    {
        ShadowOtherLight light = otherShadowLight[idx];
        var shadowSettings = new ShadowDrawingSettings(rd.cullResults, light.visibleLightIndex);
        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)rd.shadowSettings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / splitCount;
        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        for (int i = 0; i < 6; i++) { 
            rd.cullResults.ComputePointShadowMatricesAndCullingPrimitives(light.visibleLightIndex,
                (CubemapFace)i, fovBias, out var V, out var P, out var splitData);
            V.m11 = -V.m11;
            V.m12 = -V.m12;
            V.m13 = -V.m13;
            shadowSettings.splitData = splitData;
            var tileIndex = idx + i;
            var viewOffset = TileViewport(splitCount, tileIndex);
            var viewPort = viewOffset * tileSize;
            SetOtherTileData(rd, tileIndex, viewOffset, tileScale, bias);
            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(P * V, viewOffset, splitCount);
            var cb = rd.command_begin("RenderSpotShadow");
            cb.SetViewport(new Rect(viewPort.x, viewPort.y, tileSize, tileSize));
            cb.SetViewProjectionMatrices(V, P);
            cb.SetGlobalDepthBias(0f, light.slopeScaleBias);
            rd.command_end();

            rd.ctx.DrawShadows(ref shadowSettings);

            cb = rd.command_begin("RenderSpotShadow");
            cb.SetGlobalDepthBias(0f, 0f);
            rd.command_end();
        }
    }

    private void RenderOtherShadow(RenderData rd)
    {
        int i;
        int atlasSize = (int)rd.shadowSettings.other.atlasSize;
        var cmd = rd.command_begin("RenderOtherShadow");
        cmd.GetTemporaryRT(_OtherShadowAtlas, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        cmd.SetRenderTarget(_OtherShadowAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cmd.ClearRenderTarget(true, false, Color.clear);
        cmd.SetGlobalFloat(_ShadowPancaking, 0f);
        rd.command_end();
 
        int splitCount = otherShadowCount <= 1 ? 1 : (otherShadowCount <= 4 ? 2 : 4);
        int tileSize = atlasSize / splitCount;
        
        i = 0;
        while (i < otherShadowCount) {
            if (otherShadowLight[i].isPoint) { 
                RenderPointShadow(rd, i, splitCount, tileSize);
                i += 6; 
            } else {
                RenderSpotShadow(rd, i, splitCount, tileSize);
                i += 1;
            }
        }
    }

    public void Render(RenderData ctx)
    {
        Prepare(ctx);
        if (directShadowCount > 0) {
            RenderDirectionalShadows(ctx);
        } else {
            var xcb = ctx.command_begin("DirectionalShadowEmpty");
            xcb.GetTemporaryRT(_DirectionalShadowAtlas, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            ctx.command_end();
        }
        if (otherShadowCount > 0) {
            RenderOtherShadow(ctx);       
        } else {
            var xcb = ctx.command_begin("OtherShadowEmpty");
            xcb.SetGlobalTexture(_OtherShadowAtlas, _DirectionalShadowAtlas);
            ctx.command_end();
        }
        if ((directShadowCount + otherShadowCount) > 0) { 
            var cmd = ctx.command_begin("ShadowVariable");
            var setting = ctx.shadowSettings;
            int directAtlasSize = (int)setting.directional.atlasSize;
            int otherAtlasSize = (int)setting.other.atlasSize;
            float f = 1 - setting.directional.cascadeFade;
            if (directShadowCount > 0) { 
                cmd.SetGlobalVector(_ShadowDistance, 
                    new Vector4(setting.maxDistance, 1f/setting.maxDistance, 1f/setting.distanceFade, 1 / (1 - f * f)));
                cmd.SetGlobalMatrixArray(_DirectionalShadowMatrices, directionalShadowMatrices);
                cmd.SetGlobalVectorArray(_DirectionalShadowData, directionalShadowData);
                cmd.SetGlobalVectorArray(_CascadeSphere, cascadeSphere);
                cmd.SetGlobalVectorArray(_CascadeData, cascadeData);
                SetKeywords(cmd, shadowFilterKeywords, (int)setting.directional.filter - 1);
                SetKeywords(cmd, cascadeBlendKeywords, (int)setting.directional.cascadeBlend - 1);
            }
            if (otherShadowCount > 0) {
                cmd.SetGlobalVectorArray(_OtherShadowData, otherShadowData);
                cmd.SetGlobalMatrixArray(_OtherShadowMatrices, otherShadowMatrices);
                cmd.SetGlobalVectorArray(_OtherShadowTiles, otherShadowTiles);
            }
            cmd.SetGlobalInt(_CascadeCount, setting.directional.cascadeCount);
            cmd.SetGlobalVector(_ShadowAtlasSize, new Vector4(directAtlasSize, 1f / directAtlasSize, otherAtlasSize, 1f / otherAtlasSize));
            SetKeywords(cmd, shadowMaskKeywords, useShadowMask ? (QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1) : -1);
            ctx.command_end();
        }
        Cleanup(ctx);
        return ;
    }
}
