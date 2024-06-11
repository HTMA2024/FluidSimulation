using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static FluidSimulation.FluidUtilities;
using static FluidSimulation.Globals;

namespace FluidSimulation
{
    public class FluidDensityFieldRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader m_DrawParticlesShader;
        [SerializeField] private Shader m_DrawDensityShader;
        
        private DensityFieldPass m_DensityFieldPass;
        private static Mesh _mesh;

        private static ComputeBuffer _argsBuffer;
        private static uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        private static Material _particleMaterial;
        private static Material _densityMaterial;
        private static float _pRadius;
        private static float _dRadius;
        private static Color _dColor;
        private static RenderTexture _rt;

        private static NativeArray<FluidParticleGraphics> _fluidParticleGraphicsNative;
        internal static ComputeBuffer computeBuffer { get; private set; }
        private static bool _isInit = false;

        private void Init(Shader particleShader, Shader densityShader)
        {
            _particleMaterial = new Material(particleShader);
            _densityMaterial = new Material(densityShader);
            computeBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticleGraphics)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

            _particleMaterial.SetBuffer("_ComputeBuffer", computeBuffer);
            _densityMaterial.SetBuffer("_ComputeBuffer", computeBuffer);

            _mesh = CreateQuad(1, 1);
            args[0] = (uint)_mesh.GetIndexCount(0);
            args[1] = (uint)fluidParticleCount;
            args[2] = (uint)_mesh.GetIndexStart(0);
            args[3] = (uint)_mesh.GetBaseVertex(0);

            _argsBuffer.SetData(args);
            _isInit = true;
        }

        #region Update Data

        internal static void BeginWriteBuffer(int startIndex, int count)
        {
            _fluidParticleGraphicsNative = computeBuffer.BeginWrite<FluidParticleGraphics>(startIndex, count);
        }
        internal static void EndWriteBuffer(int count)
        {
            computeBuffer.EndWrite<FluidParticleGraphics>(count);
        }

        internal static void UpdateParticle(FluidParticle fluidParticle, int index)
        {
            var fluidParticleGraphics = _fluidParticleGraphicsNative[index];
            fluidParticleGraphics.position = fluidParticle.position;
            fluidParticleGraphics.color = Vector3.one;
            _fluidParticleGraphicsNative[index] = fluidParticleGraphics;
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
                
                if (fluidParticleCount == 0) return;
                args[1] = (uint)fluidParticleCount;
                _argsBuffer.SetData(args);
            
                _particleMaterial.SetFloat("_ParticleRadius", _pRadius);
                _densityMaterial.SetFloat("_DensityRadius", _dRadius);
                _densityMaterial.SetColor("_Color", _dColor);

                // cmd.SetRenderTarget(_rt);
                cmd.DrawMeshInstancedIndirect(_mesh, 0, _particleMaterial, 0, _argsBuffer);
                cmd.DrawMeshInstancedIndirect(_mesh, 0, _densityMaterial, 0, _argsBuffer);
                
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
            
        }
        public override void Create()
        {
            if (m_DrawParticlesShader == null || m_DrawDensityShader  == null) return;
            Init(m_DrawParticlesShader, m_DrawDensityShader);
            m_DensityFieldPass = new DensityFieldPass();
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_isInit)
            {
                renderer.EnqueuePass(m_DensityFieldPass);
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            computeBuffer.Dispose();
        }
    }
}