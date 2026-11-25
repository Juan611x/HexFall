// Parry shield shell in the PSX style (DD-003): a low-poly faceted sphere whose mesh UVs (U around the vertical
// axis, V pole to pole) carry two scrolling layers of a tiny point-filtered wind photo, weighted by a fresnel rim.
// Vertex snapping and affine UV warping match HexFall/PSX Lit, and the alpha is posterised to a few steps.
// Additive, unlit, single pass (WebGL2 friendly, SRP Batcher compatible).
// _BaseColor.a is the overall opacity that MagicShield animates through its MaterialPropertyBlock.
Shader "HexFall/WindShield"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color (alpha = opacity)", Color) = (0.8, 1, 0.95, 0.4)
        [NoScaleOffset] _StreakTex("Wind Mask (R, point filtered, tileable in U)", 2D) = "black" {}
        _RimPower("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength("Rim Strength", Range(0, 3)) = 1.2
        _CoreAlpha("Core Alpha (facing the camera)", Range(0, 1)) = 0.12
        _Layer1Speed("Layer 1 Speed (turns/s)", Float) = 0.12
        _Layer1Tilt("Layer 1 Tilt", Float) = 0.35
        _Layer1Scale("Layer 1 Scale (whole number)", Float) = 1
        _Layer2Speed("Layer 2 Speed (turns/s)", Float) = -0.07
        _Layer2Tilt("Layer 2 Tilt", Float) = -0.25
        _Layer2Scale("Layer 2 Scale (whole number)", Float) = 2
        _SnapResolution("Vertex Snap Resolution", Vector) = (320, 180, 0, 0)
        _AffineStrength("Affine Texture Strength", Range(0, 1)) = 0.6
        _AlphaSteps("Alpha Steps", Range(2, 8)) = 5
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
            SAMPLER(sampler_StreakTex); // the texture imports with Point filter and U repeat

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
                float4 _SnapResolution;
                half _AffineStrength;
                half _AlphaSteps;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 uvAffine : TEXCOORD1; // uv * w, w
                float3 normalWS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
            };

            // Snaps the post-divide xy position to a virtual low-resolution grid (same as PSX Lit).
            float4 PSXSnap(float4 positionCS)
            {
                float2 grid = max(_SnapResolution.xy, 1.0) * 0.5;
                float2 ndc = positionCS.xy / positionCS.w;
                ndc = round(ndc * grid) / grid;
                positionCS.xy = ndc * positionCS.w;
                return positionCS;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = PSXSnap(vp.positionCS);
                output.uv = input.uv;
                output.uvAffine = float3(input.uv * output.positionCS.w, output.positionCS.w);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(vp.positionWS);
                return output;
            }

            // uv.x turns around the vertical axis (wraps), uv.y runs pole to pole.
            half Streaks(float2 uv, float speed, float tilt, float scale)
            {
                float u = (uv.x + _Time.y * speed + (uv.y - 0.5) * tilt) * scale;
                return SAMPLE_TEXTURE2D(_StreakTex, sampler_StreakTex, float2(u, uv.y)).r;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = lerp(input.uv, input.uvAffine.xy / input.uvAffine.z, _AffineStrength);

                // Two layers, then a contrast curve: the photo is mid-grey mottled, only its bright gusts should show.
                half streak = max(Streaks(uv, _Layer1Speed, _Layer1Tilt, _Layer1Scale),
                    Streaks(uv, _Layer2Speed, _Layer2Tilt, _Layer2Scale) * 0.85h);
                streak = saturate((streak - 0.3h) * 2.0h);

                half nDotV = saturate(dot(normalize(input.normalWS), normalize(input.viewWS)));
                half rim = pow(1.0h - nDotV, _RimPower);
                half shell = saturate(_CoreAlpha + rim * _RimStrength);

                half mask = saturate(streak * shell * 1.6h + rim * _RimStrength * 0.35h);
                // Posterised mask (a few alpha steps, PSX look); the overall opacity MagicShield animates stays smooth.
                mask = floor(mask * _AlphaSteps + 0.5h) / _AlphaSteps;
                return half4(_BaseColor.rgb * (1.0h + rim), _BaseColor.a * mask);
            }
            ENDHLSL
        }
    }
}
