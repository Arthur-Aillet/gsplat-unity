// Copyright (c) 2025 Yize Wu
// SPDX-License-Identifier: MIT

Shader "Gsplat/Standard"
{
    Properties {}
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            ZWrite Off
            Blend One OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require compute
            #pragma multi_compile SH_BANDS_0 SH_BANDS_1 SH_BANDS_2 SH_BANDS_3

            #include "UnityCG.cginc"
            #include "Gsplat.hlsl"
            bool _GammaToLinear;
            float4x4 _MATRIX_M;
            #ifndef SH_BANDS_0
            StructuredBuffer<float3> _SHBuffer;
            #endif

            struct SplatViewData {
                float4 pos;
                float2 axis1, axis2;
                half4 color; // 4xFP16
            };

            int _SplatCount;
            int _SplatInstanceSize;
            StructuredBuffer<uint> _OrderBuffer;
            StructuredBuffer<SplatViewData> _VertexBuffer;

            // struct appdata
            // {
            //     float4 vertex : POSITION;
            //     #if !defined(UNITY_INSTANCING_ENABLED) && !defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && !defined(UNITY_STEREO_INSTANCING_ENABLED)
            //     uint instanceID : SV_InstanceID;
            //     #endif
            //     UNITY_VERTEX_INPUT_INSTANCE_ID
            // };

            // bool InitSource(appdata v, out SplatSource source)
            // {
            //     #if !defined(UNITY_INSTANCING_ENABLED) && !defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && !defined(UNITY_STEREO_INSTANCING_ENABLED)
            //     source.id = v.instanceID * _SplatInstanceSize + asuint(v.vertex.z);
            //     #else
            //     source.id = unity_InstanceID * _SplatInstanceSize + asuint(v.vertex.z);
            //     #endif

            //     if (source.id >= _SplatCount)
            //         return false;

            //     source.cornerUV = float2(v.vertex.x, v.vertex.y);
            //     return true;
            // }

            bool InitCenter(float3 modelCenter, out SplatCenter center)
            {
                float4x4 modelView = mul(UNITY_MATRIX_V, _MATRIX_M);
                float4 centerView = mul(modelView, float4(modelCenter, 1.0));
                if (centerView.z > 0.0)
                {
                    return false;
                }
                float4 centerProj = mul(UNITY_MATRIX_P, centerView);
                centerProj.z = clamp(centerProj.z, -abs(centerProj.w), abs(centerProj.w));
                center.view = centerView.xyz / centerView.w;
                center.proj = centerProj;
                center.projMat00 = UNITY_MATRIX_P[0][0];
                center.modelView = modelView;
                return true;
            }

            struct v2f
            {
                half4 color: COLOR0;
                float2 pos : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(uint vtxID : SV_VertexID, uint instID : SV_InstanceID)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                instID = _OrderBuffer[instID];
                SplatViewData view = _VertexBuffer[instID];

                //float4 centerClipPos = view.pos;

                float4 centerClipPos = mul(UNITY_MATRIX_VP, view.pos);

                bool behindCam = centerClipPos.w <= 0;
                if (behindCam)
                {
                    o.vertex = discardVec;
                    return o;
                }

                o.color = view.color;

                uint idx = vtxID;

                switch (idx) {
                    case 0: o.pos = float2(-1, -1); break;
                    case 1: o.pos = float2(-1, 1); break;
                    case 2: o.pos = float2(1, -1); break;
                    case 3: o.pos = float2(-1, 1); break;
                    case 4: o.pos = float2(1, -1); break;
                    case 5: o.pos = float2(1, 1); break;
                }
                // float2 quadPos = float2(idx&1, (idx>>1)&1) * 2.0 - 1.0;
                // quadPos *= 2;
                o.pos *= 2;
                // o.pos = quadPos;
                    // float2 c = centerClipPos.ww / _ScreenParams.xy;
                    // float2 deltaScreenPos = (o.pos.x * view.axis1 + o.pos.y * view.axis2) * c;
                float2 deltaScreenPos = (o.pos.x * view.axis1 + o.pos.y * view.axis2) * 2 / _ScreenParams.xy;
		        o.vertex = centerClipPos;
                o.vertex.xy += deltaScreenPos * centerClipPos.w;

                // SplatCovariance cov = CalcCovariance(quat, scale);
                // SplatCorner corner;
                // if (!InitCorner(source, cov, center, corner))
                // {
                //     o.vertex = discardVec;
                //     return o;
                // }

                // #ifndef SH_BANDS_0
                // // calculate the model-space view direction
                // float3 dir = normalize(mul(center.view, (float3x3)center.modelView));
                // float3 sh[SH_COEFFS];
                // for (int i = 0; i < SH_COEFFS; i++)
                //     sh[i] = _SHBuffer[source.id * SH_COEFFS + i];
                // color.rgb += EvalSH(sh, dir);
                // #endif

                // ClipCorner(corner, color.w);

                // o.vertex = center.proj + float4(corner.offset.x, _ProjectionParams.x * corner.offset.y, 0, 0);
                // o.color = color;
                // o.uv = corner.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float power = -dot(i.pos, i.pos);
	            half alpha = exp(power);
                alpha = saturate(alpha * i.color.a);
                return half4(i.color.rgb * alpha, alpha);

                // float A = dot(i.uv, i.uv);

                // #ifdef SHADER_API_DESKTOP
                // if (A > 1.0) discard;
                // #endif

                // float alpha = exp(-A * 4.0) * i.color.a;

                // #ifdef SHADER_API_DESKTOP
                // if (alpha < 1.0 / 255.0) discard;
                // #endif

                // if (_GammaToLinear)
                //     return half4(GammaToLinearSpace(i.color.rgb) * alpha, alpha);
                // return half4(i.color.rgb * alpha, alpha);
            }
            ENDHLSL


        }
    }
}