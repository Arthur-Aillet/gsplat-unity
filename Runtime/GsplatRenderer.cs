// Copyright (c) 2025 Yize Wu
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using UnityEngine;

namespace Gsplat
{
    [ExecuteAlways]
    public class GsplatRenderer : MonoBehaviour, IGsplat
    {
        public enum GsplatSortMode
        {
            Always,
            EachNFrames
        }

        public GsplatAsset GsplatAsset;
        [HideInInspector] public int SHDegree = 3;
        [HideInInspector] public uint RenderOrder = 0;

        [Range(0.0f, 1f)] public float SizeTreshold = 1.0f;
        [Range(2, 75)] public float CullArea = 2.0f;
        [Range(1.0f, 1.2f)]
        public float FrustrumMultiplier = 1.0f;
        [Range(1.0f, 70.0f)] public float AlphaCulling = 1.0f;
        public bool GammaToLinear;
        public bool AsyncUpload;
        public GsplatSortMode SortMode = GsplatSortMode.Always;
        [HideInInspector] public uint SortRefreshRate = 20;
        [Tooltip("Max splat count to be uploaded per frame")]
        public uint UploadBatchSize = 100000;

        public bool RenderBeforeUploadComplete = true;

        GsplatAsset m_prevAsset;
        GsplatRendererImpl m_renderer;

        public bool Valid => RenderBeforeUploadComplete ? SplatCount > 0 : SplatCount == GsplatAsset.SplatCount;
        public uint SplatCount => GsplatAsset ? GsplatAsset.SplatCount - m_pendingSplatCount : 0;

        private uint m_remainingCount = 0;
        public uint RemainingCount { get => m_remainingCount; set => m_remainingCount = value; }
        private Bounds m_bounds;
        public Bounds Bounds { get => m_bounds; set => m_bounds = value; }
        public Bounds AssetBounds { get => GsplatAsset.Bounds; }

        public IComputeManagerResource Resource => m_renderer.Resource;
        public bool ComputeRequired => m_renderer.ComputeRequired;

        public GsplatCutout[] Cutouts
        {
            get
            {
                var cutouts = GsplatCutout.m_RegisteredCutouts
                    .Where(component => component.enabled)
                    .Where(component =>
                        component.m_Target == GsplatCutout.Target.All ||
                        (component.m_Target == GsplatCutout.Target.Parent && component.transform.parent == transform) ||
                        (component.m_Target == GsplatCutout.Target.Specific && component.m_SpecifcRenderer == this)
                    );
                return cutouts.ToArray();
            }
        }
        uint m_pendingSplatCount;

        void SetBufferData()
        {
            m_renderer.PackedSplatsBuffer.SetData(GsplatAsset.PackedSplats);
            if (GsplatAsset.SHBands >= 1)
                m_renderer.PackedSH1Buffer.SetData(GsplatAsset.PackedSH1);
            if (GsplatAsset.SHBands >= 2)
                m_renderer.PackedSH2Buffer.SetData(GsplatAsset.PackedSH2);
            if (GsplatAsset.SHBands == 3)
                m_renderer.PackedSH3Buffer.SetData(GsplatAsset.PackedSH3);
        }

        void SetBufferDataAsync()
        {
            m_pendingSplatCount = GsplatAsset.SplatCount;
        }

        void UploadData()
        {
            var offset = (int)(GsplatAsset.SplatCount - m_pendingSplatCount);
            var count = (int)Math.Min(UploadBatchSize, m_pendingSplatCount);
            m_pendingSplatCount -= (uint)count;
            m_renderer.PackedSplatsBuffer.SetData(GsplatAsset.PackedSplats, offset, offset, count);

            if (GsplatAsset.SHBands >= 1)
                m_renderer.PackedSH1Buffer.SetData(GsplatAsset.PackedSH1, 2 * offset, 2 * offset, 2 * count);
            if (GsplatAsset.SHBands >= 2)
                m_renderer.PackedSH2Buffer.SetData(GsplatAsset.PackedSH2, 4 * offset, 4 * offset, 4 * count);
            if (GsplatAsset.SHBands == 3)
                m_renderer.PackedSH3Buffer.SetData(GsplatAsset.PackedSH3, 4 * offset, 4 * offset, 4 * count);
        }

        void OnEnable()
        {
            GsplatComputeManager.Instance.RegisterGsplat(this);
            if (!GsplatAsset)
                return;
            m_renderer = new GsplatRendererImpl(GsplatAsset.SplatCount, GsplatAsset.SHBands);
#if UNITY_EDITOR
            if (AsyncUpload && Application.isPlaying)
#else
            if (AsyncUpload)
#endif
                SetBufferDataAsync();
            else
                SetBufferData();
        }

        void OnDisable()
        {
            GsplatComputeManager.Instance.UnregisterGsplat(this);
            m_renderer?.Dispose();
            m_renderer = null;
        }

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            if (GsplatSettings.Instance.DisplayGSplatsBoundingBoxes && Valid && isActiveAndEnabled)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(Bounds.center, Bounds.size);
            }
        }
#endif // #if UNITY_EDITOR

        void Update()
        {
            if (m_pendingSplatCount > 0)
                UploadData();

            if (m_prevAsset != GsplatAsset)
            {
                m_prevAsset = GsplatAsset;
                if (GsplatAsset)
                {
                    if (m_renderer == null)
                        m_renderer = new GsplatRendererImpl(GsplatAsset.SplatCount, GsplatAsset.SHBands);
                    else
                        m_renderer.RecreateResources(GsplatAsset.SplatCount, GsplatAsset.SHBands);
#if UNITY_EDITOR
                    if (AsyncUpload && Application.isPlaying)
#else
                    if (AsyncUpload)
#endif
                        SetBufferDataAsync();
                    else
                        SetBufferData();
                }
            }

            if (Valid)
            {
                m_renderer.EvaluateComputeRequired(SortMode, SortRefreshRate);
                GsplatComputeManager.Instance.DispatchPrePass(this);
                m_renderer.Render(m_remainingCount, transform, m_bounds,
                    gameObject.layer, GammaToLinear, SizeTreshold, CullArea, FrustrumMultiplier, AlphaCulling, SHDegree, RenderOrder);
            }
        }
    }
}
