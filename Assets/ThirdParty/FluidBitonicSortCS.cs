using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace FluidSimulation
{
    internal interface  FluidBitonicSortCS
    {
        public const int KERNEL_ID_BITONICSORT = 0;
        public const int KERNEL_ID_TRANSPOSE_MATRIX = 1;
        
        public const uint BITONIC_BLOCK_SIZE = 512;
        public const uint TRANSPOSE_BLOCK_SIZE = 16;
        
        internal static void GPUSort(CommandBuffer cmd, int bufferSize, ComputeShader bitonicCS, ComputeBuffer inBuffer, ComputeBuffer tempBuffer)
        {
            int BUFFER_SIZE = bufferSize;
            
            ComputeShader shader = bitonicCS;
            // Determine parameters.
            uint NUM_ELEMENTS = (uint)BUFFER_SIZE;
            uint MATRIX_WIDTH = BITONIC_BLOCK_SIZE;
            uint MATRIX_HEIGHT = (uint)NUM_ELEMENTS / BITONIC_BLOCK_SIZE;

            // Sort the data
            // First sort the rows for the levels <= to the block size
            for (uint level = 2; level <= BITONIC_BLOCK_SIZE; level <<= 1)
            {
                SetGPUSortConstants(cmd, shader, level, level, MATRIX_HEIGHT, MATRIX_WIDTH);

                // Sort the row data
                cmd.SetComputeBufferParam(shader,KERNEL_ID_BITONICSORT, "Data", inBuffer);
                cmd.DispatchCompute(shader,KERNEL_ID_BITONICSORT,  Mathf.CeilToInt(NUM_ELEMENTS /(float) BITONIC_BLOCK_SIZE), 1, 1);
            }
            // Then sort the rows and columns for the levels > than the block size
            // Transpose. Sort the Columns. Transpose. Sort the Rows.
            for (uint level = (BITONIC_BLOCK_SIZE << 1); level <= NUM_ELEMENTS; level <<= 1)
            {
                // Transpose the data from buffer 1 into buffer 2
                SetGPUSortConstants(cmd, shader, (level / BITONIC_BLOCK_SIZE), (level & ~NUM_ELEMENTS) / BITONIC_BLOCK_SIZE, MATRIX_WIDTH, MATRIX_HEIGHT);
                cmd.SetComputeBufferParam(shader,KERNEL_ID_TRANSPOSE_MATRIX, "Input", inBuffer);
                cmd.SetComputeBufferParam(shader,KERNEL_ID_TRANSPOSE_MATRIX, "Data", tempBuffer);
                cmd.DispatchCompute(shader,KERNEL_ID_TRANSPOSE_MATRIX, Mathf.CeilToInt(MATRIX_WIDTH / (float)TRANSPOSE_BLOCK_SIZE), Mathf.CeilToInt(MATRIX_HEIGHT /(float) TRANSPOSE_BLOCK_SIZE), 1);

                // Sort the transposed column data
                cmd.SetComputeBufferParam(shader,KERNEL_ID_BITONICSORT, "Data", tempBuffer);
                cmd.DispatchCompute(shader,KERNEL_ID_BITONICSORT, Mathf.CeilToInt(NUM_ELEMENTS /(float) BITONIC_BLOCK_SIZE), 1, 1);

                // Transpose the data from buffer 2 back into buffer 1
                SetGPUSortConstants(cmd, shader, BITONIC_BLOCK_SIZE, level, MATRIX_HEIGHT, MATRIX_WIDTH);
                cmd.SetComputeBufferParam(shader,KERNEL_ID_TRANSPOSE_MATRIX, "Input", tempBuffer);
                cmd.SetComputeBufferParam(shader,KERNEL_ID_TRANSPOSE_MATRIX, "Data", inBuffer);
                cmd.DispatchCompute(shader,KERNEL_ID_TRANSPOSE_MATRIX, Mathf.CeilToInt(MATRIX_HEIGHT /(float) TRANSPOSE_BLOCK_SIZE), Mathf.CeilToInt(MATRIX_WIDTH /(float) TRANSPOSE_BLOCK_SIZE), 1);

                // Sort the row data
                cmd.SetComputeBufferParam(shader,KERNEL_ID_BITONICSORT, "Data", inBuffer);
                cmd.DispatchCompute(shader,KERNEL_ID_BITONICSORT, Mathf.CeilToInt(NUM_ELEMENTS /(float) BITONIC_BLOCK_SIZE), 1, 1);
            }
        }
        internal static void SetGPUSortConstants(CommandBuffer cmd ,ComputeShader cs, uint level, uint levelMask, uint width, uint height)
        {
            cmd.SetComputeIntParam(cs,"_Level", (int)level);
            cmd.SetComputeIntParam(cs,"_LevelMask", (int)levelMask);
            cmd.SetComputeIntParam(cs,"_Width", (int)width);
            cmd.SetComputeIntParam(cs,"_Height", (int)height);
        }
        
        internal static uint2[] GetValuesUint2(ComputeBuffer buffer)
        {
            if (buffer == null || buffer.count == 0) return null;
            var data = new uint2[buffer.count];
            buffer.GetData(data);
            return data;
        }
        
        internal static uint[] GetValuesInt(ComputeBuffer buffer)
        {
            if (buffer == null || buffer.count == 0) return null;
            var data = new uint[buffer.count];
            buffer.GetData(data);
            return data;
        }
    }
}