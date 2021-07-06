using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    struct shadow_light
    {
        public int light_index;
    };

	private const int max_shadow_count = 1;
    private static int shader_prop_shadow_atlas = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int shader_prop_shadow_data = Shader.PropertyToID("_DirectionalLightShadowData");
    private int shadow_count = 0;
    private shadow_light[] shadow_lights = new shadow_light[max_shadow_count];

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
                    shadow_lights[shadow_count++] = new shadow_light {
                        light_index = i,
                    };
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

    private void draw_shadow(RenderContext ctx)
    {
        var cull = ctx.cull_result;
        int tile_size = (int)ctx.shadow_setting.directional.atlasSize;
        for (int i = 0; i < shadow_count; i++) {
            var light_index = shadow_lights[i].light_index;
            cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(light_index, 0, 1, Vector3.zero, tile_size, 0, 
                out Matrix4x4 V, out Matrix4x4 P, out ShadowSplitData split);
            var setting = new ShadowDrawingSettings(cull, light_index) {
                splitData = split
            };
            var cb = ctx.command_begin("ShadowDraw");
            cb.SetViewProjectionMatrices(V, P);
            ctx.command_end();
            ctx.ctx.DrawShadows(ref setting);
        }
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
