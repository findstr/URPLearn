using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraRender {
	private ShadowPass shadows = new ShadowPass();
	private Lighting lighting = new Lighting();
	private RenderContext render_ctx = new RenderContext();
	private ShaderTagId shaderGBuffer = new ShaderTagId("GBuffer");

	public CameraRender(ref ShadowSetting ss)
	{ 
		render_ctx.shadow_setting = ss;
	}

	private void setup(ScriptableRenderContext src, Camera cam)
    {
		render_ctx.ctx = src;
		render_ctx.camera = cam;
		if (render_ctx.camera.TryGetCullingParameters(out var p)) {
			p.shadowDistance = Mathf.Min(render_ctx.shadow_setting.maxDistance, cam.farClipPlane);
			render_ctx.cull_result = render_ctx.ctx.Cull(ref p);
		}
    }

	private void cleanup()
    {
		render_ctx.ctx.Submit();
    }

	private void clear_screen()
    {
		render_ctx.ctx.SetupCameraProperties(render_ctx.camera);
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
		var drawingSetting = new DrawingSettings(shaderGBuffer, sortingSettings) {
			enableDynamicBatching = true,
			enableInstancing = true,
			perObjectData = PerObjectData.Lightmaps | 
							PerObjectData.LightProbe | 
							PerObjectData.LightProbeProxyVolume | 
							PerObjectData.OcclusionProbe | 
							PerObjectData.ShadowMask |
							PerObjectData.ReflectionProbes

		};
		lighting.setup(render_ctx);
		render_ctx.ctx.DrawRenderers(render_ctx.cull_result, ref drawingSetting, ref filterSetting);

		if (render_ctx.camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
			render_ctx.ctx.DrawSkybox(render_ctx.camera);

#if UNITY_EDITOR
		if (Handles.ShouldRenderGizmos()) {
			render_ctx.ctx.DrawGizmos(render_ctx.camera, GizmoSubset.PreImageEffects);
			render_ctx.ctx.DrawGizmos(render_ctx.camera, GizmoSubset.PostImageEffects);
        }	
#endif
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
