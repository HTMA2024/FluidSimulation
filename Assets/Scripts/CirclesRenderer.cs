using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Collections;
using static FluidSimulation.FluidUtilities;
using Random = UnityEngine.Random;

namespace FluidSimulation
{
    public abstract class CirclesRenderer : IDisposable
    {
        private Shader m_DrawCirclesShader;

        private static ComputeBuffer _computeBuffer;
        public static Material _material;

        private static readonly FluidPoint[] m_CirclesArray = new FluidPoint[MAX_FLUIDPOINT_COUNT];
        private static int _fluidPointCount = 0;
        private static NativeArray<FluidPoint> _circlesNtvArray;


        public static int GetFluidPointCount() => _fluidPointCount;
        
        public static void Initialize(Shader circlesShader)
        {
            _fluidPointCount = 0;
            _material = new Material(circlesShader);
            _circlesNtvArray = new NativeArray<FluidPoint>(m_CirclesArray, Allocator.Persistent);
            _computeBuffer = new ComputeBuffer(m_CirclesArray.Length, Marshal.SizeOf(typeof(FluidPoint)), ComputeBufferType.Default);
        }


        public static void AddFluidPoint(Vector3 position, Vector3 color)
        {
            var fluidPoint = _circlesNtvArray[_fluidPointCount];
            fluidPoint.Position = position;
            fluidPoint.Position.z = 1; // Make it 2D
            fluidPoint.Color = color;
            _circlesNtvArray[_fluidPointCount] = fluidPoint;
            _fluidPointCount += 1;
            UpdateComputeBuffer();
        }

        public static void Clean()
        {
            for (int i = 0; i < _fluidPointCount; i++)
            {
                var fluidPoint = _circlesNtvArray[_fluidPointCount];
                fluidPoint.Position = Vector3.zero;
                fluidPoint.Position.z = 0; // Make it 2D
                fluidPoint.Color = Vector3.zero;
                _circlesNtvArray[_fluidPointCount] = fluidPoint;
            }
            _fluidPointCount = 0;
        }
        private static void UpdateComputeBuffer()
        {
            if (_fluidPointCount == 0) return;
            _computeBuffer.SetData(_circlesNtvArray);
            _material.SetBuffer("_ComputeBuffer", _computeBuffer);
        }


        public void Dispose()
        {
            _material = null;
            _computeBuffer?.Release();
        }
    }
}