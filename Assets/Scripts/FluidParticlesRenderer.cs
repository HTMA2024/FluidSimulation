using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static FluidSimulation.FluidUtilities;
using static FluidSimulation.FluidParticlePhysics;

namespace FluidSimulation
{
    public struct FluidParticleGraphics
    {
        public Vector3 position;
        public Vector3 color;
    }
    public abstract class FluidParticlesRenderer 
    {
        private static Mesh _mesh;
        private Shader m_DrawParticlesShader;

        private static ComputeBuffer _computeBuffer;
        private static ComputeBuffer _argsBuffer;
        private static uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        private static Material _particleMaterial;
        private static Material _densityMaterial;
        private static Bounds _bounds;
        private static float _pRadius;
        private static float _dRadius;
        private static Color _dColor;
        private static Camera _Camera;
        private static RenderTexture _rt;

        internal static NativeArray<FluidParticleGraphics> fluidParticleGraphicsNative;

        internal static ComputeBuffer computeBuffer => _computeBuffer;
        internal static int GetFluidParticleCount() => fluidParticleCount;
        
        internal static void SetParticleRadius(float radius)
        {
            _pRadius = radius;
        }
        internal static void SetDensityRadius(float radius)
        {
            _dRadius = radius;
        }

        internal static void SetCamera(Camera camera)
        {
            _Camera = camera;
        }

        internal static RenderTexture GetRenderTexture()
        {
            return _rt;
        }

        internal static void SetDensityColor(Color color)
        {
            _dColor = color;
        }        
        internal static void Initialize(Shader particleShader, Shader densityMaterial)
        {
            RenderTextureDescriptor renderTextureDescriptor = new RenderTextureDescriptor();
            renderTextureDescriptor.width = _Camera.pixelWidth;
            renderTextureDescriptor.height = _Camera.pixelHeight;
            renderTextureDescriptor.dimension = TextureDimension.Tex2D;
            renderTextureDescriptor.volumeDepth = 1;
            renderTextureDescriptor.useMipMap = false;
            renderTextureDescriptor.graphicsFormat = GraphicsFormat.R16_SFloat;
            renderTextureDescriptor.bindMS = false;
            renderTextureDescriptor.msaaSamples = 1;
            renderTextureDescriptor.depthStencilFormat = GraphicsFormat.None;
            renderTextureDescriptor.useDynamicScale = false;
        
            _rt = RenderTexture.GetTemporary(renderTextureDescriptor);
            
            _particleMaterial = new Material(particleShader);
            _densityMaterial = new Material(densityMaterial);
            _computeBuffer = new ComputeBuffer(MAX_FLUIDPOINT_COUNT, Marshal.SizeOf(typeof(FluidParticleGraphics)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            
            _particleMaterial.SetBuffer("_ComputeBuffer", _computeBuffer);
            _densityMaterial.SetBuffer("_ComputeBuffer", _computeBuffer);
            
            _mesh = CreateQuad(1,1);
            args[0] = (uint)_mesh.GetIndexCount(0);
            args[1] = (uint)GetFluidParticleCount();
            args[2] = (uint)_mesh.GetIndexStart(0);
            args[3] = (uint)_mesh.GetBaseVertex(0);
            
            _argsBuffer.SetData(args);
            _bounds = new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f));
        }

        internal static void BeginWriteBuffer(int startIndex, int count)
        {
            fluidParticleGraphicsNative = _computeBuffer.BeginWrite<FluidParticleGraphics>(startIndex, count);
        }
        internal static void EndWriteBuffer(int count)
        {
            _computeBuffer.EndWrite<FluidParticleGraphics>(count);
        }

        internal static void UpdateParticle(FluidParticle fluidParticle, int index)
        {
            var fluidParticleGraphics = fluidParticleGraphicsNative[index];
            fluidParticleGraphics.position = fluidParticle.position;
            fluidParticleGraphics.color = Vector3.one;
            fluidParticleGraphicsNative[index] = fluidParticleGraphics;
        }


        internal static void ExecuteRender()
        {
            if (fluidParticleCount == 0) return;
            args[1] = (uint)fluidParticleCount;;
            _argsBuffer.SetData(args);
            
            _particleMaterial.SetPass(0);
            _densityMaterial.SetPass(0);
            _particleMaterial.SetFloat("_ParticleRadius", _pRadius);
            _densityMaterial.SetFloat("_DensityRadius", _dRadius);
            _densityMaterial.SetColor("_Color", _dColor);

            Graphics.SetRenderTarget(_rt);
            GL.Clear(false, true, Color.black);
            Graphics.DrawMeshInstancedIndirect(_mesh, 0, _particleMaterial, _bounds, _argsBuffer);
            Graphics.DrawMeshInstancedIndirect(_mesh, 0, _densityMaterial, _bounds, _argsBuffer);
        }


        public static void Dispose()
        {
            _particleMaterial = null;
            _computeBuffer?.Release();
            _argsBuffer?.Dispose();
        }
    }
}