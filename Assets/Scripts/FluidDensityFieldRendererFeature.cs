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
        private static float _pRadius;
        private static float _dRadius;
        private static Color _dColor;
        private static Mesh _mesh;

        private static RenderTexture _rt;

        private static ComputeShader _computeShader;
        private static ComputeBuffer _particlesInitBuffer;
        private static ComputeBuffer _particlesGraphicsBuffer;
        private static ComputeBuffer _particlesPhysicsBuffer;
        private static ComputeBuffer _argsBuffer;

        private static int _computeKernel;
        private static int _initKernel;

        private static readonly uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };
        
        private DensityFieldPass m_DensityFieldPass;

        private static bool _isWriting = false;
        private static bool _isInit = false;

        internal static void Init(Shader particleShader, Shader densityShader, ComputeShader computeShader)
        {
            _computeShader = computeShader;
            _particleMaterial = new Material(particleShader);
            _densityMaterial = new Material(densityShader);
            _particlesInitBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticlePhysics)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            _particlesGraphicsBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticleGraphics)), ComputeBufferType.Default);
            _particlesPhysicsBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticlePhysics)), ComputeBufferType.Default);

            _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments,ComputeBufferMode.SubUpdates);

            _initKernel = _computeShader.FindKernel("InitCS");
            _computeKernel = _computeShader.FindKernel("FluidSimulationCS");
            _computeShader.SetInt("_FluidParticleCount", FluidParticleCount);
            _computeShader.SetBuffer(_computeKernel,"_FluidParticleInit", _particlesInitBuffer);
            _computeShader.SetBuffer(_computeKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
            _computeShader.SetBuffer(_computeKernel,"_FluidParticleGraphics", _particlesGraphicsBuffer);
            
            _particleMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);
            _densityMaterial.SetBuffer("_ComputeBuffer", _particlesPhysicsBuffer);

            _mesh = CreateQuad(1, 1);
            _args[0] = (uint)_mesh.GetIndexCount(0);
            _args[1] = (uint)FluidParticleCount;
            _args[2] = (uint)_mesh.GetIndexStart(0);
            _args[3] = (uint)_mesh.GetBaseVertex(0);

            _argsBuffer.SetData(_args);
            _isInit = true;
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
            _computeShader.SetBuffer(_initKernel,"_FluidParticleInit", _particlesInitBuffer);
            _computeShader.SetBuffer(_initKernel,"_FluidParticlePhysics", _particlesPhysicsBuffer);
            int threadGroupX = Mathf.CeilToInt(currCount / 64.0f);
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
        internal static RenderTexture GetRenderTexture()
        {
            return _rt;
        }
        internal static void SetDensityColor(Color color)
        {
            _dColor = color;
        }        

        #endregion

        #region RenderFeature

        
        class DensityFieldPass : ScriptableRenderPass
        {
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(name: "DensityFieldPass");
                
                if (FluidParticleCount == 0) return;
            
                _particleMaterial.SetFloat("_ParticleRadius", _pRadius);
                _densityMaterial.SetFloat("_DensityRadius", _dRadius);
                _densityMaterial.SetColor("_Color", _dColor);
    
                var deltaTime = Time.deltaTime;
                _computeShader.SetFloat("_Deltatime", deltaTime);
                cmd.DispatchCompute(_computeShader,_computeKernel, Mathf.CeilToInt(FluidParticleCount / 64.0f),1,1 );
                
                cmd.SetRenderTarget(BuiltinRenderTextureType.CurrentActive, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.DrawMeshInstancedIndirect(_mesh, 0, _particleMaterial, 0, _argsBuffer);
                cmd.DrawMeshInstancedIndirect(_mesh, 0, _densityMaterial, 0, _argsBuffer);
                
                // cmd.SetRenderTarget(_rt, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                // cmd.ClearRenderTarget(true, true, Color.black);
                // cmd.DrawMeshInstancedIndirect(_mesh, 0, _densityMaterial, 0, _argsBuffer);
                
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);

                
            }
            
        }

        private void CreateRT(int pixelWidth, int pixelHeight)
        {
            RenderTextureDescriptor renderTextureDescriptor = new RenderTextureDescriptor();
            renderTextureDescriptor.width = pixelWidth;
            renderTextureDescriptor.height = pixelHeight;
            renderTextureDescriptor.dimension = TextureDimension.Tex2D;
            renderTextureDescriptor.volumeDepth = 1;
            renderTextureDescriptor.useMipMap = false;
            renderTextureDescriptor.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
            renderTextureDescriptor.bindMS = false;
            renderTextureDescriptor.msaaSamples = 1;
            renderTextureDescriptor.depthStencilFormat = GraphicsFormat.None;
            renderTextureDescriptor.useDynamicScale = false;
     
            _rt = RenderTexture.GetTemporary(renderTextureDescriptor);
        }

        private void ResizeRT(ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                var pixelWidth = renderingData.cameraData.camera.pixelWidth;
                var pixelHeight = renderingData.cameraData.camera.pixelHeight;
                if (_rt != null)
                {
                    _rt.Release();
                }
                else
                {
                    CreateRT(pixelWidth, pixelHeight);
                }
                if (pixelWidth != _rt.width || pixelHeight != _rt.height)
                {
                    CreateRT(pixelWidth, pixelHeight);
                }
            }
        }
        public override void Create()
        {
            m_DensityFieldPass = new DensityFieldPass();
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_isInit && !_isWriting)
            {
                ResizeRT(ref renderingData);
                renderer.EnqueuePass(m_DensityFieldPass);
            }
        }

        #endregion

    }
}