// Copyright (c) 2025 Arthur
// SPDX-License-Identifier: MIT

using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

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

        void UpdateCutoutsBuffer(CommandBuffer cmd, GraphicsBuffer cutoutsBuffer, GsplatCutout[] cutouts, Transform transform)
        {
            int numberOfCutouts = cutouts?.Length ?? 0;
            int bufferSize = Math.Max(numberOfCutouts, 1);

            if (cutoutsBuffer == null || cutoutsBuffer.count != bufferSize)
            {
                cutoutsBuffer?.Dispose();
                cutoutsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, GsplatCutout.ShaderDataSize);
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
            cutoutsBuffer.SetData(data);
            data.Dispose();

            cmd.SetComputeBufferParam(m_CS, m_kernelPreCompute, k_cutoutsBuffer, cutoutsBuffer);
            cmd.SetComputeIntParam(m_CS, k_splatCutoutsCount, numberOfCutouts);
        }

        public void Dispatch(CommandBuffer cmd, GraphicsBuffer orderBuffer, GraphicsBuffer cutoutBuffer, GraphicsBuffer packedSplats, IGsplat gs)
        {
            Assert.IsTrue(Valid);

            int threadBlocks = GsplatUtils.DivRoundUp((int)gs.SplatCount, 1024);

            UpdateCutoutsBuffer(cmd, cutoutBuffer, gs.cutouts, gs.transform);
            cmd.SetComputeIntParam(m_CS, k_count, (int)gs.SplatCount);
            cmd.SetComputeBufferParam(m_CS, m_kernelPreCompute, k_orderBuffer, orderBuffer);
            cmd.SetComputeBufferParam(m_CS, m_kernelPreCompute, k_packedSplatsBuffer, packedSplats);
            cmd.DispatchCompute(m_CS, m_kernelPreCompute, threadBlocks, 1, 1);
        }
    }
}
