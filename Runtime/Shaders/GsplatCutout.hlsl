// Gaussian Splatting Cutout Specific code
// most of these are from https://https://github.com/aras-p/UnityGaussianSplatting/blob/main/package/Shaders/SplatUtilities.compute
// Copyright (c) 2026 Arthur Aillet
// SPDX-License-Identifier: MIT

#define SPLAT_CUTOUT_TYPE_ELLIPSOID 0
#define SPLAT_CUTOUT_TYPE_BOX 1

bool IsSplatCut(float3 pos)
{
    bool finalCut = false;
    for (uint i = 0; i < _SplatCutoutsCount; ++i)
    {
        GaussianCutoutShaderData cutData = _SplatCutouts[i];
        uint type = cutData.typeAndFlags & 0xFF;
        if (type == 0xFF) // invalid/null cutout, ignore
        continue;

        bool invert = (cutData.typeAndFlags & 0xFF00) != 0;

        float3 cutoutPos = mul(cutData.mat, float4(pos, 1)).xyz;
        if (type == SPLAT_CUTOUT_TYPE_ELLIPSOID)
        {
            invert = (dot(cutoutPos, cutoutPos) <= 1) == invert;
        }
        if (type == SPLAT_CUTOUT_TYPE_BOX)
        {
            invert = (all(abs(cutoutPos) <= 1)) == invert;
        }
        finalCut = finalCut | invert;
    }
    return finalCut;
}