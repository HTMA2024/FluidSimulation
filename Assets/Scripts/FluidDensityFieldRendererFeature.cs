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
        
        private static Material _particleMaterial;
        private static Material _densityMaterial;
        private static Material _gradientMaterial;
        private static float _pRadius;
        private static float _dRadius;
        private static Color _dColor;
        private static Mesh _mesh;

        private static RenderTexture _rt;
        private static RTHandle m_RTHandleDensity;
        private static RTHandle m_RTHandleGradient;

        private static ComputeShader _computeShader;
        private static ComputeBuffer _particlesInitBuffer;
        private static ComputeBuffer _particlesGraphicsBuffer;
        private static ComputeBuffer _particlesPhysicsBuffer;
        private static ComputeBuffer _argsBuffer;

        private static int _computeKernel;
        private static int _initKernel;
        private static int _cameraKernel;

        private static readonly uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };
        
        private DensityFieldPass m_DensityFieldPass;

        internal static bool enableUpdate = false;
        internal static bool drawDensityField = false;
        internal static bool drawGradientField = false;
        private static bool _isWriting = false;

        internal static void SetRendererFeatureParams(Shader particleShader, Shader densityShader, Shader gradientShader, ComputeShader computeShader)
        {
            _computeShader = computeShader;
            _particleMaterial = new Material(particleShader);
            _densityMaterial = new Material(densityShader);
            _gradientMaterial = new Material(gradientShader);
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

        #endregion

        #region Helpers

        internal static void SetParticleRadius(float radius)
        {
            _pRadius = radius;
        }
        internal static void SetDensityRadius(float radius)
        {
            _dRadius = radius;
        }
        internal static RTHandle GetRenderTexture()
        {
            return m_RTHandleDensity;
        }
        internal static void SetDensityColor(Color color)
        {
            _dColor = color;
        }        

        #endregion

        #region RenderFeature

        
        class DensityFieldPass : ScriptableRenderPass
        {
            public DensityFieldPass()
            {
                if (_computeShader == null) return;
                
                _particlesInitBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticlePhysics)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
                _particlesGraphicsBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticleGraphics)), ComputeBufferType.Default);
                _particlesPhysicsBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticlePhysics)), ComputeBufferType.Default);

                _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments,ComputeBufferMode.SubUpdates);

                _computeKernel = _computeShader.FindKernel("FluidSimulationCS");
                _computeShader.SetInt("_FluidParticleCount", FluidParticleCount);
                _computeShader.SetBuffer(_computeKernel,"_FluidParticleInit", _particlesInitBuffer);
                _computeShader.SetBuffer(_computeKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                _computeShader.SetBuffer(_computeKernel,"_FluidParticleGraphics", _particlesGraphicsBuffer);
                
                _initKernel = _computeShader.FindKernel("InitCS");
                _computeShader.SetBuffer(_initKernel,"_FluidParticleInit", _particlesInitBuffer);
                _computeShader.SetBuffer(_initKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
                
                _particleMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _densityMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
                _gradientMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);

                _mesh = CreateQuad(1, 1);
                _args[0] = (uint)_mesh.GetIndexCount(0);
                _args[1] = (uint)FluidParticleCount;
                _args[2] = (uint)_mesh.GetIndexStart(0);
                _args[3] = (uint)_mesh.GetBaseVertex(0);

                _argsBuffer.SetData(_args);
            }
            
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
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(name: "DensityFieldPass");
                
                if (FluidParticleCount == 0) return;
            
                _particleMaterial.SetFloat("_ParticleRadius", _pRadius);
                _densityMaterial.SetFloat("_DensityRadius", _dRadius);
                _densityMaterial.SetColor("_Color", _dColor);
                _gradientMaterial.SetFloat("_DensityRadius", _dRadius);
    
                var deltaTime = Time.deltaTime;
                _computeShader.SetFloat("_Deltatime", deltaTime);
                
                // Compute Physics
                if (enableUpdate)
                {
                    cmd.DispatchCompute(_computeShader,_computeKernel, Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
                }
                
                // Draw
                cmd.SetRenderTarget(BuiltinRenderTextureType.CurrentActive, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.DrawMeshInstancedIndirect(_mesh, 0, _particleMaterial, 0, _argsBuffer);
                // cmd.DrawMeshInstancedIndirect(_mesh, 0, _densityMaterial, 0, _argsBuffer);
                
                if (drawDensityField)
                {
                    cmd.SetRenderTarget(m_RTHandleDensity,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
                    cmd.ClearRenderTarget(true, true, Color.black);
                    cmd.DrawMeshInstancedIndirect(_mesh, 0, _densityMaterial, 0, _argsBuffer);
                    Blitter.BlitCameraTexture(cmd, m_RTHandleDensity, m_CameraColorTarget, 0);
                }
                
                if (drawGradientField)
                {
                    cmd.SetRenderTarget(m_RTHandleDensity,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
                    cmd.ClearRenderTarget(true, true, Color.black);
                    cmd.DrawMeshInstancedIndirect(_mesh, 0, _densityMaterial, 0, _argsBuffer);
                    cmd.SetGlobalTexture("_FluidDensity", m_RTHandleDensity);
                    
                    cmd.SetRenderTarget(m_RTHandleGradient, RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare );
                    cmd.ClearRenderTarget(true, true, Color.black);
                    cmd.DrawMeshInstancedIndirect(_mesh, 0, _gradientMaterial, 0, _argsBuffer);
                    Blitter.BlitCameraTexture(cmd, m_RTHandleGradient, m_CameraColorTarget, 0);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                m_RTHandleDensity?.Release();
                m_RTHandleGradient?.Release();
                
                _particlesGraphicsBuffer?.Release();
                _particlesInitBuffer?.Release();
                _particlesPhysicsBuffer?.Release();
                _argsBuffer?.Release();
            }
        }

        private void SetRTHandle(ref RenderingData renderingData)
        {
            // if (m_RTHandle != null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            RenderTextureDescriptor renderTextureDescriptor = new RenderTextureDescriptor();
            renderTextureDescriptor.width =  renderingData.cameraData.camera.pixelWidth;
            renderTextureDescriptor.height = renderingData.cameraData.camera.pixelHeight;
            renderTextureDescriptor.dimension = TextureDimension.Tex2D;
            renderTextureDescriptor.volumeDepth = 1;
            renderTextureDescriptor.useMipMap = false;
            renderTextureDescriptor.colorFormat = RenderTextureFormat.ARGBFloat;
            renderTextureDescriptor.msaaSamples = 1;
            renderTextureDescriptor.enableRandomWrite = true;
            
            Vector2Int screen = new Vector2Int(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight);

            // RTHandles.SetReferenceSize(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight);
            if (m_RTHandleDensity != null)
            {
                if (m_RTHandleDensity.GetScaledSize() == screen) return;
                m_RTHandleDensity.Release();
                m_RTHandleDensity = RTHandles.Alloc(
                    renderTextureDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    false,
                    1,
                    0,
                    "DensityRT"
                );
            }
            else
            {

                m_RTHandleDensity = RTHandles.Alloc(
                    renderTextureDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    false,
                    1,
                    0,
                    "DensityRT"
                );
            }
            if (m_RTHandleGradient != null)
            {
                if (m_RTHandleGradient.GetScaledSize() == screen) return;
                m_RTHandleGradient.Release();
                m_RTHandleGradient = RTHandles.Alloc(
                    renderTextureDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    false,
                    1,
                    0,
                    "GradientRT"
                );
            }
            else
            {

                m_RTHandleGradient = RTHandles.Alloc(
                    renderTextureDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    false,
                    1,
                    0,
                    "GradientRT"
                );
            }
            
        }
        public override void Create()
        {
            if (Application.isPlaying)
            {
                // RTHandles.Initialize(Screen.width, Screen.height);
            }
            m_DensityFieldPass = new DensityFieldPass();
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!_isWriting)
            {
                SetRTHandle(ref renderingData);
                renderer.EnqueuePass(m_DensityFieldPass);
            }
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer,
            in RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                // Calling ConfigureInput with the ScriptableRenderPassInput.Color argument
                // ensures that the opaque texture is available to the Render Pass.
                m_DensityFieldPass.ConfigureInput(ScriptableRenderPassInput.Color);
                m_DensityFieldPass.SetTarget(renderer.cameraColorTargetHandle);
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            m_DensityFieldPass?.Dispose();
        }
    }
    
}