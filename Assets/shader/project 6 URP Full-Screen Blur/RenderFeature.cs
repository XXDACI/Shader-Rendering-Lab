using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class RenderFeature : ScriptableRendererFeature
{
    /// <summary>
    /// 为什么要写 new RenderFeatureSettings()？
    /// 因为如果没有创建 Settings 对象，settings 有可能是 null，后面访问： settings.computeShader
    /// 就可能出现空引用错误。
    /// </summary>
    [SerializeField] RenderFeatureSettings settings = new RenderFeatureSettings();
    RenderFeaturePass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new RenderFeaturePass(settings);

        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;

    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.

    /// <summary>
    /// 
    /// render: 渲染任务管理器
    /// renderingData: 它包含当前相机和本帧的一些信息
    /// 
    /// </summary>
    /// <param name="renderer"></param>
    /// <param name="renderingData"></param>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.computeShader == null)
            return;
        if (!SystemInfo.supportsComputeShaders)
            return;

        renderer.EnqueuePass(m_ScriptablePass);
    }

    // 面板功能参数区域
    [Serializable]
    public class RenderFeatureSettings
    {
        /// <summary>
        /// RenderPassEvent.AfterRenderingTransparents
        /// def: 场景中的不透明物体和透明物体都画完后，再模糊整个画面。
        /// </summary>
        [Tooltip("模糊在渲染流程中的哪个阶段执行")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("传入的shader")]
        public ComputeShader computeShader;

        [Range(1, 10)]
        [Tooltip("执行次数")]
        public int passes = 4;


        [Range(1, 10)]
        [Tooltip("每次模糊采样距离")]
        public float offset = 1f;

        [Range(1, 10)]
        [Tooltip("临时纹理缩小等级，1 表示宽高减半")]
        public int downsample = 1;


    }

    class RenderFeaturePass : ScriptableRenderPass
    {
        readonly RenderFeatureSettings settings;

        static readonly int s_InputTextureId = Shader.PropertyToID("_InputTexture");

        static readonly int s_OutputTextureId = Shader.PropertyToID("_OutputTexture");

        static readonly int s_OffsetId = Shader.PropertyToID("_Offset");

        const int k_GroupSize = 8;

        public RenderFeaturePass(RenderFeatureSettings settings)
        {
            this.settings = settings;

            requiresIntermediateTexture = true;
        }

        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        /// <summary>
        /// 每个shader pass 在执行时所需的数据
        /// </summary>
        private class PassData
        {
            // 主要：===================================================
            // 要执行的 Compute Shader
            public ComputeShader pd_computeShader;

            // Compute Shader 中要执行的 Kernel
            public int pd_kernel;

            // 输入纹理
            public TextureHandle pd_sourceTexture;

            // 输出纹理
            public TextureHandle pd_outTexture;

            // 补充shader所需数据：======================================
            public int pd_width;
            public int pd_height;

            public float pd_offset;
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        /// <summary>
        /// pass 执行位置
        /// </summary>
        /// <param name="data"></param>
        /// <param name="context"></param>
        static void ExecutePass(PassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;

            cmd.SetComputeTextureParam(
                data.pd_computeShader,
                data.pd_kernel,
                s_InputTextureId,
                data.pd_sourceTexture);

            cmd.SetComputeTextureParam(
                data.pd_computeShader,
                data.pd_kernel,
                s_OutputTextureId,
                data.pd_outTexture);

            cmd.SetComputeFloatParam(
                data.pd_computeShader,
                s_OffsetId,
                data.pd_offset);

            int groupX = Mathf.Max(1, Mathf.CeilToInt(data.pd_width / (float)k_GroupSize));
            int groupY = Mathf.Max(1, Mathf.CeilToInt(data.pd_height / (float)k_GroupSize));

            cmd.DispatchCompute(
                data.pd_computeShader,
                data.pd_kernel, 
                groupX, 
                groupY, 
                1);
        }

        /// <summary>
        /// 向 Render Graph 注册一次 Compute Shader Pass。
        /// </summary>
        void AddComputePass(
            RenderGraph renderGraph,
            TextureHandle sourceTexture,
            TextureHandle outTexture,
            int width,
            int height,
            float offset,
            string passName)
        {
            using (var builder = renderGraph.AddComputePass<PassData>(passName, out var passData))
            {
                passData.pd_computeShader = settings.computeShader;
                passData.pd_kernel = settings.computeShader.FindKernel("Kawase");
                passData.pd_sourceTexture = sourceTexture;
                passData.pd_outTexture = outTexture;
                passData.pd_width = width;
                passData.pd_height = height;
                passData.pd_offset = offset;

                builder.UseTexture(sourceTexture, AccessFlags.Read);
                builder.UseTexture(outTexture, AccessFlags.Write);

                builder.SetRenderFunc(
                    (PassData data, ComputeGraphContext context) => ExecutePass(data, context));
            }
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(
    RenderGraph renderGraph,
    ContextContainer frameData)
        {
            if (settings.computeShader == null ||
                settings.passes < 1)
            {
                return;
            }

            // 获取当前帧的 URP 资源和相机数据
            UniversalResourceData resourceData =
                frameData.Get<UniversalResourceData>();

            UniversalCameraData cameraData =
                frameData.Get<UniversalCameraData>();

            // 相机当前的颜色画面
            TextureHandle sourceTexture =
                resourceData.activeColorTexture;

            // 全分辨率纹理配置
            RenderTextureDescriptor fullDescriptor =
                cameraData.cameraTargetDescriptor;

            // Compute Shader 输出纹理不需要深度和 MSAA
            fullDescriptor.depthBufferBits = 0;
            fullDescriptor.msaaSamples = 1;
            fullDescriptor.enableRandomWrite = true;

            int fullWidth = fullDescriptor.width;
            int fullHeight = fullDescriptor.height;

            // 复制一份，用来创建低分辨率模糊纹理
            RenderTextureDescriptor blurDescriptor =
                fullDescriptor;

            int ds = Mathf.Max(
                0,
                settings.downsample);

            blurDescriptor.width = Mathf.Max(
                1,
                blurDescriptor.width >> ds);

            blurDescriptor.height = Mathf.Max(
                1,
                blurDescriptor.height >> ds);

            // 创建 ping-pong 临时纹理
            TextureHandle pingpongA =
                UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    blurDescriptor,
                    "_KawaseBlurA",
                    false,
                    FilterMode.Bilinear);

            TextureHandle pingpongB =
                UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    blurDescriptor,
                    "_KawaseBlurB",
                    false,
                    FilterMode.Bilinear);

            // 从相机画面开始
            TextureHandle fromTexture =
                sourceTexture;

            // 记录多个 Kawase Compute Pass
            for (int i = 0; i < settings.passes; i++)
            {
                TextureHandle toTexture =
                    (i % 2 == 0)
                        ? pingpongA
                        : pingpongB;

                // 保留 Project 5 的 pass + 0.5 逻辑
                float passOffset =
                    settings.offset * (i + 0.5f);

                AddComputePass(
                    renderGraph,
                    fromTexture,
                    toTexture,
                    blurDescriptor.width,
                    blurDescriptor.height,
                    passOffset,
                    $"KawaseBlur[{i}]");

                fromTexture = toTexture;
            }

            // 创建一张与相机大小相同的 Compute 可写纹理
            TextureHandle fullResult =
                UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    fullDescriptor,
                    "_KawaseBlurFullResult",
                    false,
                    FilterMode.Bilinear);

            // 将低分辨率模糊结果放大到相机分辨率
            AddComputePass(
                renderGraph,
                fromTexture,
                fullResult,
                fullWidth,
                fullHeight,
                0f,
                "KawaseBlur Upsample");

            // 将最终结果复制回相机颜色
            renderGraph.AddCopyPass(
                fullResult,
                sourceTexture,
                "KawaseBlur Copy Back");
        }
    }
}
