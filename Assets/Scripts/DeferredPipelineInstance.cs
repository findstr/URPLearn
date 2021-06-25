using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DeferredRenderPipelineInstance : RenderPipeline
{
	private RenderTexture CameraTarget;
	private RenderTexture DepthTexture;
	private RenderTexture[] GBufferTextures;
	private RenderBuffer[] GBuffers;
	private int[] GBufferIDs;
	private ShaderTagId shaderGBuffer;
	private ShaderTagId shaderSSR;
	private static int _DepthTexture = Shader.PropertyToID("_DepthTexture");
	private DeferredRenderPipelineAsset asset;
	private void Resize(RenderTexture rt)
	{
		if (rt.width == Screen.width && rt.height == Screen.height)
			return ;
		rt.Release();
		rt.width = Screen.width;
		rt.height= Screen.height;
		rt.format= RenderTextureFormat.ARGBHalf;
		rt.Create();
	}
	public DeferredRenderPipelineInstance(DeferredRenderPipelineAsset asset) {
		this.asset = asset;
		
		CameraTarget = new RenderTexture(Screen.width, Screen.height, 0);
		DepthTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
		DepthTexture.name = "DepthTexture";

		Resize(CameraTarget);
		Resize(asset.GPosition);
		Resize(asset.GNormal);
		Resize(asset.GDiffuse);
		Resize(asset.GDepth);

		GBufferTextures = new RenderTexture[] {
			asset.GPosition,
			asset.GNormal,
			asset.GDiffuse,
			asset.GDepth,
		};
		GBufferTextures[0].name = "_GPosition";
		GBufferTextures[1].name = "_GNormal";
		GBufferTextures[2].name = "_GDiffuse";
		GBufferTextures[3].name = "_GDepth";

		GBuffers = new RenderBuffer[GBufferTextures.Length];
		GBufferIDs = new int[GBufferTextures.Length];
		for (int i = 0; i < GBuffers.Length; i++) {
			GBuffers[i] = GBufferTextures[i].colorBuffer;
			GBufferIDs[i] = Shader.PropertyToID(GBufferTextures[i].name);
		}

		shaderGBuffer = new ShaderTagId("GBuffer");
		shaderSSR = new ShaderTagId("SSR");
	}
	protected override void Render(ScriptableRenderContext context, Camera[] cameras)
	{
		//clear screen
		var cmd = new CommandBuffer();
		cmd.name = "ClearScreen";
		cmd.ClearRenderTarget(true, true, Color.black);
		context.ExecuteCommandBuffer(cmd);
		cmd.Release();
		
		Resize(CameraTarget);
		Resize(asset.GPosition);
		Resize(asset.GNormal);
		Resize(asset.GDiffuse);
		Resize(asset.GDepth);

		//culling
		foreach (var cam in cameras) {
			cam.TryGetCullingParameters(out var cullingPameters);
			var cullingResults = context.Cull(ref cullingPameters);
			context.SetupCameraProperties(cam);
			var sortingSettings = new SortingSettings(cam);
			var filterSetting = FilteringSettings.defaultValue;
			var drawingSetting = new DrawingSettings(shaderGBuffer, sortingSettings);

			Shader.SetGlobalTexture(_DepthTexture, DepthTexture);
			cam.SetTargetBuffers(GBuffers, DepthTexture.depthBuffer);

			context.DrawRenderers(cullingResults, ref drawingSetting, ref filterSetting);
/*
			if (cam.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
				context.DrawSkybox(cam);
*/

			context.Submit();

			//Graphics.Blit(GBufferTextures[0], null as RenderTexture);
		}
	}
}
