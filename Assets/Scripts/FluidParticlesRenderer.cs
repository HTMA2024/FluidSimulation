using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using static FluidSimulation.FluidUtilities;
using Random = UnityEngine.Random;
using static FluidSimulation.FluidParticleSystem;

namespace FluidSimulation
{
    public abstract class FluidParticlesRenderer 
    {
        private static Mesh _mesh;
        private Shader m_DrawParticlesShader;

        private static ComputeBuffer _computeBuffer;
        private static ComputeBuffer _argsBuffer;
        private static uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        private static Material _material;
        private static Bounds _bounds;
        private static float _radius;


        internal static ComputeBuffer computeBuffer => _computeBuffer;
        internal static int GetFluidParticleCount() => fluidParticleCount;
        
        internal static void Initialize(Shader circlesShader)
        {
            _material = new Material(circlesShader);
            _computeBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticle)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            
            _mesh = CreateQuad(1,1);
            args[0] = (uint)_mesh.GetIndexCount(0);
            args[1] = (uint)GetFluidParticleCount();
            args[2] = (uint)_mesh.GetIndexStart(0);
            args[3] = (uint)_mesh.GetBaseVertex(0);
            _argsBuffer.SetData(args);
            _bounds = new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f));
        }


        internal static void UpdateBuffers(bool isSubUpdate)
        {
            if (!isSubUpdate)
            {
                _computeBuffer.SetData(fluidParticlesNtvArray);
            }
            
            args[1] = (uint)fluidParticleCount;;
            _argsBuffer.SetData(args);
            
            _material.SetBuffer("_ComputeBuffer", _computeBuffer);
        }

        internal static void SetRadius(float radius)
        {
            _radius = radius;
        }

        internal static void ExecuteRender()
        {
            if (fluidParticleCount == 0) return;
            _material.SetPass(0);
            _material.SetFloat("_CircleRadius", _radius);
            Graphics.DrawMeshInstancedIndirect(_mesh, 0, _material, _bounds, _argsBuffer);
        }


        public static void Dispose()
        {
            _material = null;
            _computeBuffer?.Release();
            _argsBuffer?.Dispose();
        }
    }
}