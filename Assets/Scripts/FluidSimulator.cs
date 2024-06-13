using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static FluidSimulation.Globals;

namespace FluidSimulation
{
    public class FluidSimulator : MonoBehaviour
    {
        [SerializeField][Range(0,1)] private float m_ParticleRadius = 0.5f;
        [SerializeField][Range(0,1)] private float m_SmoothingRadius = 0.5f;
        [SerializeField] private bool m_EnableUpdate = false;
        [SerializeField] private bool m_DrawDensityField = false;
        [SerializeField] private bool m_DrawGradientField = false;

        [SerializeField] private RTHandle m_RenderTexture;
            
        private Camera m_Camera;
        private int m_FluidParticleCount = 0;
        private int m_DrawParticlesRTID = -1;


        private void Awake()
        {
            m_Camera = this.GetComponent<Camera>();
            FluidParticlePhysicsSystem.Init();
        }

        private void Update()
        {
        }


        private void OnValidate()
        {
            if (FluidDensityFieldRendererFeature.passCreated)
            {
                FluidDensityFieldRendererFeature.SetParticleRadius(m_ParticleRadius);
                FluidDensityFieldRendererFeature.SetDensityRadius(m_SmoothingRadius);
                FluidDensityFieldRendererFeature.enableUpdate = m_EnableUpdate;
                FluidDensityFieldRendererFeature.drawDensityField = m_DrawDensityField;
                FluidDensityFieldRendererFeature.drawGradientField = m_DrawGradientField;
            }
        }


        private void UpdateInfo()
        {
            m_FluidParticleCount = FluidParticleCount;
            // m_RenderTexture = FluidDensityFieldRendererFeature.GetRenderTexture();
        }

        void AddParticle(Vector3 position, Color color)
        {
            FluidParticlePhysicsSystem.Add(position, color);
        }

        void CleanParticles()
        {
            FluidParticlePhysicsSystem.Clean();
        }

        private void FillScreen(int density)
        {
            FluidParticlePhysicsSystem.FillScreen(m_Camera.pixelWidth,m_Camera.pixelHeight,density);
        }

        private void OnDestroy()
        {
            FluidParticlePhysicsSystem.Dispose();
            // FluidDensityFieldRendererFeature.Dispose();
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
                    // FluidParticlePhysicsSystem.Update(1);
                }
                GUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.BeginVertical(new GUIStyle("Box"));
                EditorGUILayout.LabelField($"Fluid Point Radius: {t.m_ParticleRadius}");
                EditorGUILayout.LabelField($"Fluid Point Radius: {t.m_SmoothingRadius}");
                EditorGUILayout.LabelField($"FluidPointCount: {t.m_FluidParticleCount}");
                EditorGUILayout.EndVertical();

            }
        }
#endif
        
    }
}