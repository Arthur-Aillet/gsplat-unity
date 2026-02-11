// Originated from aras-p/UnityGaussianSplatting by Aras Pranckevičius
// https://github.com/aras-p/UnityGaussianSplatting/blob/main/package/Runtime/GaussianCutout.cs
// Copyright (c) 2023 Aras Pranckevičius
// Modified by Arthur Aillet
// Copyright (c) 2025 Arthur Aillet
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gsplat
{
    public class GsplatCutout : MonoBehaviour
    {
        public enum Type
        {
            Ellipsoid,
            Box
        }

        public Type m_Type = Type.Ellipsoid;
        public bool m_Invert = false;
        private bool m_disconnected;

        public struct ShaderData // match GaussianCutoutShaderData in CS
        {
            public Matrix4x4 matrix;
            public uint typeAndFlags;
        }

        void OnValidate()
        {
            Transform currentTransform = transform;

            while (currentTransform.parent != null)
            {
                if (currentTransform.parent.TryGetComponent<GsplatRenderer>(out var renderer))
                {
                    renderer.m_Cutouts ??= Array.Empty<GsplatCutout>();
                    ArrayUtility.Add(ref renderer.m_Cutouts, this);
                    m_disconnected = false;
                    return;
                }
                currentTransform = currentTransform.parent.transform;
            }
            m_disconnected = true;
        }

        public static ShaderData GetShaderData(GsplatCutout self, Matrix4x4 rendererMatrix)
        {
            ShaderData sd = default;
            if (self && self.isActiveAndEnabled)
            {
                var tr = self.transform;
                sd.matrix = tr.worldToLocalMatrix * rendererMatrix;
                sd.typeAndFlags = ((uint)self.m_Type) | (self.m_Invert ? 0x100u : 0u);
            } else
            {
                sd.typeAndFlags = ~0u;
            }
            return sd;
        }

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Color color;
            if (m_disconnected)
                color = Color.red;
            else
                color = Color.magenta;

            color.a = 0.2f;
            if (Selection.Contains(gameObject))
                color.a = 0.9f;
            else
            {
                // mid amount of alpha if a GS object that contains us as a cutout is selected
                var activeGo = Selection.activeGameObject;
                if (activeGo != null)
                {
                    var activeSplat = activeGo.GetComponent<GsplatRenderer>();
                    if (activeSplat != null)
                    {
                        if (activeSplat.m_Cutouts != null && activeSplat.m_Cutouts.Contains(this))
                            color.a = 0.5f;
                    }
                }
            }

            Gizmos.color = color;
            if (m_Type == Type.Ellipsoid)
            {
                Gizmos.DrawWireSphere(Vector3.zero, 1.0f);
            }
            if (m_Type == Type.Box)
            {
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 2);
            }
        }
#endif // #if UNITY_EDITOR
    }
}
