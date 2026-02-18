// Copyright (c) 2025 Yize Wu
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gsplat
{
    public class GsplatSettings : ScriptableObject
    {
        const string k_gsplatSettingsResourcesPath = "GsplatSettings";

        const string k_gsplatSettingsPath =
            "Assets/Gsplat/Settings/Resources/" + k_gsplatSettingsResourcesPath + ".asset";

        static GsplatSettings s_instance;

        public static GsplatSettings Instance
        {
            get
            {
                if (s_instance)
                    return s_instance;

                var settings = Resources.Load<GsplatSettings>(k_gsplatSettingsResourcesPath);
#if UNITY_EDITOR
                if (!settings)
                {
                    var assetPath = Path.GetDirectoryName(k_gsplatSettingsPath);
                    if (!Directory.Exists(assetPath))
                        Directory.CreateDirectory(assetPath);

                    settings = CreateInstance<GsplatSettings>();
                    settings.Shader =
                        AssetDatabase.LoadAssetAtPath<Shader>(GsplatUtils.k_PackagePath +
                                                              "Runtime/Shaders/Gsplat.shader");
                    settings.ComputeShader =
                        AssetDatabase.LoadAssetAtPath<ComputeShader>(GsplatUtils.k_PackagePath +
                                                                     "Runtime/Shaders/Gsplat.compute");
                    settings.OnValidate();
                    AssetDatabase.CreateAsset(settings, k_gsplatSettingsPath);
                    AssetDatabase.SaveAssets();
                }
#endif

                s_instance = settings;
                return s_instance;
            }
        }

        public Shader Shader;
        public ComputeShader ComputeShader;
        public uint SplatInstanceSize = 128;
        public bool ShowImportErrors = true;
        public int SortPass = 0;
        public Material[] Materials { get; private set; }
        public Mesh Mesh { get; private set; }

        public bool Valid => Materials?.Length != 0 && Mesh && SplatInstanceSize > 0;

        Shader m_prevShader;
        ComputeShader m_prevComputeShader;
        uint m_prevSplatInstanceSize;

        void CreateMeshInstance()
        {
            var meshPositions = new Vector3[3 * SplatInstanceSize];
            for (uint i = 0; i < SplatInstanceSize; ++i)
            {
                unsafe
                {
                    meshPositions[i * 3] = new Vector3(1.73f, -1, *(float*)&i);
                    meshPositions[i * 3 + 1] = new Vector3(-1.73f, -1, *(float*)&i);
                    meshPositions[i * 3 + 2] = new Vector3(0, 2, *(float*)&i);
                }

            }

            Mesh = new Mesh
            {
                name = "GsplatMeshInstance",
                vertices = meshPositions,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        void CreateMaterials()
        {
            if (Materials != null)
                foreach (var mat in Materials)
                    DestroyImmediate(mat);

            if (!Shader)
            {
                Materials = null;
                return;
            }

            Materials = new Material[4];
            for (var i = 0; i < 4; ++i)
            {
                Materials[i] = new Material(Shader) { hideFlags = HideFlags.HideAndDontSave };
                Materials[i].EnableKeyword($"SH_BANDS_{i}");
            }
        }

        void OnValidate()
        {
            if (Shader != m_prevShader)
            {
                CreateMaterials();
                m_prevShader = Shader;
            }

            if (ComputeShader != m_prevComputeShader)
            {
                GsplatSorter.Instance.InitSorter(ComputeShader);
                m_prevComputeShader = ComputeShader;
            }

            if (SplatInstanceSize != m_prevSplatInstanceSize)
            {
                DestroyImmediate(Mesh);
                CreateMeshInstance();
                m_prevSplatInstanceSize = SplatInstanceSize;
            }
        }

        void OnEnable()
        {
            CreateMaterials();
            m_prevShader = Shader;
            GsplatSorter.Instance.InitSorter(ComputeShader);
            m_prevComputeShader = ComputeShader;

            CreateMeshInstance();
            m_prevSplatInstanceSize = SplatInstanceSize;
        }
    }
}
