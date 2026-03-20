using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>
/// URP ScriptableRendererFeature that injects a fullscreen edge detection pass.
/// Edges are drawn only within active echo pulse radii, using data from EchoManager.
/// Add this feature to the URP Renderer (PC_Renderer).
/// </summary>
public class EchoEdgeFeature : ScriptableRendererFeature
{
    [Header("Shader")]
    [SerializeField] private Shader _shader;

    [Header("Edge Detection")]
    [SerializeField] private float _edgeThickness = 1.0f;
    [SerializeField] private float _depthThreshold = 0.02f;
    [SerializeField] private float _normalThreshold = 0.3f;
    [SerializeField] private float _edgeIntensity = 2.5f;

    private Material _material;
    private EchoEdgePass _pass;

    public override void Create()
    {
        if (_shader == null)
            _shader = Shader.Find("Echo/EdgeDetection");

        if (_shader != null)
        {
            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new EchoEdgePass(_material)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _pass == null)
            return;

        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        _material.SetFloat("_EdgeThickness", _edgeThickness);
        _material.SetFloat("_DepthThreshold", _depthThreshold);
        _material.SetFloat("_NormalThreshold", _normalThreshold);
        _material.SetFloat("_EdgeIntensity", _edgeIntensity);

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }

    /// <summary>
    /// Fullscreen render pass using RenderGraph (Unity 6 / URP 17).
    /// </summary>
    private class EchoEdgePass : ScriptableRenderPass
    {
        private readonly Material _material;
        private const string PassName = "EchoEdgeDetection";

        public EchoEdgePass(Material material)
        {
            _material = material;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();

            // Can't run if camera has no valid color target
            if (resourceData.isActiveTargetBackBuffer) return;

            var source = resourceData.activeColorTexture;

            var descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = "_EchoEdgeTempRT";
            descriptor.clearBuffer = false;
            var tempTexture = renderGraph.CreateTexture(descriptor);

            // Pass 1: source → temp with edge detection material
            RenderGraphUtils.BlitMaterialParameters blitParams1 =
                new(source, tempTexture, _material, 0);
            renderGraph.AddBlitPass(blitParams1, PassName);

            // Pass 2: temp → source (copy back)
            RenderGraphUtils.BlitMaterialParameters blitParams2 =
                new(tempTexture, source, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0);
            renderGraph.AddBlitPass(blitParams2, "EchoEdgeCopyBack");
        }
    }
}
