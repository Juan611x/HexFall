Shader "HexFall/PSX Post"
{
    Properties
    {
        _ColorLevels("Color Levels Per Channel", Range(2, 256)) = 32
        _DitherStrength("Dither Strength", Range(0, 1)) = 1
        _Saturation("Saturation", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "PSXPost"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ColorLevels;
            float _DitherStrength;
            float _Saturation;

            // Set globally by PsxUiTarget; 0 means no UI texture is bound.
            TEXTURE2D(_PSXUITex);
            float _PSXUIEnabled;

            static const float kBayer4[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;

                if (_PSXUIEnabled > 0.5)
                {
                    // The panel is rendered at the same size as this target, so one UI pixel is one scene pixel.
                    half4 ui = SAMPLE_TEXTURE2D_LOD(_PSXUITex, sampler_PointClamp, uv, 0);
                    color = color * (1.0 - ui.a) + ui.rgb;
                }

                half luma = dot(color, half3(0.299, 0.587, 0.114));
                color = lerp(luma.xxx, color, _Saturation);

                // One dither cell per rendered pixel of the source texture.
                uint2 pixel = (uint2)floor(uv * _BlitTexture_TexelSize.zw) & 3u;
                float threshold = (kBayer4[pixel.y * 4u + pixel.x] + 0.5) / 16.0 - 0.5;

                float steps = max(_ColorLevels - 1.0, 1.0);
                float3 scaled = saturate(color) * steps + threshold * _DitherStrength;
                color = (half3)(floor(scaled + 0.5) / steps);
                return half4(saturate(color), 1);
            }
            ENDHLSL
        }
    }
}
