using FluidSimulation.Core;
using FluidSimulation.Rendering;
using UnityEngine;

namespace FluidSimulation.Simulation
{
    /// <summary>
    /// 粒子生成器 — 负责创建、批量添加和清除粒子
    /// 通过FluidRenderFeature的DensityFieldPass写入GPU缓冲区
    /// </summary>
    public static class ParticleSpawner
    {
        public static void Init()
        {
            FluidState.SetParticleCount(0);
        }

        public static void Dispose()
        {
            FluidState.SetParticleCount(0);
        }

        #region 单个/批量添加

        public static void Add(Vector3 position)
        {
            var buffer = FluidRenderFeature.DensityFieldPass.BeginWriteBuffer<FluidParticlePhysics>(
                FluidRenderFeature.ComputeBufferType.Physics, FluidState.ParticleCount, 1);

            buffer[0] = CreateParticle(position, FluidState.ParticleCount);

            FluidRenderFeature.DensityFieldPass.EndWriteBuffer(FluidRenderFeature.ComputeBufferType.Physics, 1);
            FluidState.SetParticleCount(FluidState.ParticleCount + 1);
        }

        public static void AddMultiple(Vector3[] positions, int count)
        {
            int available = FluidConstants.MAX_PARTICLE_COUNT - FluidState.ParticleCount;
            if (available <= 0) return;
            count = Mathf.Min(count, available);

            var buffer = FluidRenderFeature.DensityFieldPass.BeginWriteBuffer<FluidParticlePhysics>(
                FluidRenderFeature.ComputeBufferType.Physics, FluidState.ParticleCount, count);

            for (int i = 0; i < count; i++)
                buffer[i] = CreateParticle(positions[i], FluidState.ParticleCount + i);

            FluidRenderFeature.DensityFieldPass.EndWriteBuffer(FluidRenderFeature.ComputeBufferType.Physics, count);
            FluidState.SetParticleCount(FluidState.ParticleCount + count);
        }

        #endregion

        #region 高级生成模式

        public static void AddAroundCursor(Vector3 normalizedPos, float radius, int count)
        {
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var pos = normalizedPos * 2 - Vector3.one;
                pos.y *= -1;
                pos += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0) * radius;
                positions[i] = pos;
            }
            AddMultiple(positions, count);
        }

        public static void FillScreen(float pixelWidth, float pixelHeight, float density)
        {
            int w = (int)(pixelWidth / density);
            int h = (int)(pixelHeight / density);
            int count = (w + 1) * (h + 1);
            var positions = new Vector3[count];

            for (int i = 0; i <= w; i++)
            for (int j = 0; j <= h; j++)
            {
                var pos = new Vector3((float)i / w, (float)j / h);
                positions[i * (h + 1) + j] = pos * 2 - Vector3.one;
            }
            AddMultiple(positions, count);
        }

        public static void FillScreenCenter(float squareSize, float screenWidth, float screenHeight, float density)
        {
            int side = (int)(squareSize / density);
            int count = side * side;
            var positions = new Vector3[count];

            for (int i = 0; i < side; i++)
            for (int j = 0; j < side; j++)
            {
                var pos = new Vector3((float)i / side, (float)j / side) * 2 - Vector3.one;
                pos.x *= squareSize / screenWidth;
                pos.y *= squareSize / screenHeight;
                positions[i * side + j] = pos;
            }
            AddMultiple(positions, count);
        }

        public static void FillScreenRandom(int count)
        {
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
                positions[i] = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0);
            AddMultiple(positions, count);
        }

        #endregion

        #region 清除

        public static void Clean()
        {
            if (FluidState.ParticleCount == 0) return;

            var buffer = FluidRenderFeature.DensityFieldPass.BeginWriteBuffer<FluidParticlePhysics>(
                FluidRenderFeature.ComputeBufferType.Physics, 0, FluidState.ParticleCount);

            for (int i = 0; i < FluidState.ParticleCount; i++)
                buffer[i] = default;

            FluidRenderFeature.DensityFieldPass.EndWriteBuffer(
                FluidRenderFeature.ComputeBufferType.Physics, FluidState.ParticleCount);
            FluidState.SetParticleCount(0);
        }

        #endregion

        #region 内部方法

        private static FluidParticlePhysics CreateParticle(Vector3 position, int index)
        {
            return new FluidParticlePhysics
            {
                index = index,
                gridID = -1,
                position = new Vector3(position.x, position.y, 1f), // z=1 保持2D
                velocity = Vector3.zero,
                acceleration = Vector3.zero,
                color = Vector4.one,
                density = 0,
                pressure = 0
            };
        }

        #endregion
    }
}
