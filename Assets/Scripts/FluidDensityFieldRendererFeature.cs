using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using static FluidSimulation.FluidUtilities;
using static FluidSimulation.Globals;

namespace FluidSimulation
{
    public class FluidDensityFieldRendererFeature : ScriptableRendererFeature
    {
        internal enum FluidComputeBufferType
        {
            Physics,
            Graphics
        }

        private static Vector3 _cursorPos;

        private static Color _pColor;
        private static float _pPixel;
        private static float _pRadius;
        private static float _gravity;
        private static float _energyDamping;
        private static float _dRadius;
        private static Color _underTargetCol;
        private static Color _overTargetCol;
        private static Color _aroundTargetCol;
        private static float _targetValue;
        private static float _pressureMultiplier;
        private static Mesh _mesh;

        private static RenderTexture _rt;

        private static int _densityKernel;
        private static int _pressureKernel;
        private static int _updateKernel;

        private static int _mainKernel;
        private static int _initKernel;
        private static int _buildGridKernel;
        private static int _bitonicKernel;
        private static int _sortGridKernel;

        private static readonly uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };
        
        private DensityFieldPass m_DensityFieldPass;

        internal static bool enableUpdate = false;
        internal static bool drawGridDensityField = false;
        internal static bool drawGridPressureField = false;
        internal static bool drawVizDensityMap = false;
        internal static bool drawParticles = false;
        internal static bool computeShaderDebug = false;
        private static bool _isWriting = false;
        public static bool passCreated = false;
        

        #region Helpers

        internal static void SetParticleParams(float radius, Color color, float gravity, float energyDamping, float pixel)
        {
            _pPixel = pixel;
            _pColor = color;
            _pRadius = radius;
            _gravity = gravity;
            _energyDamping = energyDamping;
        }
        internal static void SetDensityRadius(float radius)
        {
            _dRadius = radius;
        }
        
        internal static void SetVizDensityParams(Color overTarget, Color underTarget, Color aroundTarget, float targetValue)
        {
            _overTargetCol = overTarget;
            _underTargetCol = underTarget;
            _aroundTargetCol = aroundTarget;
            _targetValue = targetValue;
        }

        internal static void SetPressureParams(float targetValue, float pressureMultiplier)
        {
            _targetValue = targetValue;
            _pressureMultiplier = pressureMultiplier;
            
        }

        #endregion

        #region RenderFeature


        public class DensityFieldPass : ScriptableRenderPass, IDisposable
        {
            private bool m_PassInit = false;
            private static ComputeShader _fluidPhysicsCS;
            private static ComputeShader _bitonicSortCS;

            private static Material _particleMaterial;
            private static Material _gridDensityMaterial;
            private static Material _gridPressureMaterial;
            private static Material _vizDensityMaterial;
            
            private static RTHandle m_DebugPressureRTHandle;
            private static RTHandle m_DebugRTHandle;
            private static RTHandle m_RTHandleParticle;
            private static RTHandle m_RTHandleDensity;
            private static RTHandle m_RTHandleVizDensity;
            private static RTHandle m_RTHandleGradient;
            private static RTHandle m_RTHandlePressure;

            private static RenderTextureDescriptor _textureDescriptor;
            
            private static ComputeBuffer _particlesInitBuffer;
            private static ComputeBuffer _particlesGraphicsBuffer;
            private static ComputeBuffer _particlesPhysicsBuffer;
            private static ComputeBuffer _argsBuffer;

            private static ComputeBuffer _fluidParticleGridBuffer;
            private static ComputeBuffer _fluidParticleGridSortedBuffer;
            private static ComputeBuffer _fluidParticleGridSortedTempBuffer;
            
            private int2[] _particleGridIndex;
            private int[] _gidIndex;

            private float m_DeltaTime = 0;
            
