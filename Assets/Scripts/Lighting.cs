using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

class Lighting
{
	private const int MAX_DIRECTIONAL_LIGHT = Config.MAX_LIGHT_COUNT;
	private int id_light_directional_count;
    private int id_light_directional_color;
    private int id_light_directional_direction;

	private int light_directional_count = 0;
	private Vector4[] light_directional_color		= new Vector4[MAX_DIRECTIONAL_LIGHT];
	private Vector4[] light_directional_direction	= new Vector4[MAX_DIRECTIONAL_LIGHT];
	
	public Lighting()
    {
		id_light_directional_count = Shader.PropertyToID("light_directional_count");
		id_light_directional_color = Shader.PropertyToID("light_directional_color");
		id_light_directional_direction = Shader.PropertyToID("light_directional_direction");
    }

	public void setup(RenderContext ctx)
    {
		light_directional_count = 0;
		NativeArray<VisibleLight> lights = ctx.cull_result.visibleLights;
		for (int i = 0; i < lights.Length; i++) {
			VisibleLight vl = lights[i];
			switch (vl.lightType) {
			case LightType.Directional:
				if (light_directional_count >= MAX_DIRECTIONAL_LIGHT)
					continue;
				light_directional_color[light_directional_count] = vl.finalColor;
				light_directional_direction[light_directional_count] = -vl.localToWorldMatrix.GetColumn(2);
				++light_directional_count;
				break;
			case LightType.Point:
				break;
			case LightType.Spot:
				break;
			default:
				Debug.LogError("DeferredPipeline: not support:" + vl.lightType);
				break;
            }
        }
		var cb = ctx.command_begin("Lighting");
		cb.SetGlobalInt(id_light_directional_count, light_directional_count);
		cb.SetGlobalVectorArray(id_light_directional_color, light_directional_color);
		cb.SetGlobalVectorArray(id_light_directional_direction, light_directional_direction);
		ctx.command_end();
    }
} 
