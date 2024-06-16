using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
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
        private static float _energyDamping;
        private static float _dRadius;
        private static Color _underTargetCol;
        private static Color _overTargetCol;
        private static Color _aroundTargetCol;
        private static float _targetValue;
        private static float _pressureMultiplier;
        private static Mesh _mesh;

        private static RenderTexture _rt;


        private static int _mainKernel;
        private static int _initKernel;
        private static int _buildGridKernel;
        private static int _cameraKernel;

        private static readonly uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };
        
        private DensityFieldPass m_DensityFieldPass;

        internal static bool enableUpdate = false;
        internal static bool drawDensityField = false;
        internal static bool drawVizDensityMap = false;
        internal static bool drawGradientField = false;
        internal static bool drawPressureField = false;
        internal static bool drawParticles = false;
        private static bool _isWriting = false;
        public static bool passCreated = false;

        
        

        #region Helpers

        internal static void SetParticleParams(float radius, Color color, float energyDamping, float pixel)
        {
            _pPixel = pixel;
            _pColor = color;
            _pRadius = radius;
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


        public class DensityFieldPass : ScriptableRenderPass
        {
            private bool m_PassInit = false;
            private static ComputeShader _computeShader;

            private static Material _particleMaterial;
            private static Material _densityMaterial;
            private static Material _griddensityMaterial;
            private static Material _gradientMaterial;
            private static Material _vizDensityMaterial;
            private static Material _pressureMaterial;
            
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
            
            public DensityFieldPass(ComputeShader computeShader, Shader drawParticlesShader, Shader drawDensityShader,Shader drawGridDensityShader, Shader vizDensityShader, Shader drawGradientShader, Shader drawPressureShader)
            {
                _computeShader = computeShader;
                _particleMaterial = new Material(drawParticlesShader);
                _densityMaterial = new Material(drawDensityShader);
                _griddensityMaterial = new Material(drawGridDensityShader);
                _gradientMaterial = new Material(drawGradientShader);
                _vizDensityMaterial = new Material(vizDensityShader);
                _pressureMaterial = new Material(drawPressureShader);
                
                _particlesGraphicsBuffer?.Release();
                _particlesInitBuffer?.Release();
                _particlesPhysicsBuffer?.Release();
                _argsBuffer?.Release();
                
                _particlesInitBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticlePhysics)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
                _particlesGraphicsBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticleGraphics)), ComputeBufferType.Default);
                _particlesPhysicsBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticlePhysics)), ComputeBufferType.Default);
                _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments,ComputeBufferMode.SubUpdates);

                _mainKernel = _computeShader.FindKernel("FluidSimulationCS");
                _computeShader.SetBuffer(_mainKernel,"_FluidParticleInit", _particlesInitBuffer);
                _computeShader.SetBuffer(_mainKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                
                _initKernel = _computeShader.FindKernel("InitCS");
                _computeShader.SetBuffer(_initKernel,"_FluidParticleInit", _particlesInitBuffer);
                _computeShader.SetBuffer(_initKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                
                _buildGridKernel = _computeShader.FindKernel("FluidBuildGridCS");
                _computeShader.SetBuffer(_buildGridKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                
                _particleMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _densityMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _gradientMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _vizDensityMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _pressureMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _griddensityMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);

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
                _computeShader.SetInt("_PrevFluidParticleCount", prevCount);
                _computeShader.SetInt("_CurrFluidParticleCount", currCount);

                if (currCount == 0) return;
                int threadGroupX = Mathf.CeilToInt(currCount / 64.0f);
                _computeShader.SetBuffer(_initKernel,"_FluidParticleInit", _particlesInitBuffer);
                _computeShader.SetBuffer(_initKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                _computeShader.Dispatch(_initKernel, threadGroupX,1,1);
            }

            internal static void SetCursorPosition(Vector3 cursorPos)
            {
                _cursorPos = cursorPos;
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!m_PassInit) return;
                if (renderingData.cameraData.cameraType != CameraType.Game) return;
                
                CommandBuffer cmd = CommandBufferPool.Get(name: "DensityFieldPass");
                
                if (FluidParticleCount == 0) return;
                cmd.SetGlobalFloat("_FluidDeltaTime", Time.deltaTime);
                cmd.SetGlobalFloat("_SmoothRadius", _dRadius);
            
                _particleMaterial.SetColor("_ParticleColor", _pColor);
                _particleMaterial.SetFloat("_Pixel", _pPixel);
                _particleMaterial.SetFloat("_ParticleRadius", _pRadius);
                
                _densityMaterial.SetFloat("_SmoothRadius", _dRadius);
                
                _griddensityMaterial.SetVector("_CursorPosition", _cursorPos);
                _griddensityMaterial.SetFloat("_SmoothRadius", _dRadius);
                _griddensityMaterial.SetVector("_TexelSize", new Vector4( m_RTHandleDensity.rt.width,  m_RTHandleDensity.rt.height, 0, 0));
                
                _gradientMaterial.SetFloat("_SmoothRadius", _dRadius);
                _vizDensityMaterial.SetFloat("_SmoothRadius", _dRadius);
                _vizDensityMaterial.SetColor("_UnderTargetColor", _underTargetCol);
                _vizDensityMaterial.SetColor("_OverTargetColor", _overTargetCol);
                _vizDensityMaterial.SetColor("_AroundTargetColor", _aroundTargetCol);
                _vizDensityMaterial.SetFloat("_TargetValue", _targetValue);
                
                _pressureMaterial.SetFloat("_SmoothRadius", _dRadius);
                _pressureMaterial.SetFloat("_TargetValue", _targetValue);
                _pressureMaterial.SetFloat("_PressureMultiplier", _pressureMultiplier);
                _pressureMaterial.SetFloat("_Pixel", _pPixel);
                
                cmd.SetGlobalInt("_FluidParticleCount", FluidParticleCount);
                cmd.DispatchCompute(_computeShader,_buildGridKernel, Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
                if (m_RTHandleDensity.rt != null)
                {
                    cmd.SetGlobalVector("_TexelSize", new Vector4( m_RTHandleDensity.rt.width,  m_RTHandleDensity.rt.height, 0, 0));
                }
                
                // Draw Density
                CreateRenderTexture("RTDensity", RenderTextureFormat.RFloat, ref _textureDescriptor, in renderingData, ref m_RTHandleDensity);
                cmd.SetRenderTarget(m_RTHandleDensity,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.Blit(null, m_RTHandleDensity, _griddensityMaterial,0);
                cmd.DrawMeshInstancedIndirect(_mesh, 0, _densityMaterial, 0, _argsBuffer);
                cmd.SetGlobalTexture("_FluidDensity", m_RTHandleDensity);
                if (drawDensityField)
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
                
                // Draw Gradient Field
                if (drawGradientField)
                {
                    CreateRenderTexture("RTGradient",RenderTextureFormat.ARGBFloat,ref _textureDescriptor, in renderingData, ref m_RTHandleGradient);
                    cmd.SetRenderTarget(m_RTHandleGradient, RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare );
                    cmd.ClearRenderTarget(true, true, Color.black);
                    cmd.DrawMeshInstancedIndirect(_mesh, 0, _gradientMaterial, 0, _argsBuffer);
                    Blitter.BlitCameraTexture(cmd, m_RTHandleGradient, m_CameraColorTarget, 0);
                }
                
                // Draw Pressure Field
                CreateRenderTexture("RTPressure",RenderTextureFormat.ARGBFloat,ref _textureDescriptor, in renderingData, ref m_RTHandlePressure);
                cmd.SetRenderTarget(m_RTHandlePressure, RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare );
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.DrawMeshInstancedIndirect(_mesh, 0, _pressureMaterial, 0, _argsBuffer);
                if (drawPressureField)
                {
                    Blitter.BlitCameraTexture(cmd, m_RTHandlePressure, m_CameraColorTarget, 0);
                }
                
                // Compute Physics
                if (enableUpdate)
                {
                    cmd.SetComputeFloatParam(_computeShader,"_EnergyDumping", _energyDamping);
                    cmd.SetComputeFloatParam(_computeShader,"_ParticleSize", _pRadius);
                    cmd.SetComputeFloatParam(_computeShader,"_Deltatime", Time.deltaTime);
                    cmd.SetComputeVectorParam(_computeShader, "_TexelSize", new Vector4( m_RTHandleDensity.rt.width,  m_RTHandleDensity.rt.height, 0, 0));
                    cmd.SetComputeTextureParam(_computeShader, _mainKernel, "_FluidDensity", m_RTHandleDensity);
                    cmd.SetComputeTextureParam(_computeShader, _mainKernel, "_FluidPressure", m_RTHandlePressure);
                    cmd.DispatchCompute(_computeShader,_mainKernel, Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
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
                m_PassInit = false;
            }
        }

        public ComputeShader computeShader;
        public Shader drawParticlesShader;
        public Shader drawDensityShader;
        public Shader drawGridDensityShader;
        public Shader vizDensityShader;
        public Shader drawGradientShader;
        public Shader drawPressureShader;

        public override void Create()
        {
            if (computeShader == null || drawParticlesShader == null ||drawDensityShader == null || drawGridDensityShader ==null ||vizDensityShader == null ||drawGradientShader == null || drawPressureShader == null) return;
            m_DensityFieldPass = new DensityFieldPass(computeShader,drawParticlesShader,drawDensityShader,drawGridDensityShader,vizDensityShader,drawGradientShader, drawPressureShader);
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