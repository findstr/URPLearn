using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ShadowPass
{
    struct shadow_light
    {
        public int light_index;
    };

	private const int max_shadow_count = Config.MAX_VISIBLE_LIGHT_COUNT;
    private static int shader_prop_shadow_atlas = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int shader_prop_shadow_matrices = Shader.PropertyToID("_DirectionalShadowMatrices");
    private static int shader_prop_shadow_data = Shader.PropertyToID("_DirectionalLightShadowData");

    private int shadow_count = 0;
    private shadow_light[] shadow_lights = new shadow_light[max_shadow_count];
    private Matrix4x4[] shadow_matrices = new Matrix4x4[max_shadow_count];
    private Vector4[] shadow_data = new Vector4[max_shadow_count];

    private bool prepare(RenderContext ctx)
    {
        var cull = ctx.cull_result;
		var lights = cull.visibleLights;
		shadow_count = 0;
		for (int i = 0; i < lights.Length && shadow_count < max_shadow_count; i++) {
			VisibleLight vl = lights[i];
            Light light = vl.light;
			switch (vl.lightType) {
			case LightType.Directional:
                if (light.shadows != LightShadows.None && light.shadowStrength > 0f && cull.GetShadowCasterBounds(i, out Bounds b)) {
                    shadow_lights[shadow_count] = new shadow_light {light_index = i};
                    shadow_data[i] = new Vector4(light.shadowStrength, shadow_count);
                    shadow_count++;
                } else {
                    shadow_data[i] = new Vector4(0, -1);
                }
                break;
            default:
                break;
            }
        }
        return shadow_count > 0;
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

    private void draw_shadow(RenderContext ctx)
    {
        var cull = ctx.cull_result;
        int split_count = shadow_count <= 1 ? 1 : 2;
        int tile_size = (int)ctx.shadow_setting.directional.atlasSize / split_count;
        for (int i = 0; i < shadow_count; i++) {
            var light_index = shadow_lights[i].light_index;
            var view_offset = shadowmap_tile(split_count, i);
            var view_port = view_offset * tile_size;
            cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light_index, 0, 1, Vector3.zero, tile_size, 0, 
                out Matrix4x4 V, out Matrix4x4 P, out ShadowSplitData split);
            var setting = new ShadowDrawingSettings(cull, light_index) {
                splitData = split
            };
            var cb = ctx.command_begin("ShadowDraw");
            cb.SetViewport(new Rect(view_port.x, view_port.y, tile_size, tile_size));
            shadow_matrices[i] = shadowmap_matrix(P * V, view_offset, split_count);
            cb.SetViewProjectionMatrices(V, P);
            ctx.command_end();
            ctx.ctx.DrawShadows(ref setting);
        }
        var cmd = ctx.command_begin("ShadowVariable");
        cmd.SetGlobalMatrixArray(shader_prop_shadow_matrices, shadow_matrices);
        cmd.SetGlobalVectorArray(shader_prop_shadow_data, shadow_data);
        ctx.command_end();
    }

    public void draw(RenderContext ctx)
    {
        if(!prepare(ctx)) { 
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
