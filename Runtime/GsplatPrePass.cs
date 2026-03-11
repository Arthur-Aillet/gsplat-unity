// Copyright (c) 2025 Arthur
// SPDX-License-Identifier: MIT

using System;
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
        static readonly int k_boundsBuffer = Shader.PropertyToID("_BoundsBuffer");

        readonly bool m_Valid;
        public bool Valid => m_Valid;

        public struct SupportResources
        {
            public GraphicsBuffer CutoutsBuffer;
            public GraphicsBuffer OrderSizeBuffer;
            public GraphicsBuffer BoundsBuffer;

            public static SupportResources Create()
            {
                var resources = new SupportResources
                {
                    CutoutsBuffer = null,
                    OrderSizeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint)),
                    BoundsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 6, sizeof(uint)),
                };
                return resources;
            }

            public void Dispose()
            {
                CutoutsBuffer?.Dispose();
                OrderSizeBuffer?.Dispose();
                BoundsBuffer?.Dispose();

                CutoutsBuffer = null;
                OrderSizeBuffer = null;
                BoundsBuffer = null;
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

        void UpdateCutoutsBuffer(ref SupportResources res, GsplatCutout.ShaderData[] cutouts)
        {
            int numberOfCutouts = cutouts.Length;
            int bufferSize = Math.Max(numberOfCutouts, 1);

            if (res.CutoutsBuffer == null || res.CutoutsBuffer.count != bufferSize)
            {
                res.CutoutsBuffer?.Dispose();
                res.CutoutsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, GsplatCutout.ShaderDataSize);
            }

            res.CutoutsBuffer.SetData(cutouts);
            m_CS.SetBuffer(m_kernelPreCompute, k_cutoutsBuffer, res.CutoutsBuffer);
            m_CS.SetInt(k_splatCutoutsCount, numberOfCutouts);
        }

        public void Dispatch(GraphicsBuffer orderBuffer, GraphicsBuffer packedSplats, ref SupportResources res, GsplatCutout.ShaderData[] cutouts, int splatCount)
        {
            Assert.IsTrue(Valid);
            orderBuffer.SetCounterValue(0);

            uint max = GsplatUtils.FloatToSortableUint(short.MaxValue);
            uint min = GsplatUtils.FloatToSortableUint(short.MinValue);
            uint[] array = {max, max, max, min, min, min};
            res.BoundsBuffer.SetData(array);

            int threadBlocks = GsplatUtils.DivRoundUp(splatCount, 1024);

            UpdateCutoutsBuffer(ref res, cutouts);
            m_CS.SetInt(k_count, splatCount);
            m_CS.SetBuffer(m_kernelPreCompute, k_orderBuffer, orderBuffer);
            m_CS.SetBuffer(m_kernelPreCompute, k_packedSplatsBuffer, packedSplats);
            m_CS.SetBuffer(m_kernelPreCompute, k_boundsBuffer, res.BoundsBuffer);
            m_CS.Dispatch(m_kernelPreCompute, threadBlocks, 1, 1);
        }

        public Bounds ExtractBounds(SupportResources res)
        {
            uint[] boundsData = new uint[6];
            res.BoundsBuffer.GetData(boundsData);

            Bounds bounds = default;
            Vector3 bmin = new(GsplatUtils.SortableUintToFloat(boundsData[0]), GsplatUtils.SortableUintToFloat(boundsData[1]), GsplatUtils.SortableUintToFloat(boundsData[2]));
            Vector3 bmax = new(GsplatUtils.SortableUintToFloat(boundsData[3]), GsplatUtils.SortableUintToFloat(boundsData[4]), GsplatUtils.SortableUintToFloat(boundsData[5]));
            bounds.SetMinMax(bmin, bmax);

            if (bounds.extents.sqrMagnitude < 0.01)
                bounds.extents = new Vector3(0.1f,0.1f,0.1f);
            return bounds;
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
