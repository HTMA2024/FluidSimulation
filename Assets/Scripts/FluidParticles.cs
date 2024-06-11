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
    public struct FluidParticlePhysics
    {
        public int index;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;
        // public Vector3 color;
        //
        // public static Action<FluidParticlePhysics, int> onUpdate;
        // private const float energyDumping = 0.98f;
        //
        // public void Update(float deltatime, NativeArray<FluidParticleGraphics> graphicsNtvArray)
        // {
        //     velocity += acceleration * deltatime;
        //     position += velocity * deltatime;
        //     if (position.x - 1 > 1e-5 || position.x + 1 < 1e-5)
        //     {
        //         velocity.x *= -1 * energyDumping;
        //     }
        //     if ( position.y - 1 > 1e-5 || position.y + 1 < 1e-5 )
        //     {
        //         velocity.y *= -1 * energyDumping;
        //     }
        //
        //     if (position.x - 1 > 1e-5)
        //     {
        //         position.x = 1;
        //     }
        //     
        //     if (position.x + 1 < 1e-5)
        //     {
        //         position.x = -1;
        //     }
        //     
        //     if (position.y - 1 > 1e-5)
        //     {
        //         position.y = 1;
        //     }
        //     
        //     if (position.y + 1 < 1e-5)
        //     {
        //         position.y = -1;
        //     }
        //     
        //     var fluidParticleGraphics = graphicsNtvArray[index];
        //     fluidParticleGraphics.position = this.position;
        //     fluidParticleGraphics.color = Vector3.one;
        //     graphicsNtvArray[index] = fluidParticleGraphics;
        // }
    }
    
    public static class FluidParticlePhysicsSystem 
    {
        private static readonly FluidParticlePhysics[] FluidParticleArray = new FluidParticlePhysics[MAX_FLUIDPOINT_COUNT];
        private static NativeArray<FluidParticlePhysics> _fluidParticlesNtvArray;

        internal static void Init()
        {
            _fluidParticlesNtvArray = new NativeArray<FluidParticlePhysics>(FluidParticleArray, Allocator.Persistent);
            SetParticleCount(0);

            for (int i = 0; i < _fluidParticlesNtvArray.Length; i++)
            {
                var fluidParticle= _fluidParticlesNtvArray[i];
                fluidParticle.index = i;
                _fluidParticlesNtvArray[i] = fluidParticle;
            }
        }

        private static FluidParticlePhysics CreateParticle(Vector3 position)
        {
            var fluidParticle = new FluidParticlePhysics();
            fluidParticle.acceleration = Vector3.up ;
            fluidParticle.velocity = Vector3.Normalize(new Vector3(Random.value, Random.value, 0));
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
            var fluidParticlesGraphicsNative = BeginWriteBuffer<FluidParticleGraphics>(FluidComputeBufferType.Graphics,FluidParticleCount,1);
            var fluidParticlesDataNtvArray = _fluidParticlesNtvArray.GetSubArray(FluidParticleCount, 1);
            
            var fluidParticle = fluidParticlesDataNtvArray[0];
            var particle = CreateParticle(position);
            fluidParticle = particle;
            fluidParticlesDataNtvArray[0] = fluidParticle;
            
            var fluidParticleGraphics = fluidParticlesGraphicsNative[0];
            fluidParticleGraphics.position = fluidParticle.position;
            // fluidParticleGraphics.color = fluidParticle.color;
            fluidParticlesGraphicsNative[0] = fluidParticleGraphics;
            
            EndWriteBuffer(FluidComputeBufferType.Graphics,1);
            SetParticleCount(FluidParticleCount + 1);
        }
        
        internal static void AddMultiple(Vector3[] positions, int count)
        {
            Profiler.BeginSample("[ParticleRenderer] <AddFluidParticlesSub>");
            
            var fluidParticlesGraphicsNative = BeginWriteBuffer<FluidParticleGraphics>(FluidComputeBufferType.Graphics, FluidParticleCount,count);
            var fluidParticlesDataNtvArray = _fluidParticlesNtvArray.GetSubArray(FluidParticleCount, count);
            for (int i = 0; i < count; i++)
            {
                var fluidParticle = fluidParticlesDataNtvArray[i];
                var particle = CreateParticle(positions[i]);
                fluidParticle = particle;
                fluidParticlesDataNtvArray[i] = fluidParticle;
                
                var fluidParticleGraphics = fluidParticlesGraphicsNative[i];
                fluidParticleGraphics.position = fluidParticle.position;
                // fluidParticleGraphics.color = fluidParticle.color;
                fluidParticlesGraphicsNative[i] = fluidParticleGraphics;
            }
            EndWriteBuffer(FluidComputeBufferType.Graphics,count);
            Profiler.EndSample();
            
            SetParticleCount(FluidParticleCount + count);
        }
        
        // public static void Update(float deltatime)
        // {
        //     // computeBuffer should be SubUpdate mode
        //     var fluidParticlesGraphicsNative = FluidDensityFieldRendererFeature.BeginWriteBuffer(0,FluidParticleCount);
        //     var fluidParticlesDataNtvArray = _fluidParticlesNtvArray.GetSubArray(0,FluidParticleCount);
        //     for (int i = 0; i < FluidParticleCount; i++)
        //     {
        //         var fluidParticle = fluidParticlesDataNtvArray[i];
        //         fluidParticle.Update(deltatime, fluidParticlesGraphicsNative);
        //         fluidParticlesDataNtvArray[i] = fluidParticle;
        //         
        //     }
        //     FluidDensityFieldRendererFeature.EndWriteBuffer(FluidParticleCount);
        // }

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
            AddMultiple(positions, count);
        }
        
        public static void Clean()
        {
            if (FluidParticleCount == 0) return;
            var fluidParticlesGraphicsNative = BeginWriteBuffer<FluidParticleGraphics>(FluidComputeBufferType.Graphics,0,FluidParticleCount);
            var fluidParticlesDataNtvArray = _fluidParticlesNtvArray.GetSubArray(0,FluidParticleCount);
            for (int i = 0; i < FluidParticleCount; i++)
            {
                var fluidParticle = fluidParticlesDataNtvArray[i];
                fluidParticle = CleanParticle();
                fluidParticlesDataNtvArray[i] = fluidParticle;
                
                var fluidParticleGraphics = fluidParticlesGraphicsNative[i];
                fluidParticleGraphics.position = fluidParticle.position;
                // fluidParticleGraphics.color = fluidParticle.color;
                fluidParticlesGraphicsNative[i] = fluidParticleGraphics;
            }
            EndWriteBuffer(FluidComputeBufferType.Graphics,FluidParticleCount);
            SetParticleCount(0);
        }
        
        public static void Dispose()
        {
            _fluidParticlesNtvArray.Dispose();
            SetParticleCount(0);
        }
    }
}