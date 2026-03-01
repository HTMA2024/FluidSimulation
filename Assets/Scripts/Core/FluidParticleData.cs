using Unity.Mathematics;
using UnityEngine;

namespace FluidSimulation.Core
{
    /// <summary>
    /// 粒子物理数据结构 — 与GPU端FluidParticle.hlsl中的结构体一一对应
    /// </summary>
    public struct FluidParticlePhysics
    {
        public int index;
        public int gridID;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;
        public Vector4 color;
        public float density;
        public float3 pressure;
    }

    /// <summary>
    /// 粒子图形数据结构 — 仅用于渲染的轻量数据
    /// </summary>
    public struct FluidParticleGraphics
    {
        public Vector3 position;
    }
}
