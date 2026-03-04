// Copyright (c) 2025 Yize Wu
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;

namespace Gsplat
{
    public interface IGsplat
    {
        public Transform transform { get; }
        public GsplatCutout[] cutouts { get; }
        public uint SplatCount { get; }
        public uint RemainingCount { get; set; }
        public IComputeManagerResource Resource { get; }
        public bool isActiveAndEnabled { get; }
        public bool Valid { get; }
    }

    public interface IComputeManagerResource
    {
        public GraphicsBuffer PackedSplatsBuffer { get; }
        public GraphicsBuffer OrderBuffer { get; }
        public void Dispose();
    }

    // some codes of this class originated from the GaussianSplatRenderSystem in aras-p/UnityGaussianSplatting by Aras Pranckevičius
    // https://github.com/aras-p/UnityGaussianSplatting/blob/main/package/Runtime/GaussianSplatRenderer.cs
    public class GsplatComputeManager
    {
        class Resource : IComputeManagerResource
        {
            public GraphicsBuffer PackedSplatsBuffer { get; }
            public GraphicsBuffer OrderBuffer { get; set; }
            public GraphicsBuffer InputKeys { get; private set; }
            public GsplatSortPass.SupportResources SortResources { get; }
            public GsplatPrePass.SupportResources PrePassResources;
            public bool WaitForInit;
            public bool HandlingCutouts;
            public GsplatCutout.ShaderData[] CutoutsData;

            public Resource(uint count, GraphicsBuffer packedSplatsBuffer, GraphicsBuffer orderBuffer)
            {
                PackedSplatsBuffer = packedSplatsBuffer;
                OrderBuffer = orderBuffer;
                WaitForInit = false;
                HandlingCutouts = true;
                CutoutsData = new GsplatCutout.ShaderData[0];

                InputKeys = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)count, sizeof(uint));
                SortResources = GsplatSortPass.SupportResources.Load(count);
                PrePassResources = GsplatPrePass.SupportResources.Create();
            }

