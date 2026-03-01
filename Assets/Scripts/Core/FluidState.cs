namespace FluidSimulation.Core
{
    /// <summary>
    /// 流体模拟全局状态管理
    /// 集中管理粒子计数，并在计数变化时通知渲染管线同步GPU缓冲区
    /// </summary>
    public static class FluidState
    {
        public static int ParticleCount { get; private set; }

        /// <summary>
        /// 更新粒子计数，同时触发GPU缓冲区同步
        /// </summary>
        public static void SetParticleCount(int count)
        {
            var prevCount = ParticleCount;
            ParticleCount = count;
            Rendering.FluidRenderFeature.DensityFieldPass.SyncParticleCount(prevCount, count);
        }
    }
}
