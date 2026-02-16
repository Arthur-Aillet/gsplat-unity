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
            #pragma use_dxc

            #include "UnityCG.cginc"

            #include "GaussianSplatting.hlsl"

            StructuredBuffer<uint> _OrderBuffer;
            uint _SplatCount;
            uint _SplatInstanceSize;

            struct SplatSource
            {
                uint order;
                uint id;
                float2 cornerUV;
            };

            struct v2f
            {
                half4 col : COLOR0;
                float2 pos : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct SplatViewData {
                float3 pos;
                half2 rg;
                float2 axis1, axis2;
                half2 ba;
            };

            StructuredBuffer<SplatViewData> _SplatViewData;

            struct appdata
            {
                float4 vertex : POSITION;
                #if !defined(UNITY_INSTANCING_ENABLED) && !defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && !defined(UNITY_STEREO_INSTANCING_ENABLED)
                uint instanceID : SV_InstanceID;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            bool InitSource(appdata v, out SplatSource source)
            {
                #if !defined(UNITY_INSTANCING_ENABLED) && !defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && !defined(UNITY_STEREO_INSTANCING_ENABLED)
                source.order = v.instanceID * _SplatInstanceSize + asuint(v.vertex.z);
                #else
                source.order = unity_InstanceID * _SplatInstanceSize + asuint(v.vertex.z);
                #endif

                if (source.order >= _SplatCount)
                    return false;

                source.id = _OrderBuffer[source.order];
                source.cornerUV = float2(v.vertex.x, v.vertex.y);
                return true;
            }

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SplatSource source;
                if (!InitSource(v, source))
                {
                    o.vertex = asfloat(0x7fc00000);
                    return o;
                }

                SplatViewData view = _SplatViewData[source.id];
                float4 centerClipPos = mul(UNITY_MATRIX_VP, float4(view.pos, 1));

                bool behindCam = centerClipPos.w <= 0;
                if (behindCam)
                {
                    o.vertex = asfloat(0x7fc00000); // NaN discards the primitive
                    return o;
                }

                o.col = half4(view.rg, view.ba);

                float2 quadPos = source.cornerUV;

                o.pos = quadPos * 2;

                float2 deltaScreenPos = (quadPos.x * view.axis1 + quadPos.y * view.axis2) * 2 / _ScreenParams.xy;
                o.vertex = centerClipPos;
                o.vertex.xy += deltaScreenPos * centerClipPos.w;
                //FlipProjectionIfBackbuffer(o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float power = -dot(i.pos, i.pos);
                half alpha = exp(power);
                alpha = saturate(alpha * i.col.a);

                if (alpha < 1.0/255.0)
                    discard;

                half4 res = half4(i.col.rgb * alpha, alpha);
                return res;
            }
            ENDHLSL

        }
    }
}