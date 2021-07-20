using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

class Lighting
{
	static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

	private const int MAX_DIRECTIONAL_LIGHT = Config.MAX_DIRECTIONAL_LIGHT_COUNT;
	private const int MAX_OTHER_LIGHT = Config.MAX_OTHER_LIGHT_COUNT;

	private int id_light_directional_count = Shader.PropertyToID("light_directional_count");
	private int id_light_directional_color = Shader.PropertyToID("light_directional_color");
	private int id_light_directional_direction = Shader.PropertyToID("light_directional_direction");

	private int _OtherLightCount = Shader.PropertyToID("_OtherLightCount");
	private int _OtherLightColors = Shader.PropertyToID("_OtherLightColors");
	private int _OtherLightPositions = Shader.PropertyToID("_OtherLightPositions");
	private int _OtherLightDirections = Shader.PropertyToID("_OtherLightDirections");
	private int _OtherLightSpotAngles = Shader.PropertyToID("_OtherLightSpotAngles");


	private Vector4[] light_directional_color		= new Vector4[MAX_DIRECTIONAL_LIGHT];
	private Vector4[] light_directional_direction	= new Vector4[MAX_DIRECTIONAL_LIGHT];
	private Vector4[] otherLightColors				= new Vector4[MAX_OTHER_LIGHT];
	private Vector4[] otherLightPositions			= new Vector4[MAX_OTHER_LIGHT];
	private Vector4[] otherLightDirections			= new Vector4[MAX_OTHER_LIGHT];
	private Vector4[] otherLightSpotAngles			= new Vector4[MAX_OTHER_LIGHT];

	public Lighting()
    {

    }

	private void SetupDirectionalLight(int idx, ref VisibleLight visibleLight)
    {
		light_directional_color[idx] = visibleLight.finalColor;
		light_directional_direction[idx] = -visibleLight.localToWorldMatrix.GetColumn(2);
    }

	private void SetupPointLight(int idx, ref VisibleLight visibleLight)
    {
		otherLightColors[idx] = visibleLight.finalColor;
		Vector4 pos = visibleLight.localToWorldMatrix.GetColumn(3);
		pos.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[idx] = pos;
		otherLightSpotAngles[idx] = new Vector4(0f, 1f);
    }

	private void SetupSpotLight(int idx, ref VisibleLight visibleLight)
    {
		Light light = visibleLight.light;
		float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
		float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
		float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
		Vector4 pos = visibleLight.localToWorldMatrix.GetColumn(3);
		pos.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.000001f);
		otherLightColors[idx] = visibleLight.finalColor;
		otherLightPositions[idx] = pos;
		otherLightDirections[idx] = -visibleLight.localToWorldMatrix.GetColumn(2);
		otherLightSpotAngles[idx] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
    }
	public void setup(RenderContext ctx, bool useLightsPerObject)
    {
		int i;
		int directLightCount = 0;
		int otherLightCount = 0;
		NativeArray<int> indexMap = useLightsPerObject ? ctx.cull_result.GetLightIndexMap(Allocator.Temp) : default;
		NativeArray<VisibleLight> lights = ctx.cull_result.visibleLights;
		for (i = 0; i < lights.Length; i++) {
			int newIndex = -1;
			VisibleLight vl = lights[i];
			switch (vl.lightType) {
			case LightType.Directional:
				if (directLightCount < MAX_DIRECTIONAL_LIGHT) { 
					SetupDirectionalLight(directLightCount++, ref vl);
				}
				break;
			case LightType.Point:
				if (otherLightCount < MAX_OTHER_LIGHT) {
					newIndex = otherLightCount;
					SetupPointLight(otherLightCount++, ref vl);
				}
				break;
			case LightType.Spot:
				if (otherLightCount < MAX_OTHER_LIGHT) {
					newIndex = otherLightCount;
					SetupSpotLight(otherLightCount++, ref vl);
	            }
				break;
			default:
				Debug.LogError("DeferredPipeline: not support:" + vl.lightType);
				break;
            }
			if (useLightsPerObject)
				indexMap[i] = newIndex;
        }
		if (useLightsPerObject) { 
            while (i < indexMap.Length)
                indexMap[i++] = -1;
			ctx.cull_result.SetLightIndexMap(indexMap);
			indexMap.Dispose();
			Shader.EnableKeyword(lightsPerObjectKeyword);
		} else {
			Shader.DisableKeyword(lightsPerObjectKeyword);
        }
		var cb = ctx.command_begin("Lighting");
		cb.SetGlobalInt(id_light_directional_count, directLightCount);
		if (directLightCount > 0) { 
            cb.SetGlobalVectorArray(id_light_directional_color, light_directional_color);
            cb.SetGlobalVectorArray(id_light_directional_direction, light_directional_direction);
		}
		cb.SetGlobalInt(_OtherLightCount, otherLightCount);
		if (otherLightCount > 0) {
			cb.SetGlobalVectorArray(_OtherLightColors, otherLightColors);
			cb.SetGlobalVectorArray(_OtherLightPositions, otherLightPositions);
			cb.SetGlobalVectorArray(_OtherLightDirections, otherLightDirections);
			cb.SetGlobalVectorArray(_OtherLightSpotAngles, otherLightSpotAngles);
        }
		ctx.command_end();
    }
} 
