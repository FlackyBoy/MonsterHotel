using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Capture la profondeur de l'environnement (blocs, murs, monstres...) dans une texture globale
/// "_EnvironmentDepthTex", en excluant explicitement la layer du joueur (occluderLayers doit
/// exclure "Player") — but unique : donner à PlayerSilhouette.shader un depth buffer de référence
/// qui ne contient JAMAIS la géométrie du joueur, pour distinguer "caché par un objet externe" de
/// "caché par soi-même" (bras devant le torse) sans les pièges d'un stencil partagé entre passes
/// (tentative précédente abandonnée, voir _Dev/TODO.md T1).
///
/// Setup manuel requis (voir _Dev/TODO.md) :
///   1. Créer une Layer "Player" (Project Settings → Tags and Layers), l'assigner au root de
///      Player.prefab (et propager aux enfants).
///   2. Ajouter ce Renderer Feature sur l'asset Renderer URP utilisé (Assets/Settings/PC_Renderer.asset).
///   3. Régler "Occluder Layers" sur Everything SAUF Player.
/// </summary>
public class PlayerOccluderDepthFeature : ScriptableRendererFeature
{
    [Tooltip("Tout ce qui doit compter comme occultant valide pour la silhouette joueur — DOIT exclure la layer du joueur lui-même.")]
    public LayerMask occluderLayers = ~0;

    EnvironmentDepthPass _pass;

    public override void Create()
    {
        _pass = new EnvironmentDepthPass(occluderLayers)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _pass.SetLayerMask(occluderLayers);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }

    class EnvironmentDepthPass : ScriptableRenderPass
    {
        static readonly int EnvironmentDepthTexID = Shader.PropertyToID("_EnvironmentDepthTex");
        static readonly ShaderTagId DepthOnlyTag = new ShaderTagId("DepthOnly");

        LayerMask _layerMask;
        RTHandle _depthTarget;

        public EnvironmentDepthPass(LayerMask layerMask) => _layerMask = layerMask;

        public void SetLayerMask(LayerMask layerMask) => _layerMask = layerMask;

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.colorFormat  = RenderTextureFormat.Depth;
            desc.depthBufferBits = 24;
            desc.msaaSamples  = 1;
            RenderingUtils.ReAllocateIfNeeded(ref _depthTarget, desc, name: "_EnvironmentDepthTex");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("Environment Depth (occluders only)");

            cmd.SetRenderTarget(_depthTarget);
            cmd.ClearRenderTarget(true, false, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var drawSettings   = CreateDrawingSettings(DepthOnlyTag, ref renderingData, SortingCriteria.CommonOpaque);
            var filterSettings = new FilteringSettings(RenderQueueRange.opaque, _layerMask);
            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);

            cmd.SetGlobalTexture(EnvironmentDepthTexID, _depthTarget);

            // Restaure la cible de rendu de la caméra avant de rendre la main à URP — sans ça, tout
            // ce qui se dessine ensuite dans la frame (transparents, silhouette du joueur,
            // post-processing...) continue d'écrire dans _depthTarget au lieu de l'écran.
            var cameraData = renderingData.cameraData;
            cmd.SetRenderTarget(cameraData.renderer.cameraColorTargetHandle, cameraData.renderer.cameraDepthTargetHandle);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose() => _depthTarget?.Release();
    }
}
