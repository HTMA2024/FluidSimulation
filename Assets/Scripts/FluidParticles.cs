using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using static FluidSimulation.FluidUtilities;
using Random = UnityEngine.Random;

namespace FluidSimulation
{
    public struct FluidParticle
    {
        public Vector3 position;
        public Vector3 acceleration;
        public Vector3 color;

        public void Update(float deltatime)
        {
            position += acceleration * deltatime;
            if (position.x - 1 > 1e-5 || position.x + 1 < 1e-5 || position.y - 1 > 1e-5 || position.y + 1 < 1e-5 )
            {
                acceleration *= -1;
            }

            if (position.x - 1 > 1e-5)
            {
                position.x = 1;
            }
            
            if (position.x + 1 < 1e-5)
            {
                position.x = -1;
            }
            
            if (position.y - 1 > 1e-5)
            {
                position.y = 1;
            }
            
            if (position.y + 1 < 1e-5)
            {
                position.y = -1;
            }
            
        }
    }
    
    public static class FluidParticleSystem 
    {
        internal static readonly FluidParticle[] FluidParticleArray = new FluidParticle[MAX_FLUIDPOINT_COUNT];
        internal static NativeArray<FluidParticle> fluidParticlesNtvArray;
        internal static int fluidParticleCount = 0;

        internal static void Init()
        {
            fluidParticlesNtvArray = new NativeArray<FluidParticle>(FluidParticleArray, Allocator.Persistent);
            fluidParticleCount = 0;
        }
        
        
        // ReSharper disable Unity.PerformanceAnalysis
        internal static void AddFluidParticleSub(ComputeBuffer computeBuffer, Vector3 position, Color color)
        {
            if (fluidParticleCount >= MAX_FLUIDPOINT_COUNT) 
            {
                Debug.Log("[ParticleRenderer] <AddFluidPoint> Exceed Max Point Limit");
                return;
            }
            var fluidParticleBufferArray = computeBuffer.BeginWrite<FluidParticle>(fluidParticleCount,1);
            var fluidParticlesDataNtvArray = fluidParticlesNtvArray.GetSubArray(fluidParticleCount, 1);
            
            var fluidParticle = fluidParticleBufferArray[0];
            fluidParticle.acceleration = Vector3.Normalize(new Vector3(Random.value, Random.value, 0));
            fluidParticle.position = position;
            fluidParticle.position.z = 1; // Make it 2D
            fluidParticle.color = (Vector4) color;
            fluidParticleBufferArray[0] = fluidParticle;
            fluidParticlesDataNtvArray[0] = fluidParticle;
            computeBuffer.EndWrite<FluidParticle>(1);
            fluidParticleCount += 1;
        }
        
        internal static void AddFluidParticlesSub(ComputeBuffer computeBuffer, Vector3[] positions, int count)
        {
            Profiler.BeginSample("[ParticleRenderer] <AddFluidParticlesSub>");
            var fluidParticleBufferArray = computeBuffer.BeginWrite<FluidParticle>(fluidParticleCount,count);
            var fluidParticlesDataNtvArray = fluidParticlesNtvArray.GetSubArray(fluidParticleCount, count);
            for (int i = 0; i < count; i++)
            {
                var fluidParticle = fluidParticleBufferArray[i];
                fluidParticle.position = positions[i];
                fluidParticle.acceleration = Vector3.Normalize(new Vector3(Random.value, Random.value, 0));
                fluidParticle.color = Vector3.one;
                fluidParticle.position.z = 1; // Make it 2D
                fluidParticleBufferArray[i] = fluidParticle;
                fluidParticlesDataNtvArray[i] = fluidParticle;
            }
            computeBuffer.EndWrite<FluidParticle>(count);
            Profiler.EndSample();
            
            fluidParticleCount += count;
        }
        
