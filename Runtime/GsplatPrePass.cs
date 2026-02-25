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
            public GraphicsBuffer OrderSizeBuffer;

            public static SupportResources Create()
            {
                var resources = new SupportResources
                {
                    CutoutsBuffer = null,
                    OrderSizeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint)),

                };
                return resources;
            }

            public void Dispose()
            {
                CutoutsBuffer?.Dispose();
                OrderSizeBuffer?.Dispose();

                CutoutsBuffer = null;
                OrderSizeBuffer = null;
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

        void UpdateCutoutsBuffer(ref GraphicsBuffer cutoutsBuffer, GsplatCutout.ShaderData[] cutouts)
        {
            int numberOfCutouts = cutouts.Length;
            int bufferSize = Math.Max(numberOfCutouts, 1);

            if (cutoutsBuffer == null || cutoutsBuffer.count != bufferSize)
            {
                cutoutsBuffer?.Dispose();
                cutoutsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, GsplatCutout.ShaderDataSize);
            }

            cutoutsBuffer.SetData(cutouts);
            m_CS.SetBuffer(m_kernelPreCompute, k_cutoutsBuffer, cutoutsBuffer);
            m_CS.SetInt(k_splatCutoutsCount, numberOfCutouts);
        }

        public void Dispatch(GraphicsBuffer orderBuffer, GraphicsBuffer packedSplats, ref GraphicsBuffer cutoutsBuffer, GsplatCutout.ShaderData[] cutouts, int splatCount)
        {
            Assert.IsTrue(Valid);
            orderBuffer.SetCounterValue(0);

            int threadBlocks = GsplatUtils.DivRoundUp(splatCount, 1024);

            UpdateCutoutsBuffer(ref cutoutsBuffer, cutouts);
            m_CS.SetInt(k_count, splatCount);
            m_CS.SetBuffer(m_kernelPreCompute, k_orderBuffer, orderBuffer);
            m_CS.SetBuffer(m_kernelPreCompute, k_packedSplatsBuffer, packedSplats);
            m_CS.Dispatch(m_kernelPreCompute, threadBlocks, 1, 1);
        }

        public uint ExtractOrderSize(GraphicsBuffer orderBuffer, SupportResources res)
        {
            GraphicsBuffer.CopyCount(orderBuffer, res.OrderSizeBuffer, 0);
            uint[] count = new uint[1];
            res.OrderSizeBuffer.GetData(count);
            return count[0];
        }
    }
}
