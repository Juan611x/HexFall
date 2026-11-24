// One horizontal wind current wrapping the air shield: a thin ring band whose U runs around the circumference and V
// across the band. Only an arc of the ring is visible (_Tail.._Head, in turns, wrapped); it fades in/out along the
// arc and at the band edges with a noisy border, and the low-res wind trace (Kenney trace_04, alpha = stroke) slides
// along it. Additive, ZWrite off, PSX vertex snap shared with SG_WindShield (WindShieldShared.hlsl).
//
// Hand-written URP HLSL rather than a Shader Graph: the graph of the sphere is a generated 80 KB asset, and this
// shader is a handful of arithmetic lines that is easier to read and diff as text. WindCurrents drives _Head, _Tail,
// _Fade and _Seed per ring through a MaterialPropertyBlock; everything else lives in the shared material.
Shader "HexFall/WindCurrent"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.96, 1, 1, 0.9)
        [MainTexture] _TraceTex("Trace (alpha = stroke)", 2D) = "white" {}
        _TraceTiling("Trace tiles around the ring (whole number)", Float) = 3
        _ScrollSpeed("Trace scroll (tiles per second / tiling)", Float) = 0.35
        _Head("Arc head (turns)", Float) = 0.5
        _Tail("Arc tail (turns)", Float) = 0.1
        _Fade("Fade", Range(0, 1)) = 1
        _EdgeNoise("Noisy edge amount", Range(0, 0.6)) = 0.25
        _Seed("Noise seed", Float) = 0
        _SnapResolution("Vertex Snap Resolution", Vector) = (320, 180, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/VFX/WindShield/Shaders/WindShieldShared.hlsl"

            TEXTURE2D(_TraceTex);
            SAMPLER(sampler_TraceTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _TraceTex_ST;
                half4 _BaseColor;
                float _TraceTiling;
                float _ScrollSpeed;
                float _Head;
                float _Tail;
                float _Fade;
                float _EdgeNoise;
                float _Seed;
                float4 _SnapResolution;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 snapped;
                WindShield_Snap_float(input.positionOS.xyz, _SnapResolution.xy, snapped);
                output.positionCS = TransformObjectToHClip(snapped);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;
                float time = _Time.y;

                // Drifting noise: bends the arc ends and the band borders so they flutter like air, never a clean cut.
                float n1 = WS_Noise(float3(u * 14.0 + _Seed, v * 3.0, time * 0.9 + _Seed * 0.37));
                float n2 = WS_Noise(float3(u * 9.0 - _Seed, v * 5.0 + 7.0, time * 0.7 - _Seed * 0.21));

                // Position along the visible arc: 0 at the tail, 1 at the head, outside [0,1] elsewhere on the ring.
                float length = clamp(_Head - _Tail, 0.0, 1.0);
                float along = frac(u - _Tail) / max(length, 1e-4);
                along += (n1 - 0.5) * _EdgeNoise;
                float arc = smoothstep(0.0, 0.45, along) * (1.0 - smoothstep(0.82, 1.0, along));

                // Across the band: strongest on the centre line, soft noisy edges.
                float across = smoothstep(0.0, 0.5, 1.0 - abs(v * 2.0 - 1.0) + (n2 - 0.5) * _EdgeNoise);

                // The wind trace runs along the ring (its long axis is the texture's V) and slides around it.
                // frac keeps the offset small for long sessions; a whole tile count keeps the wrap seamless.
                float scroll = frac(time * _ScrollSpeed);
                float2 traceUV = float2(v, (u + scroll) * _TraceTiling);
                float stroke = SAMPLE_TEXTURE2D_LOD(_TraceTex, sampler_TraceTex, traceUV, 0).a;

                float alpha = _BaseColor.a * saturate(stroke * 1.5) * arc * across * _Fade;
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