        public static void UpdateFluidParticlesSub(ComputeBuffer computeBuffer, float deltatime)
        {
            // computeBuffer should be SubUpdate mode
            
            var fluidParticles = computeBuffer.BeginWrite<FluidParticle>(0,fluidParticleCount);
            var fluidParticlesDataNtvArray = fluidParticlesNtvArray.GetSubArray(0,fluidParticleCount);
            for (int i = 0; i < fluidParticleCount; i++)
            {
                var fluidParticle = fluidParticles[i];
                fluidParticle.Update(deltatime);
                fluidParticles[i] = fluidParticle;
                fluidParticlesDataNtvArray[i] = fluidParticle;
            }
            computeBuffer.EndWrite<FluidParticle>(fluidParticleCount);
        }
        
        public static void CleanSub(ComputeBuffer computeBuffer)
        {
            if (fluidParticleCount == 0) return;
            var fluidParticles = computeBuffer.BeginWrite<FluidParticle>(0,fluidParticleCount);
            var fluidParticlesDataNtvArray = fluidParticlesNtvArray.GetSubArray(0,fluidParticleCount);
            for (int i = 0; i < fluidParticleCount; i++)
            {
                var fluidParticle = fluidParticles[i];
                fluidParticle.acceleration = Vector3.zero;
                fluidParticle.position = Vector3.zero;
                fluidParticle.position.z = 0; // Make it 2D
                fluidParticle.color = Vector3.zero;
                fluidParticles[i] = fluidParticle;
                fluidParticlesDataNtvArray[i] = fluidParticle;
            }
            computeBuffer.EndWrite<FluidParticle>(fluidParticleCount);
            fluidParticleCount = 0;
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        public static void AddFluidParticle(Vector3 position, Color color)
        {
            if (fluidParticleCount >= MAX_FLUIDPOINT_COUNT) 
            {
                Debug.Log("[ParticleRenderer] <AddFluidPoint> Exceed Max Point Limit");
                return;
            }
            var fluidParticle = fluidParticlesNtvArray[fluidParticleCount];
            fluidParticle.acceleration = Vector3.Normalize(new Vector3(Random.value, Random.value, 0));
            fluidParticle.position = position;
            fluidParticle.position.z = 1; // Make it 2D
            fluidParticle.color = (Vector4) color;
            fluidParticlesNtvArray[fluidParticleCount] = fluidParticle;
            fluidParticleCount += 1;
        }

        public static void AddFluidParticles(Vector3[] positions, int count)
        {
            Profiler.BeginSample("[ParticleRenderer] <AddFluidParticles>");
            var fluidParticleArray = fluidParticlesNtvArray.GetSubArray(fluidParticleCount, count);
            for (int i = 0; i < count; i++)
            {
                var fluidParticle = fluidParticleArray[i];
                fluidParticle.acceleration = Vector3.Normalize(new Vector3(Random.value, Random.value, 0));
                fluidParticle.position = positions[i];
                fluidParticle.color = Vector3.one;
                fluidParticle.position.z = 1; // Make it 2D
                fluidParticleArray[i] = fluidParticle;
            }
            Profiler.EndSample();
            
            fluidParticleCount += count;
        }

        public static void UpdateFluidParticles(float deltatime)
        {
            for (int i = 0; i < fluidParticleCount; i++)
            {
                var fluidParticle = fluidParticlesNtvArray[i];
                fluidParticle.Update(deltatime);
                fluidParticlesNtvArray[i] = fluidParticle;
            }
        }
        
        public static void Clean()
        {
            for (int i = 0; i < fluidParticleCount; i++)
            {
                var fluidParticle = fluidParticlesNtvArray[fluidParticleCount];
                fluidParticle.acceleration = Vector3.zero;
                fluidParticle.position = Vector3.zero;
                fluidParticle.position.z = 0; // Make it 2D
                fluidParticle.color = Vector3.zero;
                fluidParticlesNtvArray[fluidParticleCount] = fluidParticle;
            }
            fluidParticleCount = 0;
        }

        public static void Dispose()
        {
            fluidParticlesNtvArray.Dispose();
            fluidParticleCount = 0;
        }
    }
}