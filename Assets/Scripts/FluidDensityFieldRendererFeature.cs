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
        [SerializeField] private Shader m_DrawParticlesShader;
        [SerializeField] private Shader m_DrawDensityShader;
        private static Material _particleMaterial;
        private static Material _densityMaterial;
        private static float _pRadius;
        private static float _dRadius;
        private static Color _dColor;
        private static Mesh _mesh;

        private static RenderTexture _rt;
        private static ComputeBuffer _particlesBuffer;
        internal static ComputeBuffer argsBuffer;

        private static readonly uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };

        private static NativeArray<FluidParticleGraphics> _fluidParticleGraphicsNative;
        
        private DensityFieldPass m_DensityFieldPass;
        
        private static bool _isInit = false;

        internal static void Init(Shader particleShader, Shader densityShader)
        {
            _particleMaterial = new Material(particleShader);
            _densityMaterial = new Material(densityShader);
            _particlesBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticleGraphics)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments,ComputeBufferMode.SubUpdates);

            _particleMaterial.SetBuffer("_ComputeBuffer", _particlesBuffer);
            _densityMaterial.SetBuffer("_ComputeBuffer", _particlesBuffer);

            _mesh = CreateQuad(1, 1);
            _args[0] = (uint)_mesh.GetIndexCount(0);
            _args[1] = (uint)FluidParticleCount;
            _args[2] = (uint)_mesh.GetIndexStart(0);
            _args[3] = (uint)_mesh.GetBaseVertex(0);

            argsBuffer.SetData(_args);
            _isInit = true;
        }

        #region Update Data

        internal static NativeArray<FluidParticleGraphics> BeginWriteBuffer(int startIndex, int count)
        {
            var particlesNative = _particlesBuffer.BeginWrite<FluidParticleGraphics>(startIndex, count);
            return particlesNative;
        }
        internal static void EndWriteBuffer(int count)
        {
            _particlesBuffer.EndWrite<FluidParticleGraphics>(count);
        }

        internal static void UpdateArgsBuffer()
        {
            // Update arg buffer
            if (argsBuffer == null) return;
            var arg1 = argsBuffer.BeginWrite<uint>(1, 1);
            arg1[0] = (uint)FluidParticleCount;
            argsBuffer.EndWrite<int>(1);
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

                cmd.SetRenderTarget(BuiltinRenderTextureType.CurrentActive, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.DrawMeshInstancedIndirect(_mesh, 0, _particleMaterial, 0, argsBuffer);
                cmd.DrawMeshInstancedIndirect(_mesh, 0, _densityMaterial, 0, argsBuffer);
                
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
            if (m_DrawParticlesShader == null || m_DrawDensityShader  == null) return;
            m_DensityFieldPass = new DensityFieldPass();
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_isInit)
            {
                ResizeRT(ref renderingData);
                renderer.EnqueuePass(m_DensityFieldPass);
            }
        }

        #endregion

    }
}