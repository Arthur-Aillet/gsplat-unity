// Copyright (c) 2025 Yize Wu
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gsplat
{
    public interface IGsplat
    {
        public Transform transform { get; }
        public GsplatCutout[] cutouts { get; }
        public uint SplatCount { get; }
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
            public GraphicsBuffer OrderBuffer { get; }
            public GraphicsBuffer InputKeys { get; private set; }
            public GraphicsBuffer CutoutsBuffer { get; set; }
            public GsplatSortPass.SupportResources Resources { get; }
            public bool Initialized;

            public Resource(uint count, GraphicsBuffer packedSplatsBuffer, GraphicsBuffer orderBuffer)
            {
                PackedSplatsBuffer = packedSplatsBuffer;
                OrderBuffer = orderBuffer;

                CutoutsBuffer = null;

                InputKeys = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)count, sizeof(uint));
                Resources = GsplatSortPass.SupportResources.Load(count);
            }

            public void Dispose()
            {
                InputKeys?.Dispose();
                CutoutsBuffer?.Dispose();
                Resources.Dispose();

                InputKeys = null;
                CutoutsBuffer = null;
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
            DispatchPrePass(m_commandBuffer, camera);
        }

        public Vector3? camPos = null;
        public Quaternion? camRot = null;

        private void Sort(CommandBuffer cmd, Camera camera)
        {
            foreach (var gs in m_activeGsplats)
            {
                var res = (Resource)gs.Resource;
                //if (!res.Initialized)
                //{
                m_sortPass.InitPayload(cmd, res.OrderBuffer, (uint)res.OrderBuffer.count);
                //    res.Initialized = true;
                //}

                var sorterArgs = new GsplatSortPass.Args
                {
                    Count = gs.SplatCount,
                    MatrixMv = camera.worldToCameraMatrix * gs.transform.localToWorldMatrix,
                    PackedSplatsBuffer = res.PackedSplatsBuffer,
                    InputKeys = res.InputKeys,
                    InputValues = res.OrderBuffer,
                    Resources = res.Resources
                };
                m_sortPass.Dispatch(cmd, sorterArgs);
            }
        }

        public void DispatchPrePass(CommandBuffer cmd, Camera camera)
        {
            foreach (var gs in m_activeGsplats)
            {
                var res = (Resource)gs.Resource;

                m_prePass.Dispatch(cmd, res.OrderBuffer, res.CutoutsBuffer, res.PackedSplatsBuffer, gs);
            }
        }

        public void DispatchSort(CommandBuffer cmd, Camera camera)
        {
            if (GsplatSettings.Instance.SortPass == 0)
            {
                Sort(cmd, camera);
            } else if (GsplatSettings.Instance.SortPass == 1)
            {
                if (camPos == null)
                {
                    camPos = camera.transform.position;
                    camRot = camera.transform.rotation;
                    Sort(cmd, camera);
                } else
                {
                    if ((camPos.Value - camera.transform.position).magnitude > .3f || Quaternion.Angle(camRot.Value, camera.transform.rotation) > 15.0f)
                    {
                        camPos = camera.transform.position;
                        camRot = camera.transform.rotation;
                        Sort(cmd, camera);
                    }
                }
            }
        }

        public IComputeManagerResource CreateSorterResource(uint count, GraphicsBuffer packedSplatsBuffer, GraphicsBuffer orderBuffer)
        {
            return new Resource(count, packedSplatsBuffer, orderBuffer);
        }
    }
}
