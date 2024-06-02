using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FluidSimulation
{
    public static class FluidUtilities
    {
        public static uint MAX_FLUIDPOINT_COUNT = 65535;
        public struct FluidPoint
        {
            public Vector3 Position;
            public Vector3 Color;

            public FluidPoint(Vector3 position, Vector3 color)
            {
                Position = position;
                Color = color;
            }
        };
    }
}