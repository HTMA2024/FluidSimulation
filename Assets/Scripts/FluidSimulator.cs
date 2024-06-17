using System;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static FluidSimulation.Globals;

namespace FluidSimulation
{
    public class FluidSimulator : MonoBehaviour
    {
        [SerializeField] private int m_Pixel = 190;
        [SerializeField][Range(0,40)] private int m_Selector = 0;
        [SerializeField][Range(1e-3f,1)] private float m_ParticleRadius = 0.5f;
        [SerializeField][Range(1e-3f,1)] private float m_SmoothingRadius = 0.5f;

        [SerializeField] [Range(0.8f, 1)] private float _energyDamping = 1;
        [SerializeField] private Color _particleColor;
        [SerializeField] private Color _underTargetCol;
        [SerializeField] private Color _overTargetCol;
        [SerializeField] private Color _aroundTargetCol;
        [SerializeField][Range(1e-3f,3)] private float _targetDensity = 1f;
        [SerializeField][Range(1e-3f,10)] private float _pressureMultiplier = 1f;
        
        [SerializeField] private bool m_EnableUpdate = false;
        [SerializeField] private bool m_DrawDensityField = false;
        [SerializeField] private bool m_DrawGridDensityField = false;
        [SerializeField] private bool m_DrawGridPressureField = false;
        [SerializeField] private bool m_DrawVizDensityMap = false;
        [SerializeField] private bool m_DrawGradientField = false;
        [SerializeField] private bool m_DrawPressureField = false;
        [SerializeField] private bool m_DrawParticles = false;

        [SerializeField] private RTHandle m_RenderTexture;
            
        private Camera m_Camera;
        private int m_FluidParticleCount = 0;


        private void Awake()
        {
            m_Camera = this.GetComponent<Camera>();
            FluidParticlePhysicsSystem.Init();
        }

        private void Update()
        {
            if (Input.GetMouseButton(0))
            {
                FluidDensityFieldRendererFeature.DensityFieldPass.SetCursorPosition(Input.mousePosition/ new float3(m_Camera.pixelWidth,m_Camera.pixelHeight,1), m_Selector);
            }
        }


        private void OnValidate()
        {
            if (FluidDensityFieldRendererFeature.passCreated)
            {
                FluidDensityFieldRendererFeature.SetParticleParams(m_ParticleRadius, _particleColor, _energyDamping, m_Pixel);
                FluidDensityFieldRendererFeature.SetDensityRadius(m_SmoothingRadius);
                FluidDensityFieldRendererFeature.SetVizDensityParams(_overTargetCol, _underTargetCol,_aroundTargetCol, _targetDensity);
                FluidDensityFieldRendererFeature.SetPressureParams(_targetDensity, _pressureMultiplier);
                FluidDensityFieldRendererFeature.enableUpdate = m_EnableUpdate;
                FluidDensityFieldRendererFeature.drawBlendDensityField = m_DrawDensityField;
                FluidDensityFieldRendererFeature.drawGridDensityField = m_DrawGridDensityField;
                FluidDensityFieldRendererFeature.drawGridPressureField = m_DrawGridPressureField;
                
                FluidDensityFieldRendererFeature.drawVizDensityMap = m_DrawVizDensityMap;
                FluidDensityFieldRendererFeature.drawGradientField = m_DrawGradientField;
                FluidDensityFieldRendererFeature.drawPressureField = m_DrawPressureField;
                FluidDensityFieldRendererFeature.drawParticles = m_DrawParticles;
            }
        }


        private void UpdateInfo()
        {
            m_FluidParticleCount = FluidParticleCount;
            // m_RenderTexture = FluidDensityFieldRendererFeature.GetRenderTexture();
        }

        void AddParticle(Vector3 position)
        {
            FluidParticlePhysicsSystem.Add(position);
        }

        void CleanParticles()
        {
            FluidParticlePhysicsSystem.Clean();
        }

        private void FillScreenCenter(int squareSize ,int density)
        {
            FluidParticlePhysicsSystem.FillScreenCenter(squareSize,m_Camera.pixelWidth, m_Camera.pixelHeight,density);
        }
        
        private void FillScreen(int density)
        {
            FluidParticlePhysicsSystem.FillScreen(m_Camera.pixelWidth,m_Camera.pixelHeight,density);
        }

        private void FillScreenRandom(int count)
        {
            FluidParticlePhysicsSystem.FillScreenRandom(count);
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
            private Color m_ParticleColor = Color.white;
            private int m_SquareSize = 500;
            private int m_Density = 10;
            private int m_Count = 100;
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
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Fluid Particle", GUILayout.Width(150)))
                {
                    t.AddParticle(m_ParticlePosition);
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
                m_Count = EditorGUILayout.IntField("Count", m_Count);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fill Screen Random", GUILayout.Width(200)))
                {
                    t.FillScreenRandom(m_Count);
                }
                GUILayout.EndHorizontal();
                
                
                GUILayout.Space(10);  
                GUILayout.BeginHorizontal();
                m_SquareSize = EditorGUILayout.IntField("SquareSize", m_SquareSize);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fill Screen Center", GUILayout.Width(200)))
                {
                    t.FillScreenCenter(m_SquareSize, m_Density);
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