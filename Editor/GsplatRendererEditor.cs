// Copyright (c) 2025 Yize Wu
// SPDX-License-Identifier: MIT

using UnityEditor;
using UnityEngine;

namespace Gsplat.Editor
{
    [CustomEditor(typeof(GsplatRenderer))]
    public class GsplatRendererEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "m_Script", nameof(GsplatRenderer.UploadBatchSize),
                nameof(GsplatRenderer.RenderBeforeUploadComplete));

             var renderer = (GsplatRenderer)target;

            // Sort Refresh Rate slider only if on correct mode
            if (renderer.SortMode == GsplatRenderer.GsplatSortMode.SortEachNFrames || renderer.SortMode == GsplatRenderer.GsplatSortMode.CutoutsEachNSorts)
            {
                var newSortRefreshRate = (uint)EditorGUILayout.IntSlider(new GUIContent("Sort Refresh Rate"), (int)renderer.SortRefreshRate, 1, 60);
                if (newSortRefreshRate != renderer.SortRefreshRate)
                {
                    renderer.SortRefreshRate = newSortRefreshRate;
                    renderer.ForceRefresh();
                }
            }

            // Cutouts Refresh Rate slider only if on correct mode
            if (renderer.SortMode == GsplatRenderer.GsplatSortMode.CutoutsEachNSorts)
            {
                var newCutoutsRefreshRate = (uint)EditorGUILayout.IntSlider(new GUIContent("Sort Refresh Rate"), (int)renderer.CutoutsRefreshRate, 1, 60);
                if (newCutoutsRefreshRate != renderer.CutoutsRefreshRate)
                {
                    renderer.CutoutsRefreshRate = newCutoutsRefreshRate;
                    renderer.ForceRefresh();
                }
            }

            // RenderOrder slider depend on the MaxRenderOrder setting
            if (GsplatSettings.Instance.MaxRenderOrder > 1)
                renderer.RenderOrder = (uint)EditorGUILayout.IntSlider(new GUIContent("Render Order"), (int)renderer.RenderOrder, 0, (int)GsplatSettings.Instance.MaxRenderOrder - 1);

            if (serializedObject.FindProperty(nameof(GsplatRenderer.AsyncUpload)).boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(GsplatRenderer.UploadBatchSize)));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty(nameof(GsplatRenderer.RenderBeforeUploadComplete)));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
