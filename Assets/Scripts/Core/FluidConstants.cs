using UnityEngine;

namespace FluidSimulation.Core
{
    /// <summary>
    /// 流体模拟全局常量定义
    /// </summary>
    public static class FluidConstants
    {
        /// <summary>最大粒子数量 (必须是2的幂次，用于Bitonic排序)</summary>
        public const int MAX_PARTICLE_COUNT = 131072;

        /// <summary>最大网格数量</summary>
        public const int MAX_GRID_COUNT = 2048;

        /// <summary>Compute Shader线程组大小</summary>
        public const int THREAD_GROUP_SIZE = 64;

        /// <summary>固定物理时间步长</summary>
        public const float FIXED_DELTA_TIME = 0.005f;

        /// <summary>边界缓冲距离</summary>
        public const float BOUNDARY_MARGIN = 0.02f;
    }
}
