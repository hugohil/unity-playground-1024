using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class SelectiveOutlineFeature : ScriptableRendererFeature {

    [System.Serializable]
    private class SelectiveOutlineSettings {
        public Color mainColor = Color.white;

        [Range(1, 6)]
        public int scale = 1;

        [Range(0.0f, 1.0f)]
        public float threshold = 0.5f;

        [Range(0.0f, 10.0f)]
        public float thickness = 1.0f;
    }

    private class SelectiveOutlinePass : ScriptableRenderPass {
        Material normalsMaterial;
        Material outlineMaterial;

        SelectiveOutlineSettings settings;
        FilteringSettings filteringSettings;

        RenderTextureDescriptor normalsDescriptor;

        private int normalsTexID = Shader.PropertyToID("_ViewSpaceNormals");

        public SelectiveOutlinePass(Material _viewSpaceNormalsMaterial, Material _outlineMaterial, SelectiveOutlineSettings _settings, LayerMask _layerMask) {
            settings = _settings;

            normalsMaterial = _viewSpaceNormalsMaterial;

            outlineMaterial = _outlineMaterial;
            outlineMaterial.SetColor("_MainColor", settings.mainColor);
            outlineMaterial.SetFloat("_Thickness", settings.thickness);

            normalsDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);

            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, _layerMask);
        }

        class PassData {
            public Material mat;
            public TextureHandle src;
            public RendererListHandle rendererList;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("ViewSpaceNormalsPass", out PassData passData)) {
                passData.mat = outlineMaterial;
                passData.mat.SetFloat("_Scale", (float)settings.scale);
                passData.mat.SetFloat("_Threshold", settings.threshold);

                builder.AllowGlobalStateModification(true);

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                passData.src = resourceData.cameraDepthTexture;
                // passData.src = resourceData.activeColorTexture;
                // builder.UseTexture(passData.src);

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                normalsDescriptor.width = cameraData.cameraTargetDescriptor.width;
                normalsDescriptor.height = cameraData.cameraTargetDescriptor.height;
                normalsDescriptor.depthBufferBits = 0;

                TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(renderGraph, normalsDescriptor, "_ViewSpaceNormals", false);
                // builder.SetRenderAttachment(dst, 0);
                // builder.SetGlobalTextureAfterPass(dst, normalsTexID);

                builder.AllowPassCulling(false);

                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                SortingCriteria sortFlags = cameraData.defaultOpaqueSortFlags;

                ShaderTagId shaderID = new ShaderTagId("UniversalForward");
                // ShaderTagId shaderID = new ShaderTagId(normalsMaterial.shader.name);

                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(shaderID, renderingData, cameraData, lightData, sortFlags);
                drawSettings.overrideMaterial = normalsMaterial;

                RendererListParams rendererListParameters = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);

                passData.rendererList = renderGraph.CreateRendererList(rendererListParameters);

                builder.UseRendererList(passData.rendererList);

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }

        static void ExecutePass(PassData data, RasterGraphContext context) {
            RasterCommandBuffer cmd = context.cmd;

            // cmd.ClearRenderTarget(true, true, Color.clear);

            cmd.DrawRendererList(data.rendererList);

            Blitter.BlitTexture(cmd, data.src, new Vector4(1, 1, 0, 0), data.mat, 0);
        }

        public void Release() {
            // ...
        }
    }

    [SerializeField] Shader viewSpaceNormalsShader;
    Material viewSpaceNormalsMaterial;

    [SerializeField] Shader outlineShader;
    Material outlineMaterial;

    SelectiveOutlinePass selectiveOutline;

    [SerializeField] LayerMask layerMask;
    [SerializeField] SelectiveOutlineSettings settings;

    public override void Create() {
        if (viewSpaceNormalsShader == null || outlineShader == null) {
            return;
        }
        viewSpaceNormalsMaterial = new Material(viewSpaceNormalsShader);
        outlineMaterial = new Material(outlineShader);

        selectiveOutline = new SelectiveOutlinePass(viewSpaceNormalsMaterial, outlineMaterial, settings, layerMask);
        selectiveOutline.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        if (renderingData.cameraData.cameraType == CameraType.Game) {
            renderer.EnqueuePass(selectiveOutline);
        }
    }

    protected override void Dispose(bool disposing) {
        #if UNITY_EDITOR
            if (EditorApplication.isPlaying) {
                Destroy(viewSpaceNormalsMaterial);
                Destroy(outlineMaterial);
            } else {
                DestroyImmediate(viewSpaceNormalsMaterial);
                DestroyImmediate(outlineMaterial);
            }
        #else
            Destroy(viewSpaceNormalsMaterial);
            Destroy(outlineMaterial);
        #endif
        selectiveOutline?.Release();
    }
}