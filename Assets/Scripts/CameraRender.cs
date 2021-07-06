using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraRender {
	private Shadows shadows = new Shadows();
	private Lighting lighting = new Lighting();
	private RenderContext render_ctx = new RenderContext();
	private ShaderTagId shaderGBuffer = new ShaderTagId("GBuffer");

	public void SetShadowSetting(ref ShadowSetting ss)
	{ 
		render_ctx.shadow_setting = ss;
	}

	private void setup(ScriptableRenderContext src, Camera cam)
    {
		render_ctx.ctx = src;
		render_ctx.camera = cam;
		render_ctx.camera.TryGetCullingParameters(out var cullingPameters);
		render_ctx.cull_result = render_ctx.ctx.Cull(ref cullingPameters);
		render_ctx.ctx.SetupCameraProperties(render_ctx.camera);
    }

	private void cleanup()
    {
		render_ctx.ctx.Submit();
    }

	private void clear_screen()
    {
		var cmd = render_ctx.command_begin("ClearScreen");
		cmd.ClearRenderTarget(true, true, Color.black);
		render_ctx.command_end();
    }

	private void draw_shadow()
    {
		shadows.draw(render_ctx);
    }

	private void draw_geometry()
    {
		var sortingSettings = new SortingSettings(render_ctx.camera);
		var filterSetting = FilteringSettings.defaultValue;
		var drawingSetting = new DrawingSettings(shaderGBuffer, sortingSettings);
		lighting.setup(render_ctx);
		render_ctx.ctx.DrawRenderers(render_ctx.cull_result, ref drawingSetting, ref filterSetting);

		if (render_ctx.camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
			render_ctx.ctx.DrawSkybox(render_ctx.camera);
		
    }

    public void Render(ScriptableRenderContext src, Camera cam)
    {
		setup(src, cam);
		draw_shadow();
		clear_screen();
		draw_geometry();
		cleanup();
    }
}
