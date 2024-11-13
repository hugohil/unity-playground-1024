using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class SelectiveOutlineRendererFeature : ScriptableRendererFeature {
    [System.Serializable]
    private class SelectiveOutlineSettings {
        public LayerMask layerMask;

        public Color color = Color.white;

        [Range(0, 10)]
        public int thickness = 1;

        // [Range(0, 10)]
        // public int depthSampleScale = 1;
        // [Range(0, 10)]
        // public int normalsSampleScale = 1;

        [Range(0f, 10.0f)]
        public float noiseSpeed = 3.0f;
        [Range(0f, 20.0f)]
        public float noiseAmplitude = 2.0f;
        [Range(0f, 100.0f)]
        public float noiseDensity = 50.0f;

        [Range(1.0f, 20.0f)]
        public float depthMultiplier = 5.0f;
        [Range(0.0f, 1.0f)]
        public float depthThreshold = 0.5f;

        [Range(0.0f, 2.0f)]
        public float normalsMultiplier = 0.8f;
        [Range(0.0f, 10.0f)]
        public float normalsContrast = 1.5f;
        [Range(0.0f, 1.0f)]
        public float normalsThreshold = 0.5f;
    }

    class SelectiveOutlinePass : ScriptableRenderPass {
        const string m_passName = "SelectiveOutlinePass";
        Material normalsMaterial;
        Material outlineMaterial;

        private int globalTextureID = Shader.PropertyToID("_SelectiveOutlineTexture");

        private List<ShaderTagId> m_shaderTagIds = new List<ShaderTagId> { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };

        SelectiveOutlineSettings m_settings;

        public void Setup(Shader viewSpaceNormalsShader, Shader outlineShader, SelectiveOutlineSettings settings) {
            normalsMaterial = new Material(viewSpaceNormalsShader);
            outlineMaterial = new Material(outlineShader);

            m_settings = settings;
        }

        private class NormalsPassData {
            public RendererListHandle normalsRendererList;
        }
        private class OutlinePassData {
            public Material outlineMaterial;
            public TextureHandle normals;
            public TextureHandle depth;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            if (resourceData.isActiveTargetBackBuffer) {
                Debug.LogError("SelectiveOutlinePass requires an intermediate texture to render to.");
                return;
            }

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            TextureHandle normals = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_viewSpaceNormals", true);
            TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SelectiveOutlineTexture", true);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<NormalsPassData>(m_passName + "Normals", out NormalsPassData passData)) {
                SortingCriteria sortFlags = cameraData.defaultOpaqueSortFlags;
                RenderQueueRange renderQueueRange = RenderQueueRange.all;
                FilteringSettings filterSettings = new FilteringSettings(renderQueueRange, m_settings.layerMask);
                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(m_shaderTagIds, renderingData, cameraData, lightData, sortFlags);
                drawSettings.overrideMaterial = normalsMaterial;

                RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);

                passData.normalsRendererList = renderGraph.CreateRendererList(rendererListParams);

                builder.UseRendererList(passData.normalsRendererList);

                builder.SetRenderAttachment(normals, 0);

                builder.SetRenderFunc((NormalsPassData data, RasterGraphContext context) => ExecuteNormalsPass(data, context));
                builder.AllowPassCulling(false);
            }

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<OutlinePassData>(m_passName + "Outline", out OutlinePassData passData)) {
                builder.UseTexture(normals, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                passData.normals = normals;
                passData.depth = resourceData.cameraDepthTexture;

                // you can set properties here, except for textures coming from this render graph
                outlineMaterial.SetColor("_Color", m_settings.color);
                outlineMaterial.SetFloat("_Thickness", m_settings.thickness);
                outlineMaterial.SetFloat("_NoiseSpeed", m_settings.noiseSpeed);
                outlineMaterial.SetFloat("_NoiseAmplitude", m_settings.noiseAmplitude);
                outlineMaterial.SetFloat("_NoiseDensity", m_settings.noiseDensity);
                outlineMaterial.SetFloat("_DepthMultiplier", m_settings.depthMultiplier);
                outlineMaterial.SetFloat("_NormalsMultiplier", m_settings.normalsMultiplier);
                outlineMaterial.SetFloat("_NormalsContrast", m_settings.normalsContrast);
                outlineMaterial.SetFloat("_DepthThreshold", m_settings.depthThreshold);
                outlineMaterial.SetFloat("_NormalsThreshold", m_settings.normalsThreshold);
                // outlineMaterial.SetFloat("_NormalsSampleScale", m_settings.normalsSampleScale);
                // outlineMaterial.SetFloat("_DepthSampleScale", m_settings.depthSampleScale);

                passData.outlineMaterial = outlineMaterial;

                builder.SetRenderAttachment(destination, 0);
                builder.SetGlobalTextureAfterPass(destination, globalTextureID);

                builder.SetRenderFunc((OutlinePassData data, RasterGraphContext context) => ExecuteOutlinePass(data, context));
                builder.AllowPassCulling(false);
            }
        }

        static void ExecuteNormalsPass(NormalsPassData data, RasterGraphContext context) {
            context.cmd.DrawRendererList(data.normalsRendererList);
        }

        static void ExecuteOutlinePass(OutlinePassData data, RasterGraphContext context) {
            // setting textures from render graph outside the pass execution scope will break the render graph (you're trying to access a texture that is not yet created blabla)
            data.outlineMaterial.SetTexture("_NormalsTex", data.normals);
            data.outlineMaterial.SetTexture("_DepthTex", data.depth);

            context.cmd.DrawProcedural(Matrix4x4.identity, data.outlineMaterial, 0, MeshTopology.Triangles, 3);
        }

        public void Dispose() {
            CoreUtils.Destroy(normalsMaterial);
            CoreUtils.Destroy(outlineMaterial);
        }
    }

    SelectiveOutlinePass m_ScriptablePass;

    [SerializeField]
    Shader viewSpaceNormalsShader = null;

    [SerializeField]
    Shader outlineShader = null;

    [SerializeField]
    RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;

    [SerializeField] SelectiveOutlineSettings settings;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new SelectiveOutlinePass();

        m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Depth);

        m_ScriptablePass.renderPassEvent = injectionPoint;
    }

    public void Dispose() {
        if (m_ScriptablePass != null) {
            m_ScriptablePass.Dispose();
        }
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_ScriptablePass == null)
        {
            Debug.LogError("SelectiveOutlinePass is not properly initialized.");
            return;
        }
        m_ScriptablePass.Setup(viewSpaceNormalsShader, outlineShader, settings);
        renderer.EnqueuePass(m_ScriptablePass);
    }
}