            public DensityFieldPass(ComputeShader fluidPhysicsCs, ComputeShader bitonicSortCS,Shader drawParticlesShader, Shader drawGridDensityShader, Shader vizDensityShader, Shader drawGridPressureShader)
            {
                _fluidPhysicsCS = fluidPhysicsCs;
                _bitonicSortCS = bitonicSortCS;
                _particleMaterial = new Material(drawParticlesShader);
                _gridDensityMaterial = new Material(drawGridDensityShader);
                _vizDensityMaterial = new Material(vizDensityShader);
                _gridPressureMaterial = new Material(drawGridPressureShader);
                
                _particlesGraphicsBuffer?.Release();
                _particlesInitBuffer?.Release();
                _particlesPhysicsBuffer?.Release();
                _argsBuffer?.Release();
                
                _particlesInitBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticlePhysics)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
                _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments,ComputeBufferMode.SubUpdates);
                _particlesGraphicsBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticleGraphics)), ComputeBufferType.Default);
                _particlesPhysicsBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticlePhysics)), ComputeBufferType.Default);
                
                _fluidParticleGridBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(int)), ComputeBufferType.Default);
                _fluidParticleGridSortedBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(int2)), ComputeBufferType.Default);
                _fluidParticleGridSortedTempBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(int2)), ComputeBufferType.Default);

                
                _mainKernel = _fluidPhysicsCS.FindKernel("FluidSimulationGraphCS");
                _fluidPhysicsCS.SetBuffer(_mainKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                
                _initKernel = _fluidPhysicsCS.FindKernel("InitCS");
                _fluidPhysicsCS.SetBuffer(_initKernel,"_FluidParticleInit", _particlesInitBuffer);
                _fluidPhysicsCS.SetBuffer(_initKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                
                _densityKernel = _fluidPhysicsCS.FindKernel("FluidSimulationDensityCS");
                _pressureKernel = _fluidPhysicsCS.FindKernel("FluidSimulationPressureCS");
                _updateKernel = _fluidPhysicsCS.FindKernel("FluidSimulationCS");
                _fluidPhysicsCS.SetBuffer(_densityKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                _fluidPhysicsCS.SetBuffer(_pressureKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                _fluidPhysicsCS.SetBuffer(_updateKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                
                // Build Grid
                _buildGridKernel = _fluidPhysicsCS.FindKernel("FluidBuildGridCS");
                
                _particleGridIndex = new int2[MAX_FLUIDPOINT_COUNT];
                for (int i = 0; i < _particleGridIndex.Length; i++)
                {
                    _particleGridIndex[i] = Int32.MaxValue;
                }
                _fluidParticleGridSortedBuffer.SetData(_particleGridIndex);
                _fluidParticleGridSortedTempBuffer.SetData(_particleGridIndex);
                
                _fluidPhysicsCS.SetBuffer(_buildGridKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                _fluidPhysicsCS.SetBuffer(_buildGridKernel,"_FluidParticleGridSorted", _fluidParticleGridSortedBuffer);
                _fluidPhysicsCS.SetBuffer(_buildGridKernel,"_FluidParticleGridSortedTemp", _fluidParticleGridSortedTempBuffer);
                
                // Sort Grid
                _sortGridKernel = _fluidPhysicsCS.FindKernel("SortGridCS");
                _fluidPhysicsCS.SetBuffer(_sortGridKernel,"_FluidParticleGrid", _fluidParticleGridBuffer);
                _fluidPhysicsCS.SetBuffer(_sortGridKernel,"_FluidParticleGridSorted", _fluidParticleGridSortedBuffer);
                
                //Density Grid Sort
                _gridDensityMaterial.SetBuffer("_FluidParticleGrid", _fluidParticleGridBuffer);
                _gridDensityMaterial.SetBuffer("_FluidParticleGridSorted", _fluidParticleGridSortedBuffer);
                
                _gridPressureMaterial.SetBuffer("_FluidParticleGrid", _fluidParticleGridBuffer);
                _gridPressureMaterial.SetBuffer("_FluidParticleGridSorted", _fluidParticleGridSortedBuffer);
                
                
                _particleMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _vizDensityMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _gridDensityMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _gridPressureMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);

                _mesh = CreateQuad(1, 1);
                _args[0] = (uint)_mesh.GetIndexCount(0);
                _args[1] = (uint)FluidParticleCount;
                _args[2] = (uint)_mesh.GetIndexStart(0);
                _args[3] = (uint)_mesh.GetBaseVertex(0);

                _argsBuffer.SetData(_args);

                _textureDescriptor = new RenderTextureDescriptor();
                    
                m_PassInit = true;
            }
            
            
        #region Update Data

        internal static NativeArray<T> BeginWriteBuffer<T>(FluidComputeBufferType fluidComputeBufferType, int startIndex, int count) where T : struct
        {
            _isWriting = true;
            NativeArray<T> result = default;
            switch(fluidComputeBufferType)
            {
                case FluidComputeBufferType.Graphics:
                    var particlesGraphicsNative = _particlesGraphicsBuffer.BeginWrite<FluidParticleGraphics>(startIndex, count);
                    return (NativeArray<T>)Convert.ChangeType(particlesGraphicsNative, typeof(NativeArray<T>));
                case FluidComputeBufferType.Physics:
                    var particlesPhysicsNative = _particlesInitBuffer.BeginWrite<FluidParticlePhysics>(startIndex, count);
                    return (NativeArray<T>)Convert.ChangeType(particlesPhysicsNative, typeof(NativeArray<T>));
            }
            return result;
        }
        internal static void EndWriteBuffer(FluidComputeBufferType fluidComputeBufferType, int count)
        {
            _isWriting = false;
            switch(fluidComputeBufferType)
            {
                case FluidComputeBufferType.Graphics:
                    _particlesGraphicsBuffer.EndWrite<FluidParticleGraphics>(count);
                    return;

                case FluidComputeBufferType.Physics:
                    _particlesInitBuffer.EndWrite<FluidParticlePhysics>(count);
                    return;
            }
        }
        #endregion
        
            private RTHandle m_CameraColorTarget;
            public void SetTarget(RTHandle colorHandle)
            {
                m_CameraColorTarget = colorHandle;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (m_CameraColorTarget != null)
                {
                    ConfigureTarget(m_CameraColorTarget);
                }
            }

            public void CreateRT(in RenderingData renderingData)
            {
                CreateRenderTexture("RTDebug", RenderTextureFormat.RGFloat, ref _textureDescriptor, in renderingData, ref m_DebugRTHandle);
                CreateRenderTexture("RTDebugPressure", RenderTextureFormat.ARGBFloat, ref _textureDescriptor, in renderingData, ref m_DebugPressureRTHandle);
                CreateRenderTexture("RTDensity", RenderTextureFormat.RFloat, ref _textureDescriptor, in renderingData, ref m_RTHandleDensity);
                CreateRenderTexture("VizDensityRT", RenderTextureFormat.ARGBFloat,ref _textureDescriptor, in renderingData, ref m_RTHandleVizDensity);
                CreateRenderTexture("RTGradient",RenderTextureFormat.ARGBFloat,ref _textureDescriptor, in renderingData, ref m_RTHandleGradient);
                CreateRenderTexture("RTPressure",RenderTextureFormat.ARGBFloat,ref _textureDescriptor, in renderingData, ref m_RTHandlePressure);
            }

            internal static void UpdateParticleCount(int prevCount, int currCount)
            {
                // Update arg buffer
                if (_argsBuffer == null) return;
                var arg1 = _argsBuffer.BeginWrite<uint>(1, 1);
                arg1[0] = (uint)currCount;
                _argsBuffer.EndWrite<int>(1);
            
                // Update computeShader
                _fluidPhysicsCS.SetInt("_PrevFluidParticleCount", prevCount);
                _fluidPhysicsCS.SetInt("_CurrFluidParticleCount", currCount);

                if (currCount == 0) return;
                int threadGroupX = Mathf.CeilToInt(currCount / 64.0f);
                _fluidPhysicsCS.SetBuffer(_initKernel,"_FluidParticleInit", _particlesInitBuffer);
                _fluidPhysicsCS.SetBuffer(_initKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                _fluidPhysicsCS.Dispatch(_initKernel, threadGroupX,1,1);
            }

            internal static void SetCursorPosition(Vector3 cursorPos)
            {
                _cursorPos = cursorPos;
            }

            private void UpdateGridBuffer(CommandBuffer cmd, Vector4 texelSize, float smoothRadius)
            {
                int yCount = Mathf.FloorToInt(1f / smoothRadius);
                int xCount = Mathf.FloorToInt((texelSize.x / texelSize.y) / smoothRadius);
                int totalCount = (xCount) * (yCount);
                if (totalCount != _fluidParticleGridBuffer.count)
                {
                    _gidIndex = new int[totalCount];
                    _fluidParticleGridBuffer.Dispose();
                    _fluidParticleGridBuffer = new ComputeBuffer(totalCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Default);
                }
                for (int i = 0; i < _gidIndex.Length; i++)
                {
                    _gidIndex[i] = Int32.MaxValue;
                }
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!m_PassInit) return;
                if (FluidParticleCount == 0 ) return;
                if (renderingData.cameraData.cameraType != CameraType.Game) return;
                
                CommandBuffer cmd = CommandBufferPool.Get(name: "DensityFieldPass");
                Vector4 textureSize = Vector4.zero;
                if (m_RTHandleDensity.rt != null)
                { 
                    textureSize = new Vector4(m_RTHandleDensity.rt.width, m_RTHandleDensity.rt.height, 0, 0);
                }
                if (enableUpdate)
                {
                    m_DeltaTime = Time.deltaTime;
                }
                cmd.SetGlobalFloat("_FluidDeltaTime", m_DeltaTime);
                cmd.SetGlobalFloat("_SmoothRadius", _dRadius);
                cmd.SetGlobalVector("_TexelSize", textureSize);
            
                _particleMaterial.SetColor("_ParticleColor", _pColor);
                _particleMaterial.SetFloat("_Pixel", _pPixel);
                _particleMaterial.SetFloat("_ParticleRadius", _pRadius);
                
                _gridDensityMaterial.SetVector("_CursorPosition", _cursorPos);
                _gridDensityMaterial.SetFloat("_SmoothRadius", _dRadius);
                _gridDensityMaterial.SetVector("_TexelSize", textureSize);
                
                _vizDensityMaterial.SetFloat("_SmoothRadius", _dRadius);
                _vizDensityMaterial.SetColor("_UnderTargetColor", _underTargetCol);
                _vizDensityMaterial.SetColor("_OverTargetColor", _overTargetCol);
                _vizDensityMaterial.SetColor("_AroundTargetColor", _aroundTargetCol);
                _vizDensityMaterial.SetFloat("_TargetValue", _targetValue);
                
                _gridPressureMaterial.SetFloat("_SmoothRadius", _dRadius);
                _gridPressureMaterial.SetFloat("_TargetValue", _targetValue);
                _gridPressureMaterial.SetFloat("_PressureMultiplier", _pressureMultiplier);
                _gridPressureMaterial.SetFloat("_Pixel", _pPixel);
                
                cmd.SetGlobalInt("_FluidParticleCount", FluidParticleCount);
                
                // Sort
                UpdateGridBuffer(cmd, textureSize, _dRadius);
                cmd.SetComputeBufferParam(_fluidPhysicsCS,_sortGridKernel,"_FluidParticleGrid" , _fluidParticleGridBuffer);
                
                cmd.SetBufferData(_fluidParticleGridBuffer, _gidIndex);
                cmd.SetBufferData(_fluidParticleGridSortedBuffer, _particleGridIndex);
                cmd.SetBufferData(_fluidParticleGridSortedTempBuffer, _particleGridIndex);
                cmd.SetComputeBufferParam(_fluidPhysicsCS,_buildGridKernel,"_FluidParticleGridSorted" , _fluidParticleGridSortedBuffer);
                cmd.SetComputeBufferParam(_fluidPhysicsCS,_buildGridKernel,"_FluidParticleGridSortedTemp" , _fluidParticleGridSortedBuffer);
                cmd.DispatchCompute(_fluidPhysicsCS,_buildGridKernel, Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
                FluidBitonicSortCS.GPUSort(cmd ,MAX_FLUIDPOINT_COUNT, _bitonicSortCS, _fluidParticleGridSortedBuffer, _fluidParticleGridSortedTempBuffer);  // Density Bitonic
                cmd.DispatchCompute(_fluidPhysicsCS,_sortGridKernel,Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
                _gridDensityMaterial.SetBuffer("_FluidParticleGrid", _fluidParticleGridBuffer);
                _gridDensityMaterial.SetBuffer("_FluidParticleGridSorted", _fluidParticleGridSortedBuffer);
                _gridPressureMaterial.SetBuffer("_FluidParticleGrid", _fluidParticleGridBuffer);
                _gridPressureMaterial.SetBuffer("_FluidParticleGridSorted", _fluidParticleGridSortedBuffer);
                
                
                // Draw Grid Density
                CreateRenderTexture("RTDensity", RenderTextureFormat.RFloat, ref _textureDescriptor, in renderingData, ref m_RTHandleDensity);
                cmd.SetRenderTarget(m_RTHandleDensity,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.Blit(null, m_RTHandleDensity, _gridDensityMaterial,0);
                cmd.SetGlobalTexture("_FluidDensity", m_RTHandleDensity);
                if (drawGridDensityField)
                {
                    Blitter.BlitCameraTexture(cmd, m_RTHandleDensity, m_CameraColorTarget, 0);
                }
                

                // Viz Density
                if (drawVizDensityMap)
                {
                    CreateRenderTexture("VizDensityRT", RenderTextureFormat.ARGBFloat,ref _textureDescriptor, in renderingData, ref m_RTHandleVizDensity);
                    cmd.SetRenderTarget(m_RTHandleVizDensity,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
                    cmd.ClearRenderTarget(true, true, Color.black);
                    cmd.Blit(m_RTHandleDensity, m_RTHandleVizDensity, _vizDensityMaterial,0);
                    Blitter.BlitCameraTexture(cmd, m_RTHandleVizDensity, m_CameraColorTarget, 0);
                }
                
                
                // Draw Pressure Field
                CreateRenderTexture("RTPressure",RenderTextureFormat.ARGBFloat,ref _textureDescriptor, in renderingData, ref m_RTHandlePressure);
                cmd.SetRenderTarget(m_RTHandlePressure, RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare );
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.Blit(null, m_RTHandlePressure, _gridPressureMaterial, 0);
                if (drawGridPressureField)
                {
                    Blitter.BlitCameraTexture(cmd, m_RTHandlePressure, m_CameraColorTarget, 0);
                }
                
                CreateRenderTexture("RTDebug", RenderTextureFormat.RGFloat, ref _textureDescriptor, in renderingData, ref m_DebugRTHandle);
                CreateRenderTexture("RTDebugPressure", RenderTextureFormat.ARGBFloat, ref _textureDescriptor, in renderingData, ref m_DebugPressureRTHandle);
                    
                cmd.SetComputeFloatParam(_fluidPhysicsCS,"_EnergyDumping", _energyDamping);
                cmd.SetComputeFloatParam(_fluidPhysicsCS,"_ParticleSize", _pRadius);
                cmd.SetComputeFloatParam(_fluidPhysicsCS,"_Deltatime", m_DeltaTime);
                cmd.SetComputeFloatParam(_fluidPhysicsCS, "_Gravity", _gravity);
                cmd.SetComputeFloatParam(_fluidPhysicsCS, "_TargetValue", _targetValue);
                cmd.SetComputeFloatParam(_fluidPhysicsCS, "_PressureMultiplier", _pressureMultiplier);
                cmd.SetComputeVectorParam(_fluidPhysicsCS, "_TexelSize", new Vector4( m_RTHandleDensity.rt.width,  m_RTHandleDensity.rt.height, 0, 0));
                cmd.SetComputeBufferParam(_fluidPhysicsCS,_densityKernel,"_FluidParticleGrid",_fluidParticleGridBuffer);
                cmd.SetComputeBufferParam(_fluidPhysicsCS,_densityKernel,"_FluidParticleGridSorted",_fluidParticleGridSortedBuffer);
                cmd.SetComputeBufferParam(_fluidPhysicsCS,_pressureKernel,"_FluidParticleGrid",_fluidParticleGridBuffer);
                cmd.SetComputeBufferParam(_fluidPhysicsCS,_pressureKernel,"_FluidParticleGridSorted",_fluidParticleGridSortedBuffer);
                
                cmd.SetRenderTarget(m_DebugRTHandle,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.SetComputeTextureParam(_fluidPhysicsCS, _densityKernel, "_DebugTexture",m_DebugRTHandle);
                cmd.SetComputeBufferParam(_fluidPhysicsCS,_pressureKernel,"_FluidParticlePhysics",_particlesPhysicsBuffer);
                cmd.DispatchCompute(_fluidPhysicsCS,_densityKernel, Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
                
                
                cmd.SetRenderTarget(m_DebugPressureRTHandle,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.SetComputeTextureParam(_fluidPhysicsCS, _pressureKernel, "_DebugTexture",m_DebugRTHandle);
                cmd.SetComputeTextureParam(_fluidPhysicsCS, _pressureKernel, "_DebugPressureTexture",m_DebugPressureRTHandle);
                cmd.SetComputeBufferParam(_fluidPhysicsCS,_pressureKernel,"_FluidParticlePhysics",_particlesPhysicsBuffer);
                cmd.DispatchCompute(_fluidPhysicsCS,_pressureKernel, Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
                
                // Compute Physics
                if (enableUpdate)
                {
                    cmd.SetComputeTextureParam(_fluidPhysicsCS, _updateKernel, "_DebugTexture",m_DebugRTHandle);
                    cmd.SetComputeTextureParam(_fluidPhysicsCS, _updateKernel, "_DebugPressureTexture",m_DebugPressureRTHandle);
                    cmd.SetComputeTextureParam(_fluidPhysicsCS, _updateKernel, "_FluidDensity", m_RTHandleDensity);
                    cmd.SetComputeTextureParam(_fluidPhysicsCS, _updateKernel, "_FluidPressure", m_RTHandlePressure);
                    cmd.SetComputeBufferParam(_fluidPhysicsCS,_updateKernel,"_FluidParticlePhysics",_particlesPhysicsBuffer);
                    cmd.DispatchCompute(_fluidPhysicsCS,_updateKernel, Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
                    
                    // cmd.SetComputeTextureParam(_fluidPhysicsCS, _mainKernel, "_FluidDensity", m_RTHandleDensity);
                    // cmd.SetComputeTextureParam(_fluidPhysicsCS, _mainKernel, "_FluidPressure", m_RTHandlePressure);
                    // cmd.DispatchCompute(_fluidPhysicsCS,_mainKernel, Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
                }

                if (computeShaderDebug)
                {
                    cmd.SetRenderTarget(m_CameraColorTarget);
                    Blitter.BlitCameraTexture(cmd, m_DebugPressureRTHandle, m_CameraColorTarget, 0);
                }

                // Draw Particles
                if (drawParticles)
                {
                    cmd.SetRenderTarget(m_CameraColorTarget);
                    cmd.DrawMeshInstancedIndirect(_mesh, 0, _particleMaterial, 0, _argsBuffer);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                m_RTHandleParticle?.Release();
                m_RTHandleDensity?.Release();
                m_RTHandleVizDensity?.Release();
                m_RTHandleGradient?.Release();
                
                _particlesGraphicsBuffer?.Release();
                _particlesInitBuffer?.Release();
                _particlesPhysicsBuffer?.Release();
                _argsBuffer?.Release();
                
                _fluidParticleGridBuffer?.Release();
                _fluidParticleGridSortedBuffer?.Release();
                _fluidParticleGridSortedTempBuffer?.Release();
                m_PassInit = false;
            }
        }

        public ComputeShader fluidPhysicsCS;
        public ComputeShader bitonicSortCS;
        public Shader drawParticlesShader;
        public Shader drawGridDensityShader;
        public Shader vizDensityShader;
        public Shader drawGridPressureShader;

        public override void Create()
        {
            if (fluidPhysicsCS == null || bitonicSortCS == null || drawParticlesShader == null ||drawGridDensityShader ==null ||vizDensityShader == null ||drawGridPressureShader == null) return;
            m_DensityFieldPass = new DensityFieldPass(fluidPhysicsCS,bitonicSortCS, drawParticlesShader,drawGridDensityShader,vizDensityShader,drawGridPressureShader);
            passCreated = true;
        }

        public static void CreateRenderTexture(string rtName,RenderTextureFormat format , ref RenderTextureDescriptor rtTextureDescriptor ,in RenderingData renderingData, ref RTHandle rtHandle)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            if (rtTextureDescriptor.width != renderingData.cameraData.camera.pixelWidth || rtTextureDescriptor.height != renderingData.cameraData.camera.pixelHeight || rtHandle == null)
            {
                rtTextureDescriptor = new RenderTextureDescriptor
                {
                    width = renderingData.cameraData.camera.pixelWidth,
                    height = renderingData.cameraData.camera.pixelHeight,
                    dimension = TextureDimension.Tex2D,
                    volumeDepth = 1,
                    useMipMap = false,
                    colorFormat = format,
                    msaaSamples = 1,
                    enableRandomWrite = true
                };
            }
            
            if (rtHandle == null)
            {
                rtHandle = RTHandles.Alloc(
                    rtTextureDescriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    false,
                    1,
                    0,
                    rtName
                );
            } 
            else if (rtHandle.rt!=null && (rtHandle.rt.width != rtTextureDescriptor.width || rtHandle.rt.height != rtTextureDescriptor.height) )
            {
                rtHandle.Release();
                rtHandle = RTHandles.Alloc(
                    rtTextureDescriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    false,
                    1,
                    0,
                    rtName
                );
            }
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!_isWriting)
            {
                if (!passCreated) return;
                renderer.EnqueuePass(m_DensityFieldPass);
            }
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer,
            in RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                if (!passCreated) return;
                // Calling ConfigureInput with the ScriptableRenderPassInput.Color argument
                // ensures that the opaque texture is available to the Render Pass.
                m_DensityFieldPass.ConfigureInput(ScriptableRenderPassInput.Color);
                m_DensityFieldPass.SetTarget(renderer.cameraColorTargetHandle);
                m_DensityFieldPass.CreateRT(renderingData);
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            m_DensityFieldPass?.Dispose();
            passCreated = false;
        }
    }
    
}