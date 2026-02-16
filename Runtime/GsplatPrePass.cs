// Copyright (c) 2025 Arthur
// SPDX-License-Identifier: MIT

using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Gsplat
{
    public class GsplatPrePass
    {

        private ComputeShader m_CS;
        readonly int m_kernelPreCompute = -1;
        readonly int m_kernelCSCalcViewData = -1;

        static readonly int k_splatCount = Shader.PropertyToID("_SplatCount");
        static readonly int k_splatCutoutsCount = Shader.PropertyToID("_SplatCutoutsCount");
        static readonly int k_cutoutsBuffer = Shader.PropertyToID("_SplatCutouts");
        public static readonly int k_matrixMV = Shader.PropertyToID("_MatrixMV");
        public static readonly int k_matrixObjectToWorld = Shader.PropertyToID("_MatrixObjectToWorld");
        public static readonly int k_matrixWorldToObject = Shader.PropertyToID("_MatrixWorldToObject");
        public static readonly int k_vecScreenParams = Shader.PropertyToID("_VecScreenParams");
        public static readonly int k_vecWorldSpaceCameraPos = Shader.PropertyToID("_VecWorldSpaceCameraPos");
        static readonly int k_projectionMatrix = Shader.PropertyToID("_ProjectionMatrix");

        static readonly int k_orderBuffer = Shader.PropertyToID("_OrderBuffer");
        static readonly int k_packedSplatsBuffer = Shader.PropertyToID("_PackedSplatsBuffer");
        static readonly int k_splatViewData = Shader.PropertyToID("_SplatViewData");

        public GraphicsBuffer CutoutsBuffer = null;

        readonly bool m_Valid;
        public bool Valid => m_Valid;

        public GsplatPrePass(ComputeShader cs)
        {
            m_CS = cs;
            m_Valid = false;

            if (cs)
            {
                m_kernelPreCompute = cs.FindKernel("PreCompute");
                m_kernelCSCalcViewData = cs.FindKernel("CSCalcViewData");

                if (m_kernelPreCompute >= 0 && m_kernelCSCalcViewData >= 0 && cs.IsSupported(m_kernelPreCompute) && cs.IsSupported(m_kernelCSCalcViewData))
                {
                    m_Valid = true;
                }
            }
        }

        void UpdateCutoutsBuffer(CommandBuffer cmd, GsplatCutout[] cutouts, Transform transform)
        {
            int numberOfCutouts = cutouts?.Length ?? 0;
            int bufferSize = Math.Max(numberOfCutouts, 1);

            if (CutoutsBuffer == null || CutoutsBuffer.count != bufferSize)
            {
                CutoutsBuffer?.Dispose();
                CutoutsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, GsplatCutout.ShaderDataSize);
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
            CutoutsBuffer.SetData(data);
            data.Dispose();

            cmd.SetComputeBufferParam(m_CS, m_kernelCSCalcViewData, k_cutoutsBuffer, CutoutsBuffer);
            cmd.SetComputeIntParam(m_CS, k_splatCutoutsCount, numberOfCutouts);
        }

        public void Dispatch(CommandBuffer cmd, GraphicsBuffer orderBuffer, GraphicsBuffer packedSplatsBuffer, GraphicsBuffer splatViewDataBuffer, Camera cam, IGsplat gs)
        {
            Assert.IsTrue(Valid);

            // UpdateCutoutsBuffer(cmd, cutoutBuffer, gs.cutouts, gs.transform);
            cmd.SetComputeIntParam(m_CS, k_splatCount, (int)gs.SplatCount);
            // cmd.SetComputeBufferParam(m_CS, m_kernelPreCompute, k_orderBuffer, orderBuffer);
            // cmd.SetComputeBufferParam(m_CS, m_kernelPreCompute, k_packedSplatsBuffer, packedSplatsBuffer);
            // cmd.DispatchCompute(m_CS, m_kernelPreCompute, threadBlocks, 1, 1);

            var tr = gs.transform;

            Matrix4x4 matView = cam.worldToCameraMatrix;
            Matrix4x4 matO2W = tr.localToWorldMatrix;
            Matrix4x4 matW2O = tr.worldToLocalMatrix;
            int screenW = cam.pixelWidth, screenH = cam.pixelHeight;
            int eyeW = XRSettings.eyeTextureWidth, eyeH = XRSettings.eyeTextureHeight;
            Vector4 screenPar = new Vector4(eyeW != 0 ? eyeW : screenW, eyeH != 0 ? eyeH : screenH, 0, 0);
            Vector4 camPos = cam.transform.position;

            cmd.SetComputeMatrixParam(m_CS, k_matrixMV, matView * matO2W);
            cmd.SetComputeMatrixParam(m_CS, k_matrixObjectToWorld, matO2W);
            cmd.SetComputeMatrixParam(m_CS, k_matrixWorldToObject, matW2O);
            cmd.SetComputeVectorParam(m_CS, k_vecScreenParams, screenPar);
            cmd.SetComputeVectorParam(m_CS, k_vecWorldSpaceCameraPos, camPos);
            cmd.SetComputeMatrixParam(m_CS, k_projectionMatrix, cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left));

            UpdateCutoutsBuffer(cmd, gs.cutouts, tr);

            cmd.SetComputeBufferParam(m_CS, m_kernelCSCalcViewData, k_orderBuffer, orderBuffer);
            cmd.SetComputeBufferParam(m_CS, m_kernelCSCalcViewData, k_packedSplatsBuffer, packedSplatsBuffer);
            cmd.SetComputeBufferParam(m_CS, m_kernelCSCalcViewData, k_splatViewData, splatViewDataBuffer);

            int threadBlocks = GsplatUtils.DivRoundUp((int)gs.SplatCount, 1024);

            cmd.DispatchCompute(m_CS, m_kernelCSCalcViewData, threadBlocks, 1, 1);
        }
    }
}
