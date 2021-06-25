using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/ExampleRenderPipelineAsset")]
public class ExampleRenderPipelineAsset : RenderPipelineAsset
{
        public Color exampleColor;
        public string exampleString;
        public RenderTexture _GPosition;
        public RenderTexture _GNormal;
        public RenderTexture _GDiffuse;
        public RenderTexture _GDepth;
        protected override RenderPipeline CreatePipeline() {
                return new ExampleRenderPipelineInstance(this);
        }
}

