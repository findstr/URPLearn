using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/ExampleRenderPipelineAsset")]
public class DeferredRenderPipelineAsset : RenderPipelineAsset
{
        public RenderTexture GPosition;
        public RenderTexture GNormal;
        public RenderTexture GDiffuse;
        public RenderTexture GDepth;
        public RenderTexture Target;
        public Material MatSSR;
        protected override RenderPipeline CreatePipeline() {
                return new DeferredRenderPipeline(this);
        }
}

