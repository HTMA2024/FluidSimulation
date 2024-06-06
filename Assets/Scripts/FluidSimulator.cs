using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FluidSimulation
{
    public class FluidSimulator : MonoBehaviour
    {
        [SerializeField] private Shader m_DrawParticlesShader;
        [SerializeField] private Shader m_DrawDensityShader;
        [SerializeField][Range(0,1)] private float m_ParticleRadius = 0.5f;
        [SerializeField][Range(0,1)] private float m_DensityRadius = 0.5f;
        [SerializeField] private bool m_EnableUpdate = false;

        [SerializeField] private Color m_DensityColor = Color.white;
        [SerializeField] private RenderTexture m_RenderTexture;
            
        private Camera m_Camera;
        private int m_FluidParticleCount = 0;

        private FluidParticlesRenderer m_ParticlesRenderer;

        private CommandBuffer m_FluidParticleCB;
        public CameraEvent cameraEvent = CameraEvent.AfterLighting;
        private int m_DrawParticlesRTID = -1;

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginFrameRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginFrameRendering;
        }

        private void Awake()
        {
            if (m_DrawParticlesShader == null) return;
            m_Camera = this.GetComponent<Camera>();
            FluidParticlePhysics.Init();
            FluidParticlesRenderer.SetCamera(m_Camera);
            FluidParticlesRenderer.Initialize(m_DrawParticlesShader, m_DrawDensityShader);
        }

        private void Update()
        {
            if (m_EnableUpdate)
            {
                FluidParticlesRenderer.BeginWriteBuffer(0, FluidParticlePhysics.fluidParticleCount);
                
                FluidParticlePhysics.Update(Time.deltaTime);
                
                FluidParticlesRenderer.EndWriteBuffer(FluidParticlePhysics.fluidParticleCount);
            }
        }


        private void OnValidate()
        {
            FluidParticlesRenderer.SetParticleRadius(m_ParticleRadius);
            FluidParticlesRenderer.SetDensityRadius(m_DensityRadius);
            FluidParticlesRenderer.SetDensityColor(m_DensityColor);
        }


        private void UpdateInfo()
        {
            m_FluidParticleCount = FluidParticlesRenderer.GetFluidParticleCount();
            m_RenderTexture = FluidParticlesRenderer.GetRenderTexture();
        }
        
        void OnBeginFrameRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_Camera) return;
            FluidParticlesRenderer.ExecuteRender();
        }

        void AddParticle(Vector3 position, Color color)
        {
            FluidParticlePhysics.Add(FluidParticlesRenderer.computeBuffer ,position, color);
        }

        void CleanParticles()
        {
            FluidParticlePhysics.Clean(FluidParticlesRenderer.computeBuffer);
        }

        private void FillScreen(int density)
        {
            FluidParticlePhysics.FillScreen(m_Camera.pixelWidth,m_Camera.pixelHeight,density);
        }

        private void OnDestroy()
        {
            FluidParticlePhysics.Dispose();
            FluidParticlesRenderer.Dispose();
        }


#if UNITY_EDITOR
        [CustomEditor(typeof(FluidSimulator))]
        private class FluidSimulatorEditor : Editor
        {
            private Vector2 m_ParticlePosition;
            private Color m_CircleColor = Color.white;
            private int m_Density = 100;
            public override void OnInspectorGUI()
            {
                // Create a new GUIStyle for the bold label
                GUIStyle boldStyle = new GUIStyle(GUI.skin.label);
                boldStyle.fontStyle = FontStyle.Bold;
                
                
                EditorGUI.BeginChangeCheck();
                DrawDefaultInspector();
                var t = (FluidSimulator)target;
                t.UpdateInfo();

                GUILayout.Space(20);  // Adjust the value to increase or decrease the spacing
                EditorGUILayout.BeginVertical(new GUIStyle("Box"));
                
                EditorGUILayout.LabelField("Debug",boldStyle);
                
                GUILayout.Space(10);  
                m_ParticlePosition = EditorGUILayout.Vector2Field("Position", m_ParticlePosition);
                
                GUILayout.Space(10);  
                m_CircleColor = EditorGUILayout.ColorField("Color", m_CircleColor);
                
                GUILayout.Space(10);  
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Fluid Particle", GUILayout.Width(150)))
                {
                    t.AddParticle(m_ParticlePosition, m_CircleColor);
                }
                if (GUILayout.Button("Clean", GUILayout.Width(100)))
                {
                    t.CleanParticles();
                }
                GUILayout.EndHorizontal();
                
                GUILayout.Space(10);  
                GUILayout.BeginHorizontal();
                m_Density = EditorGUILayout.IntField("Density", m_Density);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fill Screen", GUILayout.Width(100)))
                {
                    t.FillScreen(m_Density);
                }
                GUILayout.EndHorizontal();
                
                GUILayout.Space(10);  
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Update A Frame"))
                {
                    FluidParticlePhysics.Update(1);
                }
                GUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.BeginVertical(new GUIStyle("Box"));
                EditorGUILayout.LabelField($"Fluid Point Radius: {t.m_ParticleRadius}");
                EditorGUILayout.LabelField($"Fluid Point Radius: {t.m_DensityRadius}");
                EditorGUILayout.LabelField($"FluidPointCount: {t.m_FluidParticleCount}");
                EditorGUILayout.EndVertical();

            }
        }
#endif
        
    }
}