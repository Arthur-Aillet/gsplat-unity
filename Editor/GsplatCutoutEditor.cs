// Copyright (c) 2026 Arthur Aillet
// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;

namespace Gsplat.Editor
{
    [CustomEditor(typeof(GsplatCutout))]
    public class GsplatCutoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var cutout = target as GsplatCutout;

            if (cutout == null)
                return;

            if (cutout.m_Target == GsplatCutout.Target.Parent && cutout.gameObject.transform.parent?.GetComponent<GsplatRenderer>() == null)
            {
                EditorGUI.indentLevel++;
                GUI.contentColor = Color.softRed;
                GUIStyle textStyle = EditorStyles.boldLabel;
                textStyle.clipping = TextClipping.Clip;
                GUILayout.Label("No GsplatRenderer could be found in this object parent.", textStyle);
            }

            if (cutout.m_Target == GsplatCutout.Target.Specific)
            {
                cutout.m_SpecifcRenderer = (GsplatRenderer)EditorGUILayout.ObjectField(
                    cutout.m_SpecifcRenderer, typeof(GsplatRenderer), false);
            }
        }
    }
}
