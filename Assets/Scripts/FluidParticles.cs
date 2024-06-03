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
        public Vector3 velocity;
        public Vector3 accelration;
        public Vector3 color;

        public void Update(float deltatime)
        {
            velocity += accelration;
            position += velocity * deltatime;
            if (position.x - 1 > 1e-5 || position.x + 1 < 1e-5)
            {
                velocity.x *= -1;
            }
            if ( position.y - 1 > 1e-5 || position.y + 1 < 1e-5 )
            {
                velocity.y *= -1;
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
    
    public static class FluidParticlePhysics 
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
        internal static void Add(ComputeBuffer computeBuffer, Vector3 position, Color color)
        {
            if (fluidParticleCount >= MAX_FLUIDPOINT_COUNT) 
            {
                Debug.Log("[ParticleRenderer] <AddFluidPoint> Exceed Max Point Limit");
                return;
            }
            var fluidParticlesGraphicsNative = computeBuffer.BeginWrite<FluidParticleGraphics>(fluidParticleCount,1);
            var fluidParticlesDataNtvArray = fluidParticlesNtvArray.GetSubArray(fluidParticleCount, 1);
            
            var fluidParticle = fluidParticlesDataNtvArray[0];
            fluidParticle.velocity = Vector3.Normalize(new Vector3(Random.value, Random.value, 0));
            fluidParticle.position = position;
            fluidParticle.position.z = 1; // Make it 2D
            fluidParticle.color = (Vector4) color;
            fluidParticlesDataNtvArray[0] = fluidParticle;
            
            var fluidParticleGraphics = fluidParticlesGraphicsNative[0];
            fluidParticleGraphics.position = fluidParticle.position;
            fluidParticleGraphics.color = fluidParticle.color;
            fluidParticlesGraphicsNative[0] = fluidParticleGraphics;
            
            computeBuffer.EndWrite<FluidParticleGraphics>(1);
            fluidParticleCount += 1;
        }
        
        internal static void AddMultiple(ComputeBuffer computeBuffer, Vector3[] positions, int count)
        {
            Profiler.BeginSample("[ParticleRenderer] <AddFluidParticlesSub>");
            var fluidParticlesGraphicsNative = computeBuffer.BeginWrite<FluidParticleGraphics>(fluidParticleCount,count);
            var fluidParticlesDataNtvArray = fluidParticlesNtvArray.GetSubArray(fluidParticleCount, count);
            for (int i = 0; i < count; i++)
            {
                var fluidParticle = fluidParticlesDataNtvArray[i];
                fluidParticle.position = positions[i];
                fluidParticle.velocity = Vector3.Normalize(new Vector3(Random.value, Random.value, 0));
                fluidParticle.color = Vector3.one;
                fluidParticle.position.z = 1; // Make it 2D
                fluidParticlesDataNtvArray[i] = fluidParticle;
                
                var fluidParticleGraphics = fluidParticlesGraphicsNative[i];
                fluidParticleGraphics.position = fluidParticle.position;
                fluidParticleGraphics.color = fluidParticle.color;
                fluidParticlesGraphicsNative[i] = fluidParticleGraphics;
            }
            computeBuffer.EndWrite<FluidParticleGraphics>(count);
            Profiler.EndSample();
            
            fluidParticleCount += count;
        }
        
        public static void Update(ComputeBuffer computeBuffer, float deltatime)
        {
            // computeBuffer should be SubUpdate mode
            
            var fluidParticlesGraphicsNative = computeBuffer.BeginWrite<FluidParticleGraphics>(0,fluidParticleCount);
            var fluidParticlesDataNtvArray = fluidParticlesNtvArray.GetSubArray(0,fluidParticleCount);
            for (int i = 0; i < fluidParticleCount; i++)
            {
                var fluidParticle = fluidParticlesDataNtvArray[i];
                fluidParticle.Update(deltatime);
                fluidParticlesDataNtvArray[i] = fluidParticle;
                
                var fluidParticleGraphics = fluidParticlesGraphicsNative[i];
                fluidParticleGraphics.position = fluidParticle.position;
                fluidParticleGraphics.color = fluidParticle.color;
                fluidParticlesGraphicsNative[i] = fluidParticleGraphics;
            }
            computeBuffer.EndWrite<FluidParticleGraphics>(fluidParticleCount);
        }
        
        public static void Clean(ComputeBuffer computeBuffer)
        {
            if (fluidParticleCount == 0) return;
            var fluidParticlesGraphicsNative = computeBuffer.BeginWrite<FluidParticleGraphics>(0,fluidParticleCount);
            var fluidParticlesDataNtvArray = fluidParticlesNtvArray.GetSubArray(0,fluidParticleCount);
            for (int i = 0; i < fluidParticleCount; i++)
            {
                var fluidParticle = fluidParticlesDataNtvArray[i];
                fluidParticle.velocity = Vector3.zero;
                fluidParticle.position = Vector3.zero;
                fluidParticle.position.z = 0; // Make it 2D
                fluidParticle.color = Vector3.zero;
                fluidParticlesDataNtvArray[i] = fluidParticle;
                
                var fluidParticleGraphics = fluidParticlesGraphicsNative[i];
                fluidParticleGraphics.position = fluidParticle.position;
                fluidParticleGraphics.color = fluidParticle.color;
                fluidParticlesGraphicsNative[i] = fluidParticleGraphics;
            }
            computeBuffer.EndWrite<FluidParticleGraphics>(fluidParticleCount);
            fluidParticleCount = 0;
        }
        
        public static void Dispose()
        {
            fluidParticlesNtvArray.Dispose();
            fluidParticleCount = 0;
        }
    }
}