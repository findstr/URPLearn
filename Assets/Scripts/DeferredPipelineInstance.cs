using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class DeferredRenderPipelineInstance : RenderPipeline
{
	private RenderTexture CameraTarget;
	private RenderTexture DepthTexture;
	private RenderTexture[] GBufferTextures;
	private RenderBuffer[] GBuffers;
	private int[] GBufferIDs;
	private int _CameraPos;
	private int _MatrixVP;
	private int _MainLightPosition;
	private int _MainLightColor;
	private int _ProjectionParams;
	private ShaderTagId shaderGBuffer;
	private static int _DepthTexture = Shader.PropertyToID("_DepthTexture");
	private DeferredRenderPipelineAsset asset;
	private void Resize(RenderTexture rt)
	{
		/*
		if (rt.width == Screen.width && rt.height == Screen.height)
			return ;
		rt.Release();
		rt.width = Screen.width;
		rt.height = Screen.height;
		rt.Create();
		*/
	}
	public DeferredRenderPipelineInstance(DeferredRenderPipelineAsset asset) {
		this.asset = asset;
		
		CameraTarget = asset.Target;
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
		_MatrixVP = Shader.PropertyToID("_MATRIX_VP");
		_CameraPos = Shader.PropertyToID("_CameraPos");
		_ProjectionParams = Shader.PropertyToID("_ProjectionArgs");

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

	protected void RenderGame(ScriptableRenderContext context, Camera camera) { 
		camera.TryGetCullingParameters(out var cullingPameters);
		var cullingResults = context.Cull(ref cullingPameters);
		context.SetupCameraProperties(camera);
		var sortingSettings = new SortingSettings(camera);
		var filterSetting = FilteringSettings.defaultValue;
		var drawingSetting = new DrawingSettings(shaderGBuffer, sortingSettings);

		/*
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

		var cmd = new CommandBuffer();
		cmd.name = "ClearScreen";
		cmd.ClearRenderTarget(true, true, Color.black);
		ctx.ExecuteCommandBuffer(cmd);
		cmd.Release();

		ctx.DrawRenderers(cullingResults, ref drawingSetting, ref filterSetting);
		
		cb = new CommandBuffer() {
			name = "ScreenSpaceReflection",
		};
		for (int i = 0; i < GBufferIDs.Length; i++) 
			asset.MatSSR.SetTexture(GBufferIDs[i], GBufferTextures[i]);	
		asset.MatSSR.SetVector(_CameraPos, cam.transform.position);
		asset.MatSSR.SetMatrix(_MatrixVP, GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix);
		asset.MatSSR.SetVector(_ProjectionParams, new Vector4(0, 0, 0, cam.farClipPlane));
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
		*/
		// Create the attachment descriptors. If these attachments are not specifically bound to any RenderTexture using the ConfigureTarget calls,
		// these are treated as temporary surfaces that are discarded at the end of the renderpass
		var albedo = new AttachmentDescriptor(RenderTextureFormat.ARGB32);
		var specRough = new AttachmentDescriptor(RenderTextureFormat.ARGB32);
		var normal = new AttachmentDescriptor(RenderTextureFormat.ARGB2101010);
		var emission = new AttachmentDescriptor(RenderTextureFormat.ARGBHalf);
		var depth = new AttachmentDescriptor(RenderTextureFormat.Depth);

		// At the beginning of the render pass, clear the emission buffer to all black, and the depth buffer to 1.0f
		emission.ConfigureClear(new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f, 0);
		depth.ConfigureClear(new Color(), 1.0f, 0);

		// Bind the albedo surface to the current camera target, so the final pass will render the Scene to the screen backbuffer
		// The second argument specifies whether the existing contents of the surface need to be loaded as the initial values;
		// in our case we do not need that because we'll be clearing the attachment anyway. This saves a lot of memory
		// bandwidth on tiled GPUs.
		// The third argument specifies whether the rendering results need to be written out to memory at the end of
		// the renderpass. We need this as we'll be generating the final image there.
		// We could do this in the constructor already, but the camera target may change on the fly, esp. in the editor
		albedo.ConfigureTarget(BuiltinRenderTextureType.CameraTarget, false, true);

        // All other attachments are transient surfaces that are not stored anywhere. If the renderer allows,
        // those surfaces do not even have a memory allocated for the pixel values, saving RAM usage.

        // Start the renderpass using the given scriptable rendercontext, resolution, samplecount, array of attachments that will be used within the renderpass and the depth surface
        var attachments = new NativeArray<AttachmentDescriptor>(5, Allocator.Temp);
        const int depthIndex = 0, albedoIndex = 1, specRoughIndex = 2, normalIndex = 3, emissionIndex = 4;
        attachments[depthIndex] = depth;
        attachments[albedoIndex] = albedo;
        attachments[specRoughIndex] = specRough;
        attachments[normalIndex] = normal;
        attachments[emissionIndex] = emission;
        context.BeginRenderPass(camera.pixelWidth, camera.pixelHeight, 1, attachments, depthIndex);
        attachments.Dispose();

        // Start the first subpass, GBuffer creation: render to albedo, specRough, normal and emission, no need to read any input attachments
        var gbufferColors = new NativeArray<int>(4, Allocator.Temp);
        gbufferColors[0] = albedoIndex;
        gbufferColors[1] = specRoughIndex;
        gbufferColors[2] = normalIndex;
        gbufferColors[3] = emissionIndex;
        context.BeginSubPass(gbufferColors);
        gbufferColors.Dispose();

	context.DrawRenderers(cullingResults, ref drawingSetting, ref filterSetting);
        // Render the deferred G-Buffer
        // RenderGBuffer(cullResults, camera, context);

        context.EndSubPass();

        // Second subpass, lighting: Render to the emission buffer, read from albedo, specRough, normal and depth.
        // The last parameter indicates whether the depth buffer can be bound as read-only.
        // Note that some renderers (notably iOS Metal) won't allow reading from the depth buffer while it's bound as Z-buffer,
        // so those renderers should write the Z into an additional FP32 render target manually in the pixel shader and read from it instead
        var lightingColors = new NativeArray<int>(1, Allocator.Temp);
        lightingColors[0] = emissionIndex;
        var lightingInputs = new NativeArray<int>(4, Allocator.Temp);
        lightingInputs[0] = albedoIndex;
        lightingInputs[1] = specRoughIndex;
        lightingInputs[2] = normalIndex;
        lightingInputs[3] = depthIndex;
        context.BeginSubPass(lightingColors, lightingInputs, true);
        lightingColors.Dispose();
        lightingInputs.Dispose();

        // PushGlobalShadowParams(context);
        // RenderLighting(camera, cullResults, context);

        context.EndSubPass();

        // Third subpass, tonemapping: Render to albedo (which is bound to the camera target), read from emission.
        var tonemappingColors = new NativeArray<int>(1, Allocator.Temp);
        tonemappingColors[0] = albedoIndex;
        var tonemappingInputs = new NativeArray<int>(1, Allocator.Temp);
        tonemappingInputs[0] = emissionIndex;
        context.BeginSubPass(tonemappingColors, tonemappingInputs, true);
        tonemappingColors.Dispose();
        tonemappingInputs.Dispose();

        // present frame buffer.
        // FinalPass(context);

        context.EndSubPass();

        context.EndRenderPass();
	context.Submit();
	}

	protected override void Render(ScriptableRenderContext context, Camera[] cameras)
	{
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
		
				RenderScene(context, cam);
				break; }
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
