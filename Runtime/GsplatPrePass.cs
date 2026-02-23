// Copyright (c) 2025 Arthur
// SPDX-License-Identifier: MIT

using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;

namespace Gsplat
{
    public class GsplatPrePass
    {

        private ComputeShader m_CS;
        readonly int m_kernelPreCompute = -1;

        static readonly int k_count = Shader.PropertyToID("_Count");
        static readonly int k_splatCutoutsCount = Shader.PropertyToID("_SplatCutoutsCount");
        static readonly int k_cutoutsBuffer = Shader.PropertyToID("_SplatCutouts");
        static readonly int k_orderBuffer = Shader.PropertyToID("_OrderBuffer");
        static readonly int k_packedSplatsBuffer = Shader.PropertyToID("_PackedSplatsBuffer");

        readonly bool m_Valid;
        public bool Valid => m_Valid;

        public struct SupportResources
        {
            public GraphicsBuffer CutoutsBuffer;

            public static SupportResources Create()
            {
                var resources = new SupportResources
                {
                    CutoutsBuffer = null,
                };
                return resources;
            }

            public void Dispose()
            {
                CutoutsBuffer?.Dispose();
                CutoutsBuffer = null;
            }
        }

        public GsplatPrePass(ComputeShader cs)
        {
            m_CS = cs;
            m_Valid = false;

            if (cs)
            {
                m_kernelPreCompute = cs.FindKernel("PreCompute");
                if (m_kernelPreCompute >= 0 && cs.IsSupported(m_kernelPreCompute))
                {
                    m_Valid = true;
                }
            }
        }

        void UpdateCutoutsBuffer(ref SupportResources res, GsplatCutout[] cutouts, Transform transform)
        {
            int numberOfCutouts = cutouts?.Length ?? 0;
            int bufferSize = Math.Max(numberOfCutouts, 1);

            if (res.CutoutsBuffer == null || res.CutoutsBuffer.count != bufferSize)
            {
                res.CutoutsBuffer?.Dispose();
                res.CutoutsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, GsplatCutout.ShaderDataSize);
            }

            NativeArray<GsplatCutout.ShaderData> data = new(bufferSize, Allocator.Temp);
            if (cutouts != null)
            {
                var matrix = transform.localToWorldMatrix;
                for (var i = 0; i < cutouts.Length; ++i)
                {
                    data[i] = cutouts[i].GetShaderData(matrix);
                }
            }
            res.CutoutsBuffer.SetData(data);
            data.Dispose();
            m_CS.SetBuffer(m_kernelPreCompute, k_cutoutsBuffer, res.CutoutsBuffer);
            m_CS.SetInt(k_splatCutoutsCount, numberOfCutouts);
        }

        public void Dispatch(GraphicsBuffer orderBuffer, GraphicsBuffer packedSplats, ref SupportResources res, IGsplat gs)
        {
            Assert.IsTrue(Valid);

            int threadBlocks = GsplatUtils.DivRoundUp((int)gs.SplatCount, 1024);

            UpdateCutoutsBuffer(ref res, gs.cutouts, gs.transform);
            m_CS.SetInt(k_count, (int)gs.SplatCount);
            m_CS.SetBuffer(m_kernelPreCompute, k_orderBuffer, orderBuffer);
            m_CS.SetBuffer(m_kernelPreCompute, k_packedSplatsBuffer, packedSplats);
            m_CS.Dispatch(m_kernelPreCompute, threadBlocks, 1, 1);
        }
    }
}
