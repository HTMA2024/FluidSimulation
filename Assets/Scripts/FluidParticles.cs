using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using static FluidSimulation.FluidUtilities;
using static FluidSimulation.Globals;
using Random = UnityEngine.Random;

namespace FluidSimulation
{
    public struct FluidParticle
    {
        public int index;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;
        public Vector3 color;

        public static Action<FluidParticle, int> onUpdate;
        private const float energyDumping = 0.98f;

        public void Update(float deltatime)
        {
            velocity += acceleration * deltatime;
            position += velocity * deltatime;
            if (position.x - 1 > 1e-5 || position.x + 1 < 1e-5)
            {
                velocity.x *= -1 * energyDumping;
            }
            if ( position.y - 1 > 1e-5 || position.y + 1 < 1e-5 )
            {
                velocity.y *= -1 * energyDumping;
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
            
            onUpdate?.Invoke(this, index);
        }
    }
    
    public static class FluidParticlePhysics 
    {
        private static readonly FluidParticle[] FluidParticleArray = new FluidParticle[MAX_FLUIDPOINT_COUNT];
        private static NativeArray<FluidParticle> _fluidParticlesNtvArray;

        internal static void Init()
        {
            _fluidParticlesNtvArray = new NativeArray<FluidParticle>(FluidParticleArray, Allocator.Persistent);
            SetParticleCount(0);

            FluidParticle.onUpdate += FluidDensityFieldRendererFeature.UpdateParticle;
            for (int i = 0; i < _fluidParticlesNtvArray.Length; i++)
            {
                var fluidParticle= _fluidParticlesNtvArray[i];
                fluidParticle.index = i;
                _fluidParticlesNtvArray[i] = fluidParticle;
            }
        }
        
        
        internal static void Add(ComputeBuffer computeBuffer, Vector3 position, Color color)
        {
            var fluidParticlesGraphicsNative = computeBuffer.BeginWrite<FluidParticleGraphics>(fluidParticleCount,1);
            var fluidParticlesDataNtvArray = _fluidParticlesNtvArray.GetSubArray(fluidParticleCount, 1);
            
            var fluidParticle = fluidParticlesDataNtvArray[0];
            fluidParticle.acceleration = Vector3.up ;
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
            SetParticleCount(fluidParticleCount + 1);
        }
        
        internal static void AddMultiple(ComputeBuffer computeBuffer, Vector3[] positions, int count)
        {
            Profiler.BeginSample("[ParticleRenderer] <AddFluidParticlesSub>");
            var fluidParticlesGraphicsNative = computeBuffer.BeginWrite<FluidParticleGraphics>(fluidParticleCount,count);
            var fluidParticlesDataNtvArray = _fluidParticlesNtvArray.GetSubArray(fluidParticleCount, count);
            for (int i = 0; i < count; i++)
            {
                var fluidParticle = fluidParticlesDataNtvArray[i];
                fluidParticle.acceleration = Vector3.up;
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
            
            SetParticleCount(fluidParticleCount + count);
        }
        
        public static void Update(float deltatime)
        {
            // computeBuffer should be SubUpdate mode
            var fluidParticlesDataNtvArray = _fluidParticlesNtvArray.GetSubArray(0,fluidParticleCount);
            for (int i = 0; i < fluidParticleCount; i++)
            {
                var fluidParticle = fluidParticlesDataNtvArray[i];
                fluidParticle.Update(deltatime);
                fluidParticlesDataNtvArray[i] = fluidParticle;
                
            }
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
            
            // FluidParticleSystem.AddFluidParticles(positions, count);
            // FluidDensityFieldRendererFeature.UpdateBuffers();
            AddMultiple(FluidDensityFieldRendererFeature.computeBuffer, positions, count);
        }
        
        public static void Clean(ComputeBuffer computeBuffer)
        {
            if (fluidParticleCount == 0) return;
            var fluidParticlesGraphicsNative = computeBuffer.BeginWrite<FluidParticleGraphics>(0,fluidParticleCount);
            var fluidParticlesDataNtvArray = _fluidParticlesNtvArray.GetSubArray(0,fluidParticleCount);
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
            SetParticleCount(0);
        }
        
        public static void Dispose()
        {
            _fluidParticlesNtvArray.Dispose();
            SetParticleCount(0);
        }
    }
}