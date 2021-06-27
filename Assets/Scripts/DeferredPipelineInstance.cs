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
	private int _MainLightPosition;
	private int _MainLightColor;
	private ShaderTagId shaderGBuffer;
	private static int _DepthTexture = Shader.PropertyToID("_DepthTexture");
	private DeferredRenderPipelineAsset asset;
	private void Resize(RenderTexture rt)
	{
		if (rt.width == Screen.width && rt.height == Screen.height)
			return ;
		rt.Release();
		rt.width = Screen.width;
		rt.height = Screen.height;
		rt.Create();
	}
	public DeferredRenderPipelineInstance(DeferredRenderPipelineAsset asset) {
		this.asset = asset;
		
		CameraTarget = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
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

		_MainLightColor = Shader.PropertyToID("_MainLightColor");
		_MainLightPosition = Shader.PropertyToID("_MainLightPosition");

		shaderGBuffer = new ShaderTagId("GBuffer");
	}
	protected void RenderScene(ScriptableRenderContext ctx, Camera cam) { 
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

	protected void RenderGame(ScriptableRenderContext ctx, Camera cam) { 
			CommandBuffer cb;
			cam.TryGetCullingParameters(out var cullingPameters);
			var cullingResults = ctx.Cull(ref cullingPameters);
			ctx.SetupCameraProperties(cam);
			var sortingSettings = new SortingSettings(cam);
			var filterSetting = FilteringSettings.defaultValue;
			var drawingSetting = new DrawingSettings(shaderGBuffer, sortingSettings);

			cb = new CommandBuffer() {
				name = "SetLight",
            };

			Light light = RenderSettings.sun;
			Shader.SetGlobalVector(_MainLightPosition, -light.transform.forward);
			Shader.SetGlobalVector(_MainLightColor, light.color.linear);
			Shader.SetGlobalTexture(_DepthTexture, DepthTexture);
			for (int i = 0; i < GBufferTextures.Length; i++) { 
				Resize(GBufferTextures[i]);
				GBuffers[i] = GBufferTextures[i].colorBuffer;
			}
			Resize(CameraTarget);
			Resize(DepthTexture);
			cam.SetTargetBuffers(GBuffers, DepthTexture.depthBuffer);

			ctx.DrawRenderers(cullingResults, ref drawingSetting, ref filterSetting);
			
			if (cam.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
				ctx.DrawSkybox(cam);
			
			cb = new CommandBuffer() {
				name = "ScreenSpaceReflection",
			};
			for (int i = 0; i < GBufferIDs.Length; i++) {
				Resize(GBufferTextures[i]);
				asset.MatSSR.SetTexture(GBufferIDs[i], GBufferTextures[i]);
			}
			cb.Blit(null, CameraTarget, asset.MatSSR, 0);
			ctx.ExecuteCommandBuffer(cb);
			cb.Clear();

			cb = new CommandBuffer() {
				name = "Blit To Screen",
			};
			cb.Blit(CameraTarget, null as RenderTexture);
			ctx.ExecuteCommandBuffer(cb);
			cb.Release();

	    
			ctx.Submit();
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
			switch (cam.cameraType) { 
			case CameraType.SceneView:
				ScriptableRenderContext.EmitWorldGeometryForSceneView(cam);
				RenderScene(context, cam);
				break;
			case CameraType.Game:
				RenderGame(context, cam);
				break;
			default:
				RenderScene(context, cam);
				break;
			}
		}
	}
}