            public void Dispose()
            {
                InputKeys?.Dispose();
                SortResources.Dispose();
                PrePassResources.Dispose();

                InputKeys = null;
            }
        }

        public static GsplatComputeManager Instance => s_instance ??= new GsplatComputeManager();
        static GsplatComputeManager s_instance;

        CommandBuffer m_commandBuffer;
        readonly HashSet<IGsplat> m_gsplats = new();
        readonly HashSet<Camera> m_camerasInjected = new();
        readonly List<IGsplat> m_activeGsplats = new();
        GsplatSortPass m_sortPass;
        GsplatPrePass m_prePass;
        public const string k_PassName = "SortGsplats";

        public bool Valid => m_sortPass is { Valid: true } && m_prePass is { Valid: true };

        public void InitSorter(ComputeShader sortComputeShader)
        {
            m_sortPass = sortComputeShader ? new GsplatSortPass(sortComputeShader) : null;
        }

        public void InitPrePass(ComputeShader prePassShader)
        {
            m_prePass = prePassShader ? new GsplatPrePass(prePassShader) : null;
        }

        public void RegisterGsplat(IGsplat gsplat)
        {
            if (m_gsplats.Count == 0)
            {
                if (!GraphicsSettings.currentRenderPipeline)
                    Camera.onPreCull += OnPreCullCamera;
            }

            m_gsplats.Add(gsplat);
        }

        public void UnregisterGsplat(IGsplat gsplat)
        {
            if (!m_gsplats.Remove(gsplat))
                return;
            if (m_gsplats.Count != 0) return;

            if (m_camerasInjected != null)
            {
                if (m_commandBuffer != null)
                    foreach (var cam in m_camerasInjected.Where(cam => cam))
                        cam.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, m_commandBuffer);
                m_camerasInjected.Clear();
            }

            m_activeGsplats.Clear();
            m_commandBuffer?.Dispose();
            m_commandBuffer = null;
            Camera.onPreCull -= OnPreCullCamera;
        }

        public bool GatherGsplatsForCamera(Camera cam)
        {
            if (cam.cameraType == CameraType.Preview)
                return false;

            m_activeGsplats.Clear();
            foreach (var gs in m_gsplats.Where(gs => gs is { isActiveAndEnabled: true, Valid: true }))
                m_activeGsplats.Add(gs);
            return m_activeGsplats.Count != 0;
        }

        void InitialClearCmdBuffer(Camera cam)
        {
            m_commandBuffer ??= new CommandBuffer { name = k_PassName };
            if (!GraphicsSettings.currentRenderPipeline && cam &&
                !m_camerasInjected.Contains(cam))
            {
                cam.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, m_commandBuffer);
                m_camerasInjected.Add(cam);
            }

            m_commandBuffer.Clear();
        }

        void OnPreCullCamera(Camera camera)
        {
            if (!Valid || !GsplatSettings.Instance.Valid || !GatherGsplatsForCamera(camera))
                return;

            InitialClearCmdBuffer(camera);
            DispatchSort(m_commandBuffer, camera);
        }

        public void DispatchSort(CommandBuffer cmd, Camera camera)
        {
            foreach (var gs in m_activeGsplats)
            {
                var res = (Resource)gs.Resource;

                if (gs.RemainingCount <= 0)
                    continue;

                if (res.WaitForInit)
                {
                    m_sortPass.InitPayload(cmd, res.OrderBuffer, (uint)res.OrderBuffer.count);
                    res.WaitForInit = false;
                }

                var sorterArgs = new GsplatSortPass.Args
                {
                    Count = gs.RemainingCount,
                    MatrixMv = camera.worldToCameraMatrix * gs.transform.localToWorldMatrix,
                    PackedSplatsBuffer = res.PackedSplatsBuffer,
                    InputKeys = res.InputKeys,
                    InputValues = res.OrderBuffer,
                    Resources = res.SortResources
                };
                m_sortPass.Dispatch(cmd, sorterArgs);
            }
        }

        public void DispatchPrePass(IGsplat gs)
        {
            if (m_prePass == null || !m_prePass.Valid)
                return;

            var res = (Resource)gs.Resource;

            if (gs.cutouts.Length == 0)
            {
                if (res.HandlingCutouts == true)
                {
                    res.HandlingCutouts = false;
                    res.WaitForInit = true;
                    res.CutoutsData = new GsplatCutout.ShaderData[0];
                    gs.RemainingCount = gs.SplatCount;
                }
                return;
            }

            if (res.HandlingCutouts == false)
                res.HandlingCutouts = true;

            GsplatCutout.ShaderData[] updatedCutoutsData = new GsplatCutout.ShaderData[gs.cutouts.Length];
            bool cutoutsUnchanged = res.CutoutsData.Length == updatedCutoutsData.Length;
            for (int i = 0; i != gs.cutouts.Length; i++)
            {
                updatedCutoutsData[i] = gs.cutouts[i].GetShaderData(gs.transform.localToWorldMatrix);
                if (cutoutsUnchanged)
                    if (updatedCutoutsData[i].matrix != res.CutoutsData[i].matrix || updatedCutoutsData[i].typeAndFlags != res.CutoutsData[i].typeAndFlags)
                        cutoutsUnchanged = false;
            }

            if (cutoutsUnchanged)
                return;

            res.CutoutsData = updatedCutoutsData;

            m_prePass.Dispatch(res.OrderBuffer, res.PackedSplatsBuffer, ref res.PrePassResources.CutoutsBuffer, res.CutoutsData, (int)gs.SplatCount);
            gs.RemainingCount = m_prePass.ExtractOrderSize(res.OrderBuffer, res.PrePassResources);
        }

        public IComputeManagerResource CreateComputeResource(uint count, GraphicsBuffer packedSplatsBuffer,
            GraphicsBuffer orderBuffer)
        {
            return new Resource(count, packedSplatsBuffer, orderBuffer);
        }
    }
}
