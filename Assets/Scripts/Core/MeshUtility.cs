using UnityEngine;

namespace FluidSimulation.Core
{
    /// <summary>
    /// 网格工具类 — 生成GPU Instancing所需的基础Quad网格
    /// </summary>
    public static class MeshUtility
    {
        public static Mesh CreateQuad(float width = 1f, float height = 1f)
        {
            float w = width * 0.5f;
            float h = height * 0.5f;

            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-w, -h, 0), new Vector3(w, -h, 0),
                    new Vector3(-w, h, 0), new Vector3(w, h, 0)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
                normals = new[]
                {
                    -Vector3.forward, -Vector3.forward,
                    -Vector3.forward, -Vector3.forward
                },
                uv = new[]
                {
                    new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(0, 1), new Vector2(1, 1)
                }
            };

            return mesh;
        }
    }
}
