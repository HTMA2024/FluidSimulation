using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using static FluidSimulation.FluidUtilities;
using static FluidSimulation.Globals;
using static FluidSimulation.FluidDensityFieldRendererFeature;
using Random = UnityEngine.Random;

namespace FluidSimulation
{
    public static class FluidParticlePhysicsSystem 
    {
        internal static void Init()
        {
            SetParticleCount(0);
        }

        private static FluidParticlePhysics CreateParticle(Vector3 position)
        {
            var fluidParticle = new FluidParticlePhysics();
            fluidParticle.acceleration = Vector3.Normalize(Vector3.one - 2.0f *new Vector3(Random.value, Random.value, 0));
            fluidParticle.velocity = Vector3.Normalize(Vector3.one - 2.0f *new Vector3(Random.value, Random.value, 0));
            fluidParticle.position = position;
            fluidParticle.position.z = 1; // Make it 2D
            return fluidParticle;
        }

        private static FluidParticlePhysics CleanParticle()
        {
            var fluidParticle = new FluidParticlePhysics();
            fluidParticle.velocity = Vector3.zero;
            fluidParticle.position = Vector3.zero;
            fluidParticle.position.z = 0; // Make it 2D
            // fluidParticle.color = Vector3.zero;
            return fluidParticle;
        }
        
        
        internal static void Add(Vector3 position, Color color)
        {
            var fluidParticlesPhysicsNative = BeginWriteBuffer<FluidParticlePhysics>(FluidComputeBufferType.Physics,FluidParticleCount,1);
            
            var fluidParticle = fluidParticlesPhysicsNative[0];
            var particle = CreateParticle(position);
            fluidParticle = particle;
            fluidParticlesPhysicsNative[0] = fluidParticle;
            
            EndWriteBuffer(FluidComputeBufferType.Physics,1);
            SetParticleCount(FluidParticleCount + 1);
        }
        
        internal static void AddMultiple(Vector3[] positions, int count)
        {
            
            var fluidParticlesPhysicsNative = BeginWriteBuffer<FluidParticlePhysics>(FluidComputeBufferType.Physics, FluidParticleCount,count);
            for (int i = 0; i < count; i++)
            {
                var fluidParticle = fluidParticlesPhysicsNative[i];
                var particle = CreateParticle(positions[i]);
                fluidParticle = particle;
                fluidParticlesPhysicsNative[i] = fluidParticle;
            }
            EndWriteBuffer(FluidComputeBufferType.Physics,count);
            SetParticleCount(FluidParticleCount + count);
        }
        
        internal static void FillScreen(float pwidth,float pheight,float density)
        {
            var width =  (int) (pwidth / (float)density);
            var height = (int) (pheight / (float)density);
            var count = (int)(width + 1) * (int)(height + 1);

            var positions = new Vector3[count];
            
            for (int i = 0; i <= width; i++)
            {
                for (int j = 0; j <= height; j++)
                {
                    Vector3 position = new Vector3((float)i/width, (float)j/height);
                    position = position * 2 - Vector3.one;
                    var index = (int)(i * (height + 1) + j);
                    positions[index] = position;
                }
            }
            
            AddMultiple(positions, count);
        }
        
        public static void Clean()
        {
            if (FluidParticleCount == 0) return;
            var fluidParticlesPhysicsNative = BeginWriteBuffer<FluidParticlePhysics>(FluidComputeBufferType.Physics,0,FluidParticleCount);
            for (int i = 0; i < FluidParticleCount; i++)
            {
                var fluidParticle = fluidParticlesPhysicsNative[i];
                fluidParticle = CleanParticle();
                fluidParticlesPhysicsNative[i] = fluidParticle;
            }
            EndWriteBuffer(FluidComputeBufferType.Physics,FluidParticleCount);
            SetParticleCount(0);
        }
        
        public static void Dispose()
        {
            SetParticleCount(0);
        }
    }
}