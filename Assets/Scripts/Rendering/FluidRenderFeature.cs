using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FluidSimulation.Core;
using FluidSimulation.Sorting;

namespace FluidSimulation.Rendering
{
    /// <summary>
    /// URP ScriptableRendererFeature — 流体渲染管线入口
    /// 管理所有渲染参数、创建DensityFieldPass并注入URP渲染流程
    /// </summary>
    public class FluidRenderFeature : ScriptableRendererFeature
    {
        #region 枚举

        internal enum ComputeBufferType
        {
            Physics,
            Graphics
        }

        #endregion

        #region 序列化资源引用

        public ComputeShader fluidPhysicsCS;
        public ComputeShader bitonicSortCS;
        public Shader drawParticlesShader;
        public Shader drawGridDensityShader;
        public Shader vizDensityShader;
        public Shader drawGridPressureShader;

        #endregion

        #region 静态参数 (由FluidSimulatorController通过OnValidate写入)

        // 粒子参数
        private static Color _pColor;
        private static float _pPixel;
        private static float _pRadius;
        private static float _gravity;
        private static float _energyDamping;

        // 密度场参数
        private static float _dRadius;

        // 可视化参数
        private static Color _underTargetCol;
        private static Color _overTargetCol;
        private static Color _aroundTargetCol;
        private static float _targetValue;
        private static float _pressureMultiplier;

        // 光标位置
        private static Vector3 _cursorPos;

        #endregion

        #region 静态开关

        internal static bool EnableUpdate;
        internal static bool DrawGridDensityField;
        internal static bool DrawGridPressureField;
        internal static bool DrawVizDensityMap;
        internal static bool DrawParticles;
        internal static bool ComputeShaderDebug;

        public static bool IsPassCreated { get; private set; }
        private static bool _isWriting;

        #endregion

        #region 参数设置接口

        internal static void SetParticleParams(float radius, Color color, float gravity, float energyDamping, float pixel)
        {
            _pPixel = pixel;
            _pColor = color;
            _pRadius = radius;
            _gravity = gravity;
            _energyDamping = energyDamping;
        }

        internal static void SetDensityRadius(float radius) => _dRadius = radius;

        internal static void SetVizDensityParams(Color over, Color under, Color around, float target)
        {
            _overTargetCol = over;
            _underTargetCol = under;
            _aroundTargetCol = around;
            _targetValue = target;
        }

        internal static void SetPressureParams(float target, float multiplier)
        {
            _targetValue = target;
            _pressureMultiplier = multiplier;
        }

        #endregion

        #region Pass实例

        private DensityFieldPass m_DensityFieldPass;

        #endregion

        #region DensityFieldPass — 核心渲染Pass

        public class DensityFieldPass : ScriptableRenderPass, IDisposable
        {
            private bool m_Initialized;

            // Compute Shaders
            private static ComputeShader _fluidPhysicsCS;
            private static ComputeShader _bitonicSortCS;

            // Materials
            private static Material _particleMat;
            private static Material _gridDensityMat;
            private static Material _gridPressureMat;
            private static Material _vizDensityMat;

            // RTHandles
            private static RTHandle m_DebugPressureRT;
            private static RTHandle m_DebugRT;
            private static RTHandle m_DensityRT;
            private static RTHandle m_VizDensityRT;
            private static RTHandle m_GradientRT;
            private static RTHandle m_PressureRT;
            private static RenderTextureDescriptor _rtDescriptor;

            // GPU Buffers
            private static ComputeBuffer _initBuffer;
            private static ComputeBuffer _graphicsBuffer;
            private static ComputeBuffer _physicsBuffer;
            private static ComputeBuffer _argsBuffer;
            private static ComputeBuffer _gridBuffer;
            private static ComputeBuffer _gridSortedBuffer;
            private static ComputeBuffer _gridSortedTempBuffer;

            // Kernel IDs
            private static int _mainKernel;
            private static int _initKernel;
            private static int _buildGridKernel;
            private static int _sortGridKernel;
            private static int _densityKernel;
            private static int _pressureKernel;
            private static int _updateKernel;

            // 网格索引缓存
            private int2[] _gridIndexCache;
            private int[] _gridCellCache;

