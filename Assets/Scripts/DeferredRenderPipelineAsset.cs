using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class ShadowSetting {
    public enum TextureSize
    {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    };
    public enum FilterMode
    {
        PCF2x2, PCF3x3, PCF5x5, PCF7x7
    };
    public enum CascadeBlendMode
    {
        Hard, Soft, Dither
    };
    [System.Serializable]
    public struct Directional {
        public TextureSize atlasSize;
        public FilterMode filter;
        public CascadeBlendMode cascadeBlend;
        [Range(1,4)]
        public int cascadeCount;
        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
        [Range(0.001f, 1f)]
        public float cascadeFade;
        public Vector3 cascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
	};
    [System.Serializable]
    public struct Other {
        public TextureSize atlasSize;
        public FilterMode filter;
    };
    [Min(0f)]
    public float maxDistance = 100f;
    [Min(0.001f)]
    public float distanceFade = 0.1f;
    public Directional directional = new Directional { 
        atlasSize = TextureSize._1024,
        filter = FilterMode.PCF2x2,
        cascadeBlend = CascadeBlendMode.Hard,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f,
    };
    public Other other = new Other {
        atlasSize = TextureSize._1024,
        filter = FilterMode.PCF2x2,
    };
}

[CreateAssetMenu(menuName = "Rendering/ExampleRenderPipelineAsset")]
public class DeferredRenderPipelineAsset : RenderPipelineAsset
{
        public RenderTexture GPosition;
        public RenderTexture GNormal;
        public RenderTexture GDiffuse;
        public RenderTexture GDepth;
        public RenderTexture Target;
        public Material MatSSR;
        [SerializeField]
        public ShadowSetting shadows = default;
        protected override RenderPipeline CreatePipeline() {
                return new DeferredRenderPipeline(this);
        }
}

