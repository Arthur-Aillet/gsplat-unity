// static const float PI = 3.14159265f;

// // Implementation taken from spark.js
// // Decode a 24‐bit encoded uint into a quaternion (float4) using the folded octahedral inverse.
// float4 DecodeQuatOctXyz88R8(uint encoded) {
//     // Extract the fields.
//     uint quantU = encoded & 0xFFU;              // bits 0–7
//     uint quantV = (encoded >> 8u) & 0xFFU;       // bits 8–15
//     uint angleInt = encoded >> 16u;              // bits 16–23

//     // Recover u and v in [0,1], then map to [-1,1].
//     float u_f = float(quantU) / 255.0;
//     float v_f = float(quantV) / 255.0;
//     float2 f = float2(u_f * 2.0 - 1.0, v_f * 2.0 - 1.0);

//     float3 axis = float3(f.xy, 1.0 - abs(f.x) - abs(f.y));
//     float t = max(-axis.z, 0.0);
//     axis.x += (axis.x >= 0.0) ? -t : t;
//     axis.y += (axis.y >= 0.0) ? -t : t;
//     axis = normalize(axis);

//     // Decode the angle θ ∈ [0,π].
//     float theta = (float(angleInt) / 255.0) * PI;
//     float halfTheta = theta * 0.5;
//     float s = sin(halfTheta);
//     float w = cos(halfTheta);

//     return float4(axis * s, w);
// }

// // Implementation taken from spark.js
// // Decode a 24‐bit encoded uint into a quaternion (float4)
// // float4 decodeQuatXyz888(uint encoded) {
// //     int3 iQuat3 = int3(
// //         int(encoded << 24) >> 24,
// //         int(encoded << 16) >> 24,
// //         int(encoded << 8) >> 24
// //     );
// //     float4 quat = float4(float3(iQuat3) / 127.0, 0.0);
// //     quat.w = sqrt(max(0.0, 1.0 - dot(quat.xyz, quat.xyz)));
// //     return quat;
// // }

// #define LN_SCALE_MIN -12.0
// #define LN_SCALE_MAX 9.0

// struct SplatVertex {
//     float3 modelCenter;
//     float3 scale;
//     float4 color;
//     float4 quat;
// };

// SplatVertex UpackSplat(uint4 packedData) {
//     SplatVertex vertex;
//     uint word0 = packedData.x;
//     uint word1 = packedData.y;
//     uint word2 = packedData.z;
//     uint word3 = packedData.w;

//     uint4 uColor = uint4(word0 & 0xFFU, (word0 >> 8u) & 0xFFU, (word0 >> 16u) & 0xFFU, (word0 >> 24u) & 0xFFU);
//     vertex.color = (float4(uColor) / 255.0);

//     vertex.modelCenter = float3(f16tof32(word1 & 0xFFFFU), f16tof32((word1 >> 16u) & 0xFFFFU), f16tof32(word2 & 0xFFFFU));

//     uint3 uScale = uint3(word3 & 0xFFU, (word3 >> 8u) & 0xFFU, (word3 >> 16u) & 0xFFU);
//     float lnScaleScale = (LN_SCALE_MAX - LN_SCALE_MIN) / 254.0;
//     vertex.scale = float3(
//         (uScale.x == 0u) ? 0.0 : exp(LN_SCALE_MIN + float(uScale.x - 1u) * lnScaleScale),
//         (uScale.y == 0u) ? 0.0 : exp(LN_SCALE_MIN + float(uScale.y - 1u) * lnScaleScale),
//         (uScale.z == 0u) ? 0.0 : exp(LN_SCALE_MIN + float(uScale.z - 1u) * lnScaleScale)
//     );

//     uint uQuat = ((word2 >> 16u) & 0xFFFFU) | ((word3 >> 8u) & 0xFF0000U);
//     vertex.quat = DecodeQuatOctXyz88R8(uQuat);
//     return vertex;
// }
