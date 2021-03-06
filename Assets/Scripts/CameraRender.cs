using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraRender {
	private ShadowPass shadows = new ShadowPass();
	private Lighting lighting = new Lighting();
	private RenderData render_ctx = new RenderData();
	private ShaderTagId shaderGBuffer = new ShaderTagId("GBuffer");

	public CameraRender(ref ShadowSetting ss)
	{ 
		render_ctx.shadowSettings = ss;
	}

	private void setup(ScriptableRenderContext src, Camera cam)
    {
		render_ctx.ctx = src;
		render_ctx.camera = cam;
		if (render_ctx.camera.TryGetCullingParameters(out var p)) {
			p.shadowDistance = Mathf.Min(render_ctx.shadowSettings.maxDistance, cam.farClipPlane);
			render_ctx.cullResults = render_ctx.ctx.Cull(ref p);
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
		shadows.Render(render_ctx);
    }

	private void draw_geometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
    {
		PerObjectData lightsPerObjectFlags = useLightsPerObject ? (PerObjectData.LightData | PerObjectData.LightIndices) : PerObjectData.None;
		var sortingSettings = new SortingSettings(render_ctx.camera);
		var filterSetting = FilteringSettings.defaultValue;
		var drawingSetting = new DrawingSettings(shaderGBuffer, sortingSettings) {
			enableDynamicBatching = useDynamicBatching,
			enableInstancing = useGPUInstancing,
			perObjectData = PerObjectData.Lightmaps | 
							PerObjectData.LightProbe | 
							PerObjectData.LightProbeProxyVolume | 
							PerObjectData.OcclusionProbe | 
							PerObjectData.ShadowMask |
							PerObjectData.ReflectionProbes |
							lightsPerObjectFlags

		};
		lighting.setup(render_ctx, useLightsPerObject);
		render_ctx.ctx.DrawRenderers(render_ctx.cullResults, ref drawingSetting, ref filterSetting);

		if (render_ctx.camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
			render_ctx.ctx.DrawSkybox(render_ctx.camera);

#if UNITY_EDITOR
		if (Handles.ShouldRenderGizmos()) {
			render_ctx.ctx.DrawGizmos(render_ctx.camera, GizmoSubset.PreImageEffects);
			render_ctx.ctx.DrawGizmos(render_ctx.camera, GizmoSubset.PostImageEffects);
        }	
#endif
    }

    public void Render(ScriptableRenderContext src, Camera cam, bool useDynamicBatch, bool useGPUInstancing, bool useLightPerObject)
    {
		setup(src, cam);
		draw_shadow();
		clear_screen();
		draw_geometry(useDynamicBatch, useGPUInstancing, useLightPerObject);
		cleanup();
    }
}
