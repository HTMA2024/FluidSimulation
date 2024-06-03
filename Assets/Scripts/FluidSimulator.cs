using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FluidSimulation
{
    public class FluidSimulator : MonoBehaviour
    {
        [SerializeField] private Shader m_DrawParticlesShader;
        [SerializeField] private float m_Radius = 0.5f;
        [SerializeField] private bool m_EnableUpdate = false;
        
        private Camera m_Camera;
        private int m_FluidParticleCount = 0;

        private FluidParticlesRenderer m_ParticlesRenderer;

        private void OnEnable()
        {
            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
        }

        private void Awake()
        {
            if (m_DrawParticlesShader == null) return;
            m_Camera = this.GetComponent<Camera>();
            FluidParticlePhysics.Init();
            FluidParticlesRenderer.Initialize(m_DrawParticlesShader);
        }

        private void Update()
        {
            if (m_EnableUpdate)
            {
                FluidParticlePhysics.Update(FluidParticlesRenderer.computeBuffer, Time.deltaTime);
            }
        }


        private void OnValidate()
        {
            FluidParticlesRenderer.SetRadius(m_Radius);
        }


        private void UpdateInfo()
        {
            m_FluidParticleCount = FluidParticlesRenderer.GetFluidParticleCount();
        }
        
        void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
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
            var width =  (int) (m_Camera.pixelWidth / (float)density);
            var height = (int) (m_Camera.pixelHeight / (float)density);
            var count = (int)(width + 1) * (int)(height + 1);

            var positions = new Vector3[count];
            
            for (int i = 0; i <= width; i++)
            {
                for (int j = 0; j <= height; j++)
                {
                    Vector3 position = new Vector3((float)i/width, (float)j/height);
                    position = position * 2 - Vector3.one;
                    var index = (int)(i * (height + 1) + j);
                    positions[index] = position;
                }
            }
            
            // FluidParticleSystem.AddFluidParticles(positions, count);
            // FluidParticlesRenderer.UpdateBuffers();
            FluidParticlePhysics.AddMultiple(FluidParticlesRenderer.computeBuffer, positions, count);
        }

        private void OnDestroy()
        {
            FluidParticlePhysics.Dispose();
            FluidParticlesRenderer.Dispose();
        }


#if UNITY_EDITOR
        [CustomEditor(typeof(FluidSimulator))]
        private class RandomParticlesGeneratorEditor : Editor
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
                    FluidParticlePhysics.Update(FluidParticlesRenderer.computeBuffer, 1);
                }
                GUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.BeginVertical(new GUIStyle("Box"));
                EditorGUILayout.LabelField($"FluidPointRadius: {t.m_Radius}");
                EditorGUILayout.LabelField($"FluidPointCount: {t.m_FluidParticleCount}");
                EditorGUILayout.EndVertical();

            }
        }
#endif
        
    }
}