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

        [Range(0.0f, 10.0f)]
        public float thickness = 1.0f;
    }

    private class SelectiveOutlinePass : ScriptableRenderPass {
        Material viewSpaceNormalMaterial;
        Material outlineMaterial;

        SelectiveOutlineSettings settings;
        FilteringSettings filteringSettings;

        RenderTextureDescriptor normalsDescriptor;

        public SelectiveOutlinePass(Material _viewSpaceNormalsMaterial, Material _outlineMaterial, SelectiveOutlineSettings _settings, LayerMask _layerMask) {
            settings = _settings;

            viewSpaceNormalMaterial = _viewSpaceNormalsMaterial;

            outlineMaterial = _outlineMaterial;
            outlineMaterial.SetColor("_MainColor", settings.mainColor);
            outlineMaterial.SetFloat("_Thickness", settings.thickness);

            normalsDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height,
            RenderTextureFormat.Default, 0);

            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, _layerMask);
        }

        class PassData {
            public Material mat;
            public TextureHandle src;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("ViewSpaceNormalsPass", out PassData passData)) {
                passData.mat = viewSpaceNormalMaterial;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                passData.src = resourceData.cameraDepthTexture;
                // passData.src = resourceData.activeColorTexture;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                normalsDescriptor.width = cameraData.cameraTargetDescriptor.width;
                normalsDescriptor.height = cameraData.cameraTargetDescriptor.height;
                normalsDescriptor.depthBufferBits = 0;

                TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(renderGraph, normalsDescriptor, "_ViewSpaceNormals", false);

                builder.UseTexture(passData.src);

                builder.SetRenderAttachment(dst, 0);

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }

        static void ExecutePass(PassData data, RasterGraphContext context) {
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.mat, 0);
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