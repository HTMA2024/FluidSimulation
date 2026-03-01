using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace FluidSimulation.Sorting
{
    /// <summary>
    /// GPU Bitonic排序 — 用于空间哈希网格的粒子排序
    /// 基于经典的Bitonic Merge Sort算法在GPU上并行执行
    /// </summary>
    internal static class BitonicSort
    {
        private const int KERNEL_BITONIC_SORT = 0;
        private const int KERNEL_TRANSPOSE = 1;
        private const uint BITONIC_BLOCK_SIZE = 512;
        private const uint TRANSPOSE_BLOCK_SIZE = 16;

        /// <summary>
        /// 在GPU上对缓冲区执行Bitonic排序
        /// </summary>
        /// <param name="cmd">CommandBuffer</param>
        /// <param name="bufferSize">缓冲区大小 (必须是BITONIC_BLOCK_SIZE的整数倍)</param>
        /// <param name="bitonicCS">Bitonic排序Compute Shader</param>
        /// <param name="inBuffer">输入/输出缓冲区</param>
        /// <param name="tempBuffer">临时缓冲区 (用于矩阵转置)</param>
        internal static void GPUSort(CommandBuffer cmd, int bufferSize, ComputeShader bitonicCS,
            ComputeBuffer inBuffer, ComputeBuffer tempBuffer)
        {
            uint numElements = (uint)bufferSize;
            uint matrixWidth = BITONIC_BLOCK_SIZE;
            uint matrixHeight = numElements / BITONIC_BLOCK_SIZE;

            // 第一阶段: 块内排序 (level <= BITONIC_BLOCK_SIZE)
            for (uint level = 2; level <= BITONIC_BLOCK_SIZE; level <<= 1)
            {
                SetSortConstants(cmd, bitonicCS, level, level, matrixHeight, matrixWidth);
                cmd.SetComputeBufferParam(bitonicCS, KERNEL_BITONIC_SORT, "Data", inBuffer);
                cmd.DispatchCompute(bitonicCS, KERNEL_BITONIC_SORT,
                    Mathf.CeilToInt(numElements / (float)BITONIC_BLOCK_SIZE), 1, 1);
            }

            // 第二阶段: 跨块排序 (level > BITONIC_BLOCK_SIZE)
            // 使用矩阵转置技巧实现跨共享内存边界的比较
            for (uint level = (BITONIC_BLOCK_SIZE << 1); level <= numElements; level <<= 1)
            {
                // 转置 → 排序列 → 转置回 → 排序行
                SetSortConstants(cmd, bitonicCS,
                    level / BITONIC_BLOCK_SIZE, (level & ~numElements) / BITONIC_BLOCK_SIZE,
                    matrixWidth, matrixHeight);
                cmd.SetComputeBufferParam(bitonicCS, KERNEL_TRANSPOSE, "Input", inBuffer);
                cmd.SetComputeBufferParam(bitonicCS, KERNEL_TRANSPOSE, "Data", tempBuffer);
                cmd.DispatchCompute(bitonicCS, KERNEL_TRANSPOSE,
                    Mathf.CeilToInt(matrixWidth / (float)TRANSPOSE_BLOCK_SIZE),
                    Mathf.CeilToInt(matrixHeight / (float)TRANSPOSE_BLOCK_SIZE), 1);

                cmd.SetComputeBufferParam(bitonicCS, KERNEL_BITONIC_SORT, "Data", tempBuffer);
                cmd.DispatchCompute(bitonicCS, KERNEL_BITONIC_SORT,
                    Mathf.CeilToInt(numElements / (float)BITONIC_BLOCK_SIZE), 1, 1);

                SetSortConstants(cmd, bitonicCS, BITONIC_BLOCK_SIZE, level, matrixHeight, matrixWidth);
                cmd.SetComputeBufferParam(bitonicCS, KERNEL_TRANSPOSE, "Input", tempBuffer);
                cmd.SetComputeBufferParam(bitonicCS, KERNEL_TRANSPOSE, "Data", inBuffer);
                cmd.DispatchCompute(bitonicCS, KERNEL_TRANSPOSE,
                    Mathf.CeilToInt(matrixHeight / (float)TRANSPOSE_BLOCK_SIZE),
                    Mathf.CeilToInt(matrixWidth / (float)TRANSPOSE_BLOCK_SIZE), 1);

                cmd.SetComputeBufferParam(bitonicCS, KERNEL_BITONIC_SORT, "Data", inBuffer);
                cmd.DispatchCompute(bitonicCS, KERNEL_BITONIC_SORT,
                    Mathf.CeilToInt(numElements / (float)BITONIC_BLOCK_SIZE), 1, 1);
            }
        }

        private static void SetSortConstants(CommandBuffer cmd, ComputeShader cs,
            uint level, uint levelMask, uint width, uint height)
        {
            cmd.SetComputeIntParam(cs, "_Level", (int)level);
            cmd.SetComputeIntParam(cs, "_LevelMask", (int)levelMask);
            cmd.SetComputeIntParam(cs, "_Width", (int)width);
            cmd.SetComputeIntParam(cs, "_Height", (int)height);
        }
    }
}
