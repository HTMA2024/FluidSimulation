using Unity.Mathematics;
using UnityEngine;
using FluidSimulation.Core;
using FluidSimulation.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FluidSimulation.Simulation
{
    /// <summary>
    /// 流体模拟控制器 — MonoBehaviour入口
    /// 职责：接收用户输入、同步Inspector参数到渲染管线、管理生命周期
    /// </summary>
    public class FluidSimulatorController : MonoBehaviour
    {
        [Header("粒子参数")]
        [SerializeField] private int m_Pixel = 190;
        [SerializeField][Range(1e-3f, 1)] private float m_ParticleRadius = 0.5f;
        [SerializeField][Range(1e-3f, 1)] private float m_SmoothingRadius = 0.5f;

        [Header("物理参数")]
        [SerializeField][Range(0.8f, 1)] private float m_EnergyDamping = 1;
        [SerializeField] private float m_Gravity = 1;
        [SerializeField][Range(1e-3f, 5000)] private float m_TargetDensity = 1f;
        [SerializeField][Range(1e-3f, 10)] private float m_PressureMultiplier = 1f;

        [Header("可视化颜色")]
        [SerializeField] private Color m_ParticleColor;
        [SerializeField] private Color m_UnderTargetCol;
        [SerializeField] private Color m_OverTargetCol;
        [SerializeField] private Color m_AroundTargetCol;

        [Header("渲染开关")]
        [SerializeField] private bool m_EnableUpdate;
        [SerializeField] private bool m_DrawGridDensityField;
        [SerializeField] private bool m_DrawGridPressureField;
        [SerializeField] private bool m_DrawVizDensityMap;
        [SerializeField] private bool m_DrawParticles;
        [SerializeField] private bool m_EnableDebug;

        [Header("鼠标交互")]
        [SerializeField] private float m_Radius = 0.1f;
        [SerializeField] private int m_AddCount = 500;

        private Camera m_Camera;
        private int m_FluidParticleCount;

        private void Awake()
        {
            m_Camera = GetComponent<Camera>();
            ParticleSpawner.Init();
        }

        private void Update()
        {
            if (!Input.GetMouseButton(0)) return;

            var normalizedPos = ((float3)Input.mousePosition / new Vector3(m_Camera.pixelWidth, m_Camera.pixelHeight, 1));
            FluidRenderFeature.DensityFieldPass.SetCursorPosition(normalizedPos);
            ParticleSpawner.AddAroundCursor(normalizedPos, m_Radius, m_AddCount);
        }

        private void OnValidate()
        {
            if (!FluidRenderFeature.IsPassCreated) return;

            FluidRenderFeature.SetParticleParams(m_ParticleRadius, m_ParticleColor, m_Gravity, m_EnergyDamping, m_Pixel);
            FluidRenderFeature.SetDensityRadius(m_SmoothingRadius);
            FluidRenderFeature.SetVizDensityParams(m_OverTargetCol, m_UnderTargetCol, m_AroundTargetCol, m_TargetDensity);
            FluidRenderFeature.SetPressureParams(m_TargetDensity, m_PressureMultiplier);

            FluidRenderFeature.EnableUpdate = m_EnableUpdate;
            FluidRenderFeature.ComputeShaderDebug = m_EnableDebug;
            FluidRenderFeature.DrawGridDensityField = m_DrawGridDensityField;
            FluidRenderFeature.DrawGridPressureField = m_DrawGridPressureField;
            FluidRenderFeature.DrawVizDensityMap = m_DrawVizDensityMap;
            FluidRenderFeature.DrawParticles = m_DrawParticles;
        }

        private void OnDestroy()
        {
            ParticleSpawner.Dispose();
        }

        internal void UpdateInfo()
        {
            m_FluidParticleCount = FluidState.ParticleCount;
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(FluidSimulatorController))]
        private class FluidSimulatorControllerEditor : Editor
        {
            private Vector2 m_ParticlePosition;
            private int m_SquareSize = 500;
            private int m_Density = 10;
            private int m_Count = 100;

            public override void OnInspectorGUI()
            {
                var boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

                EditorGUI.BeginChangeCheck();
                DrawDefaultInspector();
                var t = (FluidSimulatorController)target;
                t.UpdateInfo();

                GUILayout.Space(20);
                EditorGUILayout.BeginVertical(new GUIStyle("Box"));
                EditorGUILayout.LabelField("调试工具", boldStyle);

                GUILayout.Space(10);
                m_ParticlePosition = EditorGUILayout.Vector2Field("Position", m_ParticlePosition);

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Particle", GUILayout.Width(150)))
                    ParticleSpawner.Add(m_ParticlePosition);
                if (GUILayout.Button("Clean", GUILayout.Width(100)))
                    ParticleSpawner.Clean();
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                m_Density = EditorGUILayout.IntField("Density", m_Density);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fill Screen", GUILayout.Width(100)))
                    ParticleSpawner.FillScreen(t.m_Camera.pixelWidth, t.m_Camera.pixelHeight, m_Density);
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                m_Count = EditorGUILayout.IntField("Count", m_Count);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fill Random", GUILayout.Width(200)))
                    ParticleSpawner.FillScreenRandom(m_Count);
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                m_SquareSize = EditorGUILayout.IntField("SquareSize", m_SquareSize);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fill Center", GUILayout.Width(200)))
                    ParticleSpawner.FillScreenCenter(m_SquareSize, t.m_Camera.pixelWidth, t.m_Camera.pixelHeight, m_Density);
                GUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical(new GUIStyle("Box"));
                EditorGUILayout.LabelField($"Particle Radius: {t.m_ParticleRadius}");
                EditorGUILayout.LabelField($"Smoothing Radius: {t.m_SmoothingRadius}");
                EditorGUILayout.LabelField($"Particle Count: {t.m_FluidParticleCount}");
                EditorGUILayout.EndVertical();
            }
        }
#endif
    }
}
