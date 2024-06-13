using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FluidSimulation
{
    public static class Globals
    {
        public static int FluidParticleCount { get; private set; } = 0;

        public static void SetParticleCount(int count)
        {
            FluidDensityFieldRendererFeature.DensityFieldPass.UpdateParticleCount(FluidParticleCount, count);
            FluidParticleCount = count;
        }
    }
    
    public struct FluidParticlePhysics
    {
        public int index;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;
    }

    public struct FluidParticleGraphics
    {
        public Vector3 position;
        // public Vector3 color;
    }
    
    public static class FluidUtilities
    {
        public static int MAX_FLUIDPOINT_COUNT = 6553500;
        public static Mesh CreateQuad(float width = 1f, float height = 1f) {
            // Create a quad mesh.
            var mesh = new Mesh();

            float w = width * .5f;
            float h = height * .5f;
            var vertices = new Vector3[4] {
                new Vector3(-w, -h, 0),
                new Vector3(w, -h, 0),
                new Vector3(-w, h, 0),
                new Vector3(w, h, 0)
            };

            var tris = new int[6] {
                // lower left tri.
                0, 2, 1,
                // lower right tri
                2, 3, 1
            };

            var normals = new Vector3[4] {
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
            };

            var uv = new Vector2[4] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
            };

            mesh.vertices = vertices;
            mesh.triangles = tris;
            mesh.normals = normals;
            mesh.uv = uv;

            return mesh;
        }

    }
}