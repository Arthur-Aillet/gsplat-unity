// Copyright (c) 2025 Arthur
// SPDX-License-Identifier: MIT

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
        static readonly int k_orderBuffer = Shader.PropertyToID("_OrderBuffer");

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

        public void Dispatch(CommandBuffer cmd, GraphicsBuffer orderBuffer, uint count)
        {
            Assert.IsTrue(Valid);

            int threadBlocks = GsplatUtils.DivRoundUp((int)count, 1024);

            cmd.SetComputeIntParam(m_CS, k_count, (int)count);
            cmd.SetComputeBufferParam(m_CS, m_kernelPreCompute, k_orderBuffer, orderBuffer);
            cmd.DispatchCompute(m_CS, m_kernelPreCompute, threadBlocks, 1, 1);
        }
    }
}
