using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine;
using UnityEngine.Serialization;

namespace FluidSimulation
{
    public class RandomPointsGenerator : MonoBehaviour
    {
        [SerializeField] private Shader m_DrawCirclesShader;
        [SerializeField] private Material m_DrawCirclesMat;
        [SerializeField] private float m_Radius = 0.5f;
            
        private Mesh m_FullScreenTriangle;
        private int m_FluidPointCount = 0;

        
        private void Awake()
        {
            if (m_DrawCirclesShader == null) return;
            CirclesRenderer.Initialize(m_DrawCirclesShader);
            m_DrawCirclesMat = CirclesRenderer._material;
        }


        private void LateUpdate()
        {
            UpdateInfo();
        }

        private void UpdateInfo()
        {
            m_FluidPointCount = CirclesRenderer.GetFluidPointCount();
        }

        private void DrawCircle(Vector3 circlePosRadius, Vector3 color)
        {
            CirclesRenderer.AddFluidPoint(circlePosRadius, color);
        }
        
        void OnRenderObject()
        {
            if (m_FluidPointCount == 0) return;
            CirclesRenderer._material.SetPass(0);
            CirclesRenderer._material.SetFloat("_CircleRadius", m_Radius);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, m_FluidPointCount);
        }

        void CleanCircles()
        {
            CirclesRenderer.Clean();
        }


#if UNITY_EDITOR
        [CustomEditor(typeof(RandomPointsGenerator))]
        private class RandomPointsGeneratorEditor : Editor
        {
            private Vector2 m_CirclePosition;
            public override void OnInspectorGUI()
            {
                EditorGUI.BeginChangeCheck();
                DrawDefaultInspector();
                var t = (RandomPointsGenerator)target;

                m_CirclePosition = EditorGUILayout.Vector2Field("Position", m_CirclePosition);
                
                
                // Create a horizontal layout to align the button to the right
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Space(10); 
                if (GUILayout.Button("Add One Circle", GUILayout.Width(100)))
                {
                    t.DrawCircle(m_CirclePosition, Vector3.one);
                }
                if (GUILayout.Button("Clean", GUILayout.Width(100)))
                {
                    t.CleanCircles();
                }
                GUILayout.EndHorizontal();
                
                EditorGUILayout.BeginVertical(new GUIStyle("Box"));
                EditorGUILayout.LabelField($"FluidPointRadius: {t.m_Radius}");
                EditorGUILayout.LabelField($"FluidPointCount: {t.m_FluidPointCount}");
                EditorGUILayout.EndVertical();

            }
        }
#endif
        
    }
}