            // Mesh & Args
            private static Mesh _quadMesh;
            private static readonly uint[] _args = new uint[5];

            // 时间
            private float m_DeltaTime;

            // Camera target
            private RTHandle m_CameraColorTarget;

            public DensityFieldPass(
                ComputeShader physicsCS, ComputeShader bitonicCS,
                Shader particleShader, Shader gridDensityShader,
                Shader vizDensityShader, Shader gridPressureShader)
            {
                _fluidPhysicsCS = physicsCS;
                _bitonicSortCS = bitonicCS;

                // 创建材质
                _particleMat = new Material(particleShader);
                _gridDensityMat = new Material(gridDensityShader);
                _vizDensityMat = new Material(vizDensityShader);
                _gridPressureMat = new Material(gridPressureShader);

                // 创建GPU缓冲区
                InitializeBuffers();

                // 绑定Compute Shader Kernels
                InitializeKernels();

                // 初始化Quad网格和间接绘制参数
                InitializeMeshAndArgs();

                _rtDescriptor = new RenderTextureDescriptor();
                m_Initialized = true;
            }

            #region 初始化

            private void InitializeBuffers()
            {
                _initBuffer?.Release();
                _graphicsBuffer?.Release();
                _physicsBuffer?.Release();
                _argsBuffer?.Release();

                int particleStride = Marshal.SizeOf(typeof(FluidParticlePhysics));
                int graphicsStride = Marshal.SizeOf(typeof(FluidParticleGraphics));

                _initBuffer = new ComputeBuffer(FluidConstants.MAX_PARTICLE_COUNT, particleStride, UnityEngine.ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
                _physicsBuffer = new ComputeBuffer(FluidConstants.MAX_PARTICLE_COUNT, particleStride, UnityEngine.ComputeBufferType.Default);
                _graphicsBuffer = new ComputeBuffer(FluidConstants.MAX_PARTICLE_COUNT, graphicsStride, UnityEngine.ComputeBufferType.Default);
                _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), UnityEngine.ComputeBufferType.IndirectArguments, ComputeBufferMode.SubUpdates);

                // 空间哈希网格缓冲区
                _gridBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(int)), UnityEngine.ComputeBufferType.Default);
                _gridSortedBuffer = new ComputeBuffer(FluidConstants.MAX_PARTICLE_COUNT, Marshal.SizeOf(typeof(int2)), UnityEngine.ComputeBufferType.Default);
                _gridSortedTempBuffer = new ComputeBuffer(FluidConstants.MAX_PARTICLE_COUNT, Marshal.SizeOf(typeof(int2)), UnityEngine.ComputeBufferType.Default);

