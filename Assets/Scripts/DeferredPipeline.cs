using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class DeferredRenderPipeline : RenderPipeline
{
	private CameraRender camera_render = null;
	public DeferredRenderPipeline(DeferredRenderPipelineAsset asset) {
		GraphicsSettings.lightsUseLinearIntensity = true;
		camera_render = new CameraRender(ref asset.shadows);
	}

	protected override void Render(ScriptableRenderContext context, Camera[] cameras)
	{
		foreach (var cam in cameras)
			camera_render.Render(context, cam);
	}
}
