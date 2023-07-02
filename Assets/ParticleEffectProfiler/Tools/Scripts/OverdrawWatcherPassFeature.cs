using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OverdrawWatcherPassFeature : ScriptableRendererFeature
{
    [Serializable]
    public class OverdrawWatcherSetting
    {

        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;

        public Shader shader = null;
    }

    public OverdrawWatcherSetting overdrawWatcherSetting = new OverdrawWatcherSetting();
    class OverdrawWatcherPass : ScriptableRenderPass
    {
        Material material;

        RenderTargetIdentifier src;

        private FilteringSettings filteringSettings;
        private List<ShaderTagId> tagIdList = new List<ShaderTagId>();

        int overdrawWatcherTexture;

        public OverdrawWatcherPass(OverdrawWatcherSetting setting)
        {

            tagIdList.Add(new ShaderTagId("UniversalForward"));
            tagIdList.Add(new ShaderTagId("LightweightForward"));
            tagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            filteringSettings = new FilteringSettings(RenderQueueRange.transparent, LayerMask.NameToLayer("Everything"));

            material = CoreUtils.CreateEngineMaterial(setting.shader);

        }

        public void Setup(RenderTargetIdentifier src)
        {
            this.src = src;
        }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            overdrawWatcherTexture = Shader.PropertyToID("_OverdrawWatcherTexture");
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            
            desc.depthBufferBits = 0;
            //desc.enableRandomWrite = true;
            //desc.width = 1024;
            //desc.height = 1024;
            cmd.GetTemporaryRT(overdrawWatcherTexture, desc);

            ConfigureClear(ClearFlag.All, Color.black);
            ConfigureTarget(overdrawWatcherTexture);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("OverDraw Watcher");

            var sortFlags = SortingCriteria.CommonTransparent;
            var drawSettings = CreateDrawingSettings(tagIdList, ref renderingData, sortFlags);
            drawSettings.overrideMaterial = material;
            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

            cmd.Blit(overdrawWatcherTexture, src);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(overdrawWatcherTexture);
        }
    }

    OverdrawWatcherPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new OverdrawWatcherPass(overdrawWatcherSetting);

        m_ScriptablePass.renderPassEvent = overdrawWatcherSetting.passEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!renderingData.cameraData.isSceneViewCamera)
        {
            m_ScriptablePass.Setup(renderer.cameraColorTarget);
            renderer.EnqueuePass(m_ScriptablePass);
        }
        
    }
}


