using System;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Render all objects that have a 'DepthOnly' pass into the given depth buffer.
    ///
    /// You can use this pass to prime a depth buffer for subsequent rendering.
    /// Use it as a z-prepass, or use it to generate a depth buffer.
    /// </summary>
    public class DepthOnlyPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId k_ShaderTagId = new ShaderTagId("DepthOnly");

        private RTHandle destination { get; set; }
        private GraphicsFormat depthStencilFormat;
        internal ShaderTagId shaderTagId { get; set; } = k_ShaderTagId;

        FilteringSettings m_FilteringSettings;

        /// <summary>
        /// Creates a new <c>DepthOnlyPass</c> instance.
        /// </summary>
        /// <param name="evt">The <c>RenderPassEvent</c> to use.</param>
        /// <param name="renderQueueRange">The <c>RenderQueueRange</c> to use for creating filtering settings that control what objects get rendered.</param>
        /// <param name="layerMask">The layer mask to use for creating filtering settings that control what objects get rendered.</param>
        /// <seealso cref="RenderPassEvent"/>
        /// <seealso cref="RenderQueueRange"/>
        /// <seealso cref="LayerMask"/>
        public DepthOnlyPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            base.profilingSampler = new ProfilingSampler(nameof(DepthOnlyPass));
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            renderPassEvent = evt;
            useNativeRenderPass = false;
        }

        /// <summary>
        /// Configures the pass.
        /// </summary>
        /// <param name="baseDescriptor">The <c>RenderTextureDescriptor</c> used for the depthStencilFormat.</param>
        /// <param name="depthAttachmentHandle">The <c>RTHandle</c> used to render to.</param>
        /// <seealso cref="RenderTextureDescriptor"/>
        /// <seealso cref="RTHandle"/>
        /// <seealso cref="GraphicsFormat"/>
        public void Setup(
            RenderTextureDescriptor baseDescriptor,
            RTHandle depthAttachmentHandle)
        {
            this.destination = depthAttachmentHandle;
            this.depthStencilFormat = baseDescriptor.depthStencilFormat;
            this.shaderTagId = k_ShaderTagId;
        }

        /// <inheritdoc />
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;

            // When depth priming is in use the camera target should not be overridden so the Camera's MSAA depth attachment is used.
            if (renderingData.cameraData.renderer.useDepthPriming && (renderingData.cameraData.renderType == CameraRenderType.Base || renderingData.cameraData.clearDepth))
            {
                ConfigureTarget(renderingData.cameraData.renderer.cameraDepthTargetHandle);
                // Only clear depth here so we don't clear any bound color target. It might be unused by this pass but that doesn't mean we can just clear it. (e.g. in case of overlay cameras + depth priming)
                ConfigureClear(ClearFlag.Depth, Color.black);
            }
            // When not using depth priming the camera target should be set to our non MSAA depth target.
            else
            {
                useNativeRenderPass = true;
                ConfigureTarget(destination);
                ConfigureClear(ClearFlag.All, Color.black);
            }
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = renderingData.commandBuffer;
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.DepthPrepass)))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(this.shaderTagId, ref renderingData, sortFlags);
                drawSettings.perObjectData = PerObjectData.None;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
            }
        }
    }
}