                // 初始化网格排序缓冲区为最大值
                _gridIndexCache = new int2[FluidConstants.MAX_PARTICLE_COUNT];
                for (int i = 0; i < _gridIndexCache.Length; i++)
                    _gridIndexCache[i] = int.MaxValue;
                _gridSortedBuffer.SetData(_gridIndexCache);
                _gridSortedTempBuffer.SetData(_gridIndexCache);
            }

            private void InitializeKernels()
            {
                // 主渲染kernel (基于纹理采样的旧路径)
                _mainKernel = _fluidPhysicsCS.FindKernel("FluidSimulationGraphCS");
                _fluidPhysicsCS.SetBuffer(_mainKernel, "_FluidParticlePhysics", _physicsBuffer);

                // 初始化kernel — 从CPU staging buffer拷贝到GPU physics buffer
                _initKernel = _fluidPhysicsCS.FindKernel("InitCS");
                _fluidPhysicsCS.SetBuffer(_initKernel, "_FluidParticleInit", _initBuffer);
                _fluidPhysicsCS.SetBuffer(_initKernel, "_FluidParticlePhysics", _physicsBuffer);

                // SPH核心计算kernels
                _densityKernel = _fluidPhysicsCS.FindKernel("FluidSimulationDensityCS");
                _pressureKernel = _fluidPhysicsCS.FindKernel("FluidSimulationPressureCS");
                _updateKernel = _fluidPhysicsCS.FindKernel("FluidSimulationCS");
                _fluidPhysicsCS.SetBuffer(_densityKernel, "_FluidParticlePhysics", _physicsBuffer);
                _fluidPhysicsCS.SetBuffer(_pressureKernel, "_FluidParticlePhysics", _physicsBuffer);
                _fluidPhysicsCS.SetBuffer(_updateKernel, "_FluidParticlePhysics", _physicsBuffer);

                // 空间哈希网格构建
                _buildGridKernel = _fluidPhysicsCS.FindKernel("FluidBuildGridCS");
                _fluidPhysicsCS.SetBuffer(_buildGridKernel, "_FluidParticlePhysics", _physicsBuffer);
                _fluidPhysicsCS.SetBuffer(_buildGridKernel, "_FluidParticleGridSorted", _gridSortedBuffer);
                _fluidPhysicsCS.SetBuffer(_buildGridKernel, "_FluidParticleGridSortedTemp", _gridSortedTempBuffer);

                // 网格排序后索引构建
                _sortGridKernel = _fluidPhysicsCS.FindKernel("SortGridCS");
                _fluidPhysicsCS.SetBuffer(_sortGridKernel, "_FluidParticleGrid", _gridBuffer);
                _fluidPhysicsCS.SetBuffer(_sortGridKernel, "_FluidParticleGridSorted", _gridSortedBuffer);

                // 材质绑定缓冲区
                _gridDensityMat.SetBuffer("_FluidParticleGrid", _gridBuffer);
                _gridDensityMat.SetBuffer("_FluidParticleGridSorted", _gridSortedBuffer);
                _gridPressureMat.SetBuffer("_FluidParticleGrid", _gridBuffer);
                _gridPressureMat.SetBuffer("_FluidParticleGridSorted", _gridSortedBuffer);

                _particleMat.SetBuffer("_ComputeBuffer", _physicsBuffer);
                _vizDensityMat.SetBuffer("_ComputeBuffer", _physicsBuffer);
                _gridDensityMat.SetBuffer("_ComputeBuffer", _physicsBuffer);
                _gridPressureMat.SetBuffer("_ComputeBuffer", _physicsBuffer);
            }

            private void InitializeMeshAndArgs()
            {
                _quadMesh = MeshUtility.CreateQuad(1, 1);
                _args[0] = _quadMesh.GetIndexCount(0);
                _args[1] = (uint)FluidState.ParticleCount;
                _args[2] = _quadMesh.GetIndexStart(0);
                _args[3] = _quadMesh.GetBaseVertex(0);
                _argsBuffer.SetData(_args);
            }

            #endregion

            #region CPU→GPU数据传输

            internal static NativeArray<T> BeginWriteBuffer<T>(ComputeBufferType type, int startIndex, int count) where T : struct
            {
                _isWriting = true;
                switch (type)
                {
                    case ComputeBufferType.Graphics:
                        return (NativeArray<T>)Convert.ChangeType(
                            _graphicsBuffer.BeginWrite<FluidParticleGraphics>(startIndex, count), typeof(NativeArray<T>));
                    case ComputeBufferType.Physics:
                        return (NativeArray<T>)Convert.ChangeType(
                            _initBuffer.BeginWrite<FluidParticlePhysics>(startIndex, count), typeof(NativeArray<T>));
                    default:
                        return default;
                }
            }

            internal static void EndWriteBuffer(ComputeBufferType type, int count)
            {
                _isWriting = false;
                switch (type)
                {
                    case ComputeBufferType.Graphics:
                        _graphicsBuffer.EndWrite<FluidParticleGraphics>(count);
                        break;
                    case ComputeBufferType.Physics:
                        _initBuffer.EndWrite<FluidParticlePhysics>(count);
                        break;
                }
            }

            #endregion

            #region 粒子计数同步

            internal static void SyncParticleCount(int prevCount, int currCount)
            {
                if (_argsBuffer == null || !_argsBuffer.IsValid()) return;

                // 更新间接绘制参数
                var arg1 = _argsBuffer.BeginWrite<uint>(1, 1);
                arg1[0] = (uint)currCount;
                _argsBuffer.EndWrite<int>(1);

                // 通知Compute Shader
                _fluidPhysicsCS.SetInt("_PrevFluidParticleCount", prevCount);
                _fluidPhysicsCS.SetInt("_CurrFluidParticleCount", currCount);

                if (currCount == 0) return;

                // 执行InitCS — 将staging buffer数据拷贝到physics buffer
                int threadGroups = Mathf.CeilToInt(currCount / (float)FluidConstants.THREAD_GROUP_SIZE);
                _fluidPhysicsCS.SetBuffer(_initKernel, "_FluidParticleInit", _initBuffer);
                _fluidPhysicsCS.SetBuffer(_initKernel, "_FluidParticlePhysics", _physicsBuffer);
                _fluidPhysicsCS.Dispatch(_initKernel, threadGroups, 1, 1);
            }

            internal static void SetCursorPosition(Vector3 pos) => _cursorPos = pos;

            #endregion

            #region 空间哈希网格管理

            private void ResizeGridBufferIfNeeded(Vector4 texelSize, float smoothRadius)
            {
                if (smoothRadius < 1e-2f) smoothRadius = 1e-2f;

                int yCount = Mathf.Clamp(Mathf.FloorToInt(1f / smoothRadius), 1, FluidConstants.MAX_GRID_COUNT);
                int xCount = Mathf.Clamp(Mathf.FloorToInt((texelSize.x / texelSize.y) / smoothRadius), 1, FluidConstants.MAX_GRID_COUNT);
                int totalCount = xCount * yCount;

                if (totalCount != _gridBuffer.count)
                {
                    _gridCellCache = new int[totalCount];
                    _gridBuffer.Dispose();
                    _gridBuffer = new ComputeBuffer(totalCount, Marshal.SizeOf(typeof(int)), UnityEngine.ComputeBufferType.Default);
                }

                for (int i = 0; i < _gridCellCache.Length; i++)
                    _gridCellCache[i] = int.MaxValue;
            }

            #endregion

            #region 渲染Pass生命周期

            public void SetTarget(RTHandle colorHandle) => m_CameraColorTarget = colorHandle;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (m_CameraColorTarget != null)
                    ConfigureTarget(m_CameraColorTarget);
            }

            public void CreateRT(in RenderingData renderingData)
            {
                AllocateRT("RTDebug", RenderTextureFormat.RGFloat, ref _rtDescriptor, in renderingData, ref m_DebugRT);
                AllocateRT("RTDebugPressure", RenderTextureFormat.ARGBFloat, ref _rtDescriptor, in renderingData, ref m_DebugPressureRT);
                AllocateRT("RTDensity", RenderTextureFormat.RFloat, ref _rtDescriptor, in renderingData, ref m_DensityRT);
                AllocateRT("VizDensityRT", RenderTextureFormat.ARGBFloat, ref _rtDescriptor, in renderingData, ref m_VizDensityRT);
                AllocateRT("RTGradient", RenderTextureFormat.ARGBFloat, ref _rtDescriptor, in renderingData, ref m_GradientRT);
                AllocateRT("RTPressure", RenderTextureFormat.ARGBFloat, ref _rtDescriptor, in renderingData, ref m_PressureRT);
            }

            #endregion

            #region Execute — 每帧渲染核心逻辑

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!m_Initialized || FluidState.ParticleCount == 0) return;
                if (renderingData.cameraData.cameraType != CameraType.Game) return;

                CommandBuffer cmd = CommandBufferPool.Get("FluidSimulation");

                Vector4 textureSize = Vector4.zero;
                if (m_DensityRT.rt != null)
                    textureSize = new Vector4(m_DensityRT.rt.width, m_DensityRT.rt.height, 0, 0);

                // 注意: 原始行为是m_DeltaTime一旦设为0.005就不会归零
                // 即使关闭enableUpdate，密度/压力可视化仍使用预测位置
                if (EnableUpdate)
                    m_DeltaTime = FluidConstants.FIXED_DELTA_TIME;

                // ── 全局参数 ──
                SetGlobalParams(cmd, textureSize);
                SetMaterialParams(textureSize);

                int particleCount = FluidState.ParticleCount;
                int threadGroups = Mathf.CeilToInt(particleCount / (float)FluidConstants.THREAD_GROUP_SIZE);

                // ── 阶段1: 空间哈希排序 ──
                ExecuteSpatialSort(cmd, textureSize, threadGroups, particleCount);

                // ── 阶段2: 可视化渲染 (可选) ──
                if (DrawGridDensityField)
                    ExecuteGridDensityVisualization(cmd, in renderingData);
                if (DrawVizDensityMap)
                    ExecuteVizDensityMap(cmd, in renderingData);
                if (DrawGridPressureField)
                    ExecuteGridPressureVisualization(cmd, in renderingData);

                // ── 阶段3: SPH物理计算 (密度→压力→位置更新) ──
                ExecuteSPHCompute(cmd, threadGroups);

                // ── 阶段4: 调试输出 ──
                if (ComputeShaderDebug)
                {
                    cmd.SetRenderTarget(m_CameraColorTarget);
                    Blitter.BlitCameraTexture(cmd, m_DebugPressureRT, m_CameraColorTarget, 0);
                }

                // ── 阶段5: 粒子绘制 ──
                if (DrawParticles)
                {
                    cmd.SetRenderTarget(m_CameraColorTarget);
                    cmd.DrawMeshInstancedIndirect(_quadMesh, 0, _particleMat, 0, _argsBuffer);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            #endregion

            #region Execute子阶段

            private void SetGlobalParams(CommandBuffer cmd, Vector4 textureSize)
            {
                cmd.SetGlobalFloat("_FluidDeltaTime", m_DeltaTime);
                cmd.SetGlobalFloat("_SmoothRadius", _dRadius);
                cmd.SetGlobalVector("_TexelSize", textureSize);
                cmd.SetGlobalInt("_FluidParticleCount", FluidState.ParticleCount);
            }

            private void SetMaterialParams(Vector4 textureSize)
            {
                _particleMat.SetColor("_ParticleColor", _pColor);
                _particleMat.SetFloat("_Pixel", _pPixel);
                _particleMat.SetFloat("_ParticleRadius", _pRadius);

                _gridDensityMat.SetVector("_CursorPosition", _cursorPos);
                _gridDensityMat.SetFloat("_SmoothRadius", _dRadius);
                _gridDensityMat.SetVector("_TexelSize", textureSize);

                _vizDensityMat.SetFloat("_SmoothRadius", _dRadius);
                _vizDensityMat.SetColor("_UnderTargetColor", _underTargetCol);
                _vizDensityMat.SetColor("_OverTargetColor", _overTargetCol);
                _vizDensityMat.SetColor("_AroundTargetColor", _aroundTargetCol);
                _vizDensityMat.SetFloat("_TargetValue", _targetValue);

                _gridPressureMat.SetFloat("_SmoothRadius", _dRadius);
                _gridPressureMat.SetFloat("_TargetValue", _targetValue);
                _gridPressureMat.SetFloat("_PressureMultiplier", _pressureMultiplier);
                _gridPressureMat.SetFloat("_Pixel", _pPixel);
            }

            private void ExecuteSpatialSort(CommandBuffer cmd, Vector4 textureSize, int threadGroups, int particleCount)
            {
                cmd.BeginSample("Fluid Sort");

                ResizeGridBufferIfNeeded(textureSize, _dRadius);

                cmd.SetComputeBufferParam(_fluidPhysicsCS, _sortGridKernel, "_FluidParticleGrid", _gridBuffer);
                cmd.SetBufferData(_gridBuffer, _gridCellCache);
                cmd.SetBufferData(_gridSortedBuffer, _gridIndexCache);
                cmd.SetBufferData(_gridSortedTempBuffer, _gridIndexCache);

                // 构建网格
                cmd.SetComputeBufferParam(_fluidPhysicsCS, _buildGridKernel, "_FluidParticleGridSorted", _gridSortedBuffer);
                cmd.SetComputeBufferParam(_fluidPhysicsCS, _buildGridKernel, "_FluidParticleGridSortedTemp", _gridSortedBuffer);
                cmd.DispatchCompute(_fluidPhysicsCS, _buildGridKernel, threadGroups, 1, 1);

                // Bitonic排序
                BitonicSort.GPUSort(cmd, FluidConstants.MAX_PARTICLE_COUNT, _bitonicSortCS, _gridSortedBuffer, _gridSortedTempBuffer);

                // 构建网格起始索引
                cmd.DispatchCompute(_fluidPhysicsCS, _sortGridKernel, threadGroups, 1, 1);

                // 更新材质缓冲区引用
                _gridDensityMat.SetBuffer("_FluidParticleGrid", _gridBuffer);
                _gridDensityMat.SetBuffer("_FluidParticleGridSorted", _gridSortedBuffer);
                _gridPressureMat.SetBuffer("_FluidParticleGrid", _gridBuffer);
                _gridPressureMat.SetBuffer("_FluidParticleGridSorted", _gridSortedBuffer);

                cmd.EndSample("Fluid Sort");
            }

            private void ExecuteGridDensityVisualization(CommandBuffer cmd, in RenderingData renderingData)
            {
                cmd.BeginSample("Fluid Grid Density");
                AllocateRT("RTDensity", RenderTextureFormat.RFloat, ref _rtDescriptor, in renderingData, ref m_DensityRT);
                cmd.SetRenderTarget(m_DensityRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.Blit(null, m_DensityRT, _gridDensityMat, 0);
                cmd.SetGlobalTexture("_FluidDensity", m_DensityRT);
                Blitter.BlitCameraTexture(cmd, m_DensityRT, m_CameraColorTarget, 0);
                cmd.EndSample("Fluid Grid Density");
            }

            private void ExecuteVizDensityMap(CommandBuffer cmd, in RenderingData renderingData)
            {
                cmd.BeginSample("Fluid VizDensity");
                AllocateRT("VizDensityRT", RenderTextureFormat.ARGBFloat, ref _rtDescriptor, in renderingData, ref m_VizDensityRT);
                cmd.SetRenderTarget(m_VizDensityRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.Blit(m_DensityRT, m_VizDensityRT, _vizDensityMat, 0);
                Blitter.BlitCameraTexture(cmd, m_VizDensityRT, m_CameraColorTarget, 0);
                cmd.EndSample("Fluid VizDensity");
            }

            private void ExecuteGridPressureVisualization(CommandBuffer cmd, in RenderingData renderingData)
            {
                cmd.BeginSample("Fluid Grid Pressure");
                AllocateRT("RTPressure", RenderTextureFormat.ARGBFloat, ref _rtDescriptor, in renderingData, ref m_PressureRT);
                cmd.SetRenderTarget(m_PressureRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.Blit(null, m_PressureRT, _gridPressureMat, 0);
                Blitter.BlitCameraTexture(cmd, m_PressureRT, m_CameraColorTarget, 0);
                cmd.EndSample("Fluid Grid Pressure");
            }

            private void ExecuteSPHCompute(CommandBuffer cmd, int threadGroups)
            {
                cmd.BeginSample("Fluid SPH Compute");

                // 设置Compute Shader参数
                cmd.SetComputeFloatParam(_fluidPhysicsCS, "_EnergyDumping", _energyDamping);
                cmd.SetComputeFloatParam(_fluidPhysicsCS, "_ParticleSize", _pRadius);
                cmd.SetComputeFloatParam(_fluidPhysicsCS, "_Deltatime", m_DeltaTime);
                cmd.SetComputeFloatParam(_fluidPhysicsCS, "_Gravity", _gravity);
                cmd.SetComputeFloatParam(_fluidPhysicsCS, "_TargetValue", _targetValue);
                cmd.SetComputeFloatParam(_fluidPhysicsCS, "_PressureMultiplier", _pressureMultiplier);
                cmd.SetComputeVectorParam(_fluidPhysicsCS, "_TexelSize",
                    new Vector4(m_DensityRT.rt.width, m_DensityRT.rt.height, 0, 0));

                // 绑定网格缓冲区
                cmd.SetComputeBufferParam(_fluidPhysicsCS, _densityKernel, "_FluidParticleGrid", _gridBuffer);
                cmd.SetComputeBufferParam(_fluidPhysicsCS, _densityKernel, "_FluidParticleGridSorted", _gridSortedBuffer);
                cmd.SetComputeBufferParam(_fluidPhysicsCS, _pressureKernel, "_FluidParticleGrid", _gridBuffer);
                cmd.SetComputeBufferParam(_fluidPhysicsCS, _pressureKernel, "_FluidParticleGridSorted", _gridSortedBuffer);

                // 密度计算
                cmd.BeginSample("SPH Density");
                cmd.DispatchCompute(_fluidPhysicsCS, _densityKernel, threadGroups, 1, 1);
                cmd.EndSample("SPH Density");

                // 压力计算
                cmd.BeginSample("SPH Pressure");
                cmd.DispatchCompute(_fluidPhysicsCS, _pressureKernel, threadGroups, 1, 1);
                cmd.EndSample("SPH Pressure");

                // 位置/速度更新
                if (EnableUpdate)
                {
                    cmd.BeginSample("SPH Update");
                    cmd.SetComputeBufferParam(_fluidPhysicsCS, _updateKernel, "_FluidParticlePhysics", _physicsBuffer);
                    cmd.DispatchCompute(_fluidPhysicsCS, _updateKernel, threadGroups, 1, 1);
                    cmd.EndSample("SPH Update");
                }

                cmd.EndSample("Fluid SPH Compute");
            }

            #endregion

            #region 资源释放

            public void Dispose()
            {
                m_DensityRT?.Release();
                m_VizDensityRT?.Release();
                m_GradientRT?.Release();
                m_PressureRT?.Release();
                m_DebugRT?.Release();
                m_DebugPressureRT?.Release();

                _initBuffer?.Release();
                _graphicsBuffer?.Release();
                _physicsBuffer?.Release();
                _argsBuffer?.Release();
                _gridBuffer?.Release();
                _gridSortedBuffer?.Release();
                _gridSortedTempBuffer?.Release();

                m_Initialized = false;
            }

            #endregion
        }

        #endregion

        #region ScriptableRendererFeature生命周期

        public override void Create()
        {
            if (fluidPhysicsCS == null || bitonicSortCS == null ||
                drawParticlesShader == null || drawGridDensityShader == null ||
                vizDensityShader == null || drawGridPressureShader == null)
                return;

            m_DensityFieldPass = new DensityFieldPass(
                fluidPhysicsCS, bitonicSortCS,
                drawParticlesShader, drawGridDensityShader,
                vizDensityShader, drawGridPressureShader);
            IsPassCreated = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_isWriting || !IsPassCreated) return;
            renderer.EnqueuePass(m_DensityFieldPass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game || !IsPassCreated) return;

            m_DensityFieldPass.ConfigureInput(ScriptableRenderPassInput.Color);
            m_DensityFieldPass.SetTarget(renderer.cameraColorTargetHandle);
            m_DensityFieldPass.CreateRT(renderingData);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            m_DensityFieldPass?.Dispose();
            IsPassCreated = false;
        }

        #endregion

        #region RTHandle工具方法

        public static void AllocateRT(string name, RenderTextureFormat format,
            ref RenderTextureDescriptor desc, in RenderingData renderingData, ref RTHandle handle)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            var cam = renderingData.cameraData.camera;
            bool needsResize = desc.width != cam.pixelWidth || desc.height != cam.pixelHeight || handle == null;

            if (needsResize)
            {
                desc = new RenderTextureDescriptor
                {
                    width = cam.pixelWidth,
                    height = cam.pixelHeight,
                    dimension = TextureDimension.Tex2D,
                    volumeDepth = 1,
                    useMipMap = false,
                    colorFormat = format,
                    msaaSamples = 1,
                    enableRandomWrite = true
                };
            }

            if (handle == null)
            {
                handle = RTHandles.Alloc(desc, FilterMode.Point, TextureWrapMode.Clamp, false, 1, 0, name);
            }
            else if (handle.rt != null && (handle.rt.width != desc.width || handle.rt.height != desc.height))
            {
                handle.Release();
                handle = RTHandles.Alloc(desc, FilterMode.Point, TextureWrapMode.Clamp, false, 1, 0, name);
            }
        }

        #endregion
    }
}
