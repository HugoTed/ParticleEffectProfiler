using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OverdrawRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public class OverdrawSetting
    {
        public ComputeShader overdrawCS = null;

        public bool overdrawCompute = false;

        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public OverdrawSetting overdrawSetting = new OverdrawSetting();
    class OverdrawPass : ScriptableRenderPass
    {
        ComputeShader m_ComputeShader;
        bool m_Compute = false;
        RenderTargetIdentifier src,m_Rt;

        private const int dataSize = 128 * 128;
        private int[] inputData = new int[dataSize];
        private int[] resultData = new int[dataSize];
        private ComputeBuffer resultBuffer;
        private Shader replacementShader;

        //int overdrawTexture;

        RenderTexture overdrawTexture;

        // ========= Results ========
        // Last measurement
        /// <summary> The number of shaded fragments in the last frame. </summary>
        public long TotalShadedFragments { get; private set; }
        /// <summary> The overdraw ration in the last frame. </summary>
        public float OverdrawRatio { get; private set; }

        // Sampled measurement
        /// <summary> Number of shaded fragments in the measured time span. </summary>
        public long IntervalShadedFragments { get; private set; }
        /// <summary> The average number of shaded fragments in the measured time span. </summary>
        public float IntervalAverageShadedFragments { get; private set; }
        /// <summary> The average overdraw in the measured time span. </summary>
        public float IntervalAverageOverdraw { get; private set; }
        public float AccumulatedAverageOverdraw { get { return accumulatedIntervalOverdraw / intervalFrames; } }

        private long accumulatedIntervalFragments;
        private float accumulatedIntervalOverdraw;
        private long intervalFrames;

        public OverdrawPass(OverdrawSetting setting)
        {
            m_ComputeShader = setting.overdrawCS;
            m_Compute = setting.overdrawCompute;
            RecreateComputeBuffer();
            for (int i = 0; i < inputData.Length; i++) inputData[i] = 0;
        }

        public void Setup(RenderTargetIdentifier source)
        {
            src = source;
        }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        private void RecreateComputeBuffer()
        {
            if (resultBuffer != null) return;
            resultBuffer = new ComputeBuffer(resultData.Length, 4);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            if (m_Compute && m_ComputeShader != null)
            {
                CommandBuffer cmd = CommandBufferPool.Get("Check OverDraw");
                //overdrawTexture = Shader.PropertyToID("_OverdrawTexture");
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.colorFormat = RenderTextureFormat.RFloat;
                desc.enableRandomWrite = true;

                overdrawTexture = RenderTexture.GetTemporary(desc);
                //cmd.GetTemporaryRT(overdrawTexture, desc);

                cmd.Blit(src,overdrawTexture);

                int kernel = m_ComputeShader.FindKernel("CSMain");

                RecreateComputeBuffer();

                // Setting up the data
                resultBuffer.SetData(inputData);
                m_ComputeShader.SetTexture(kernel, "Overdraw", overdrawTexture);
                m_ComputeShader.SetBuffer(kernel, "Output", resultBuffer);

                //cmd.SetComputeBufferData(resultBuffer, inputData);
                //cmd.SetComputeTextureParam(m_ComputeShader, kernel, "Overdraw", overdrawTexture);
                //cmd.SetComputeBufferParam(m_ComputeShader, kernel, "Output", resultBuffer);

                int xGroups = (desc.width / 32);
                int yGroups = (desc.height / 32);

                // Summing up the fragments
                m_ComputeShader.Dispatch(kernel, xGroups, yGroups, 1);
                //cmd.DispatchCompute(m_ComputeShader, kernel, xGroups, yGroups, 1);
                resultBuffer.GetData(resultData);

                // Getting the results
                TotalShadedFragments = 0;
                for (int i = 0; i < resultData.Length; i++)
                {
                    TotalShadedFragments += resultData[i];
                }

                OverdrawRatio = (float)TotalShadedFragments / (xGroups * 32 * yGroups * 32);

                Debug.Log(OverdrawRatio);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            //resultBuffer?.Release();
            //cmd.ReleaseTemporaryRT(overdrawTexture);
            RenderTexture.ReleaseTemporary(overdrawTexture);
        }
    }

    OverdrawPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new OverdrawPass(overdrawSetting);

        m_ScriptablePass.renderPassEvent = overdrawSetting.passEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        m_ScriptablePass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(m_ScriptablePass);
    }
}


