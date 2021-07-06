using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class DeferredRenderPipeline : RenderPipeline
{
	private RenderTexture CameraTarget;
	private RenderTexture DepthTexture;
	private RenderTexture[] GBufferTextures;
	private RenderBuffer[] GBuffers;
	private int[] GBufferIDs;
	private int _CameraPos;
	private int _MatrixVP;
	private int _ProjectionParams;
	private ShaderTagId shaderGBuffer;
	private static int _DepthTexture = Shader.PropertyToID("_DepthTexture");
	private DeferredRenderPipelineAsset asset;
	private Lighting light = new Lighting();
	private CameraRender camera_render = new CameraRender();
	private void Resize(RenderTexture rt)
	{
		if (rt.width == Screen.width && rt.height == Screen.height)
			return ;
		rt.Release();
		rt.width = Screen.width;
		rt.height = Screen.height;
		rt.Create();
	}
	public DeferredRenderPipeline(DeferredRenderPipelineAsset asset) {
		this.asset = asset;
		GraphicsSettings.lightsUseLinearIntensity = true;
		camera_render.SetShadowSetting(ref asset.shadows);
/*
		CameraTarget = asset.Target;
		DepthTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
		DepthTexture.name = "DepthTexture";

		Resize(CameraTarget);
		Resize(asset.GPosition);
		Resize(asset.GNormal);
		Resize(asset.GDiffuse);
		Resize(asset.GDepth);

		GBufferTextures = new RenderTexture[] {
			asset.GDiffuse,
			asset.GPosition,
			asset.GNormal,
			asset.GDepth,
		};
		GBufferTextures[0].name = "_GDiffuse";
		GBufferTextures[1].name = "_GPosition";
		GBufferTextures[2].name = "_GNormal";
		GBufferTextures[3].name = "_GDepth";

		GBuffers = new RenderBuffer[GBufferTextures.Length];
		GBufferIDs = new int[GBufferTextures.Length];
		for (int i = 0; i < GBuffers.Length; i++) {
			GBuffers[i] = GBufferTextures[i].colorBuffer;
			GBufferIDs[i] = Shader.PropertyToID(GBufferTextures[i].name);
		}

		_MatrixVP = Shader.PropertyToID("_MATRIX_VP");
		_CameraPos = Shader.PropertyToID("_CameraPos");
		_ProjectionParams = Shader.PropertyToID("_ProjectionArgs");
		shaderGBuffer = new ShaderTagId("GBuffer");
	*/
	}


	protected void RenderScene(ScriptableRenderContext ctx, Camera cam, ShadowSetting shadow) { 
		cam.TryGetCullingParameters(out var cullingPameters);
		var cullingResults = ctx.Cull(ref cullingPameters);
		ctx.SetupCameraProperties(cam);
		var sortingSettings = new SortingSettings(cam);
		var filterSetting = FilteringSettings.defaultValue;
		var drawingSetting = new DrawingSettings(shaderGBuffer, sortingSettings);

		ctx.DrawRenderers(cullingResults, ref drawingSetting, ref filterSetting);
		
		if (cam.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
			ctx.DrawSkybox(cam);
		
		ctx.Submit();
	}

	protected void RenderGame(ScriptableRenderContext context, Camera camera, ShadowSetting shadow) { 
		/*
		if (!camera.TryGetCullingParameters(out var cullingPameters))
			return ;
		cullingPameters.shadowDistance = Mathf.Min(shadow.maxDistance, camera.farClipPlane);
		var cullingResults = context.Cull(ref cullingPameters);
		context.SetupCameraProperties(camera);
		light.setup(ref context, ref cullingResults, ref shadow);
		var sortingSettings = new SortingSettings(camera);
		var filterSetting = FilteringSettings.defaultValue;
		var drawingSetting = new DrawingSettings(shaderGBuffer, sortingSettings);

		Shader.SetGlobalTexture(_DepthTexture, DepthTexture);
		for (int i = 0; i < GBufferTextures.Length; i++) { 
			Resize(GBufferTextures[i]);
			GBuffers[i] = GBufferTextures[i].colorBuffer;
		}
		Resize(CameraTarget);
		Resize(DepthTexture);
		camera.SetTargetBuffers(GBuffers, DepthTexture.depthBuffer);

		var cmd = new CommandBuffer();
		cmd.name = "ClearScreen";
		cmd.ClearRenderTarget(true, true, Color.black);
		context.ExecuteCommandBuffer(cmd);
		cmd.Release();

		context.DrawRenderers(cullingResults, ref drawingSetting, ref filterSetting);
		

		var cb = new CommandBuffer() {
			name = "ScreenSpaceReflection",
		};
		for (int i = 0; i < GBufferIDs.Length; i++) 
			asset.MatSSR.SetTexture(GBufferIDs[i], GBufferTextures[i]);	
		asset.MatSSR.SetVector(_CameraPos, camera.transform.position);
		asset.MatSSR.SetMatrix(_MatrixVP, GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix);
		asset.MatSSR.SetVector(_ProjectionParams, new Vector4(0, 0, 0, camera.farClipPlane));
		cb.Blit(null, CameraTarget, asset.MatSSR, 0);
		context.ExecuteCommandBuffer(cb);
		cb.Clear();

		cb = new CommandBuffer() {
			name = "Blit To Screen",
		};
		cb.Blit(CameraTarget, null as RenderTexture);
		context.ExecuteCommandBuffer(cb);
		cb.Release();
 
		context.Submit();
		*/
	}

	protected override void Render(ScriptableRenderContext context, Camera[] cameras)
	{
		/*
		//clear screen	
		Resize(CameraTarget);
		Resize(asset.GPosition);
		Resize(asset.GNormal);
		Resize(asset.GDiffuse);
		Resize(asset.GDepth);
		//culling
		foreach (var cam in cameras) {
			switch (cam.cameraType) { 
			case CameraType.SceneView: { 
				var cmd = new CommandBuffer();
				cmd.name = "ClearScreen";
				cmd.ClearRenderTarget(true, true, Color.black);
				context.ExecuteCommandBuffer(cmd);
				cmd.Release();
		
				RenderScene(context, cam, shadowSetting);
				break; }
			case CameraType.Game:
				RenderGame(context, cam, shadowSetting);
				break;
			default:
				RenderScene(context, cam, shadowSetting);
				break;
			}
		}*/
		foreach (var cam in cameras)
			RenderScene(context, cam, null);
	}
}
