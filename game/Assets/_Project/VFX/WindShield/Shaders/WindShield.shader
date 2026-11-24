// Parry shield shell: two layers of wind streaks wrapped around the object's vertical axis, scrolling at
// different speeds and tilts, weighted by a fresnel rim. Additive, unlit, single pass (WebGL2 friendly).
// _BaseColor.a is the overall opacity that MagicShield animates through its MaterialPropertyBlock.
Shader "HexFall/WindShield"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color (alpha = opacity)", Color) = (0.8, 1, 0.95, 0.4)
        [NoScaleOffset] _StreakTex("Streak Mask (R, tileable in U)", 2D) = "black" {}
        _RimPower("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength("Rim Strength", Range(0, 3)) = 1.2
        _CoreAlpha("Core Alpha (facing the camera)", Range(0, 1)) = 0.12
        _Layer1Speed("Layer 1 Speed (turns/s)", Float) = 0.12
        _Layer1Tilt("Layer 1 Tilt", Float) = 0.35
        _Layer1Scale("Layer 1 Scale (whole number)", Float) = 1
        _Layer2Speed("Layer 2 Speed (turns/s)", Float) = -0.07
        _Layer2Tilt("Layer 2 Tilt", Float) = -0.25
        _Layer2Scale("Layer 2 Scale (whole number)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "WindShield"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_StreakTex);
            SAMPLER(sampler_StreakTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _RimPower;
                half _RimStrength;
                half _CoreAlpha;
                float _Layer1Speed;
                float _Layer1Tilt;
                float _Layer1Scale;
                float _Layer2Speed;
                float _Layer2Tilt;
                float _Layer2Scale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionOS : TEXCOORD0; // unit direction from the object centre
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vp.positionCS;
                output.directionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(vp.positionWS);
                return output;
            }

            // u: turns around the vertical axis (wraps), height: -1..1 up the sphere.
            half Streaks(float turns, float height, float speed, float tilt, float scale)
            {
                float u = (turns + _Time.y * speed + height * tilt) * scale;
                // atan2 jumps at its seam, which would make the sampler pick the smallest mip there:
                // take the derivatives from whichever of two phase-shifted copies is continuous.
                float2 gradA = float2(ddx(frac(u)), ddy(frac(u)));
                float2 gradB = float2(ddx(frac(u + 0.5)), ddy(frac(u + 0.5)));
                bool useA = dot(gradA, gradA) <= dot(gradB, gradB);
                float2 dU = useA ? gradA : gradB;
                float2 dV = float2(ddx(height), ddy(height)) * 0.5;
                float2 uv = float2(u, height * 0.5 + 0.5);
                return SAMPLE_TEXTURE2D_GRAD(_StreakTex, sampler_StreakTex, uv, float2(dU.x, dV.x), float2(dU.y, dV.y)).r;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.directionOS);
                float turns = atan2(dir.z, dir.x) * (0.5 / PI);
                float height = dir.y;

                half streak = saturate(Streaks(turns, height, _Layer1Speed, _Layer1Tilt, _Layer1Scale)
                    + Streaks(turns, height, _Layer2Speed, _Layer2Tilt, _Layer2Scale) * 0.8h);
                // streaks pinch where the angle degenerates at the poles
                streak *= 1.0h - smoothstep(0.85h, 1.0h, abs(height));

                half nDotV = saturate(dot(normalize(input.normalWS), normalize(input.viewWS)));
                half rim = pow(1.0h - nDotV, _RimPower);
                half shell = saturate(_CoreAlpha + rim * _RimStrength);

                half mask = saturate(streak * shell * 1.6h + rim * _RimStrength * 0.2h);
                return half4(_BaseColor.rgb * (1.0h + rim), _BaseColor.a * mask);
            }
            ENDHLSL
        }
    }
}
