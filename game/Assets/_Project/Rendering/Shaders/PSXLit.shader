Shader "HexFall/PSX Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _SnapResolution("Vertex Snap Resolution", Vector) = (320, 180, 0, 0)
        _AffineStrength("Affine Texture Strength", Range(0, 1)) = 0.6
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Identical in every pass to keep the shader SRP Batcher compatible.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            float4 _SnapResolution;
            half _AffineStrength;
        CBUFFER_END

        // Snaps the post-divide xy position to a virtual low-resolution grid.
        float4 PSXSnap(float4 positionCS)
        {
            float2 grid = max(_SnapResolution.xy, 1.0) * 0.5;
            float2 ndc = positionCS.xy / positionCS.w;
            ndc = round(ndc * grid) / grid;
            positionCS.xy = ndc * positionCS.w;
            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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
                float3 uvAffine : TEXCOORD1;      // uv * w, w
                half3 ambientAdd : TEXCOORD2;     // ambient + additional lights
                half3 mainDiffuse : TEXCOORD3;    // main light, shadowed per pixel
                float3 positionWS : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(input.normalOS);

                output.positionCS = PSXSnap(vp.positionCS);
                output.positionWS = vp.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uvAffine = float3(output.uv * output.positionCS.w, output.positionCS.w);
                output.fogFactor = ComputeFogFactor(vp.positionCS.z);

                half3 normalWS = half3(vn.normalWS);
                half3 ambientAdd = SampleSH(normalWS);

                Light mainLight = GetMainLight();
                output.mainDiffuse = mainLight.color * saturate(dot(normalWS, mainLight.direction));

            #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
                InputData inputData = (InputData)0;
                inputData.positionWS = vp.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(vp.positionCS);

                uint lightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint i = 0; i < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); i++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light l = GetAdditionalLight(i, vp.positionWS);
                    ambientAdd += l.color * (l.distanceAttenuation * saturate(dot(normalWS, l.direction)));
                }
                #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, vp.positionWS);
                    ambientAdd += l.color * (l.distanceAttenuation * saturate(dot(normalWS, l.direction)));
                LIGHT_LOOP_END
            #endif

                output.ambientAdd = ambientAdd;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uvAffine = input.uvAffine.xy / input.uvAffine.z;
                float2 uv = lerp(input.uv, uvAffine, _AffineStrength);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half shadow = MainLightRealtimeShadow(shadowCoord);

                half3 lighting = input.ambientAdd + input.mainDiffuse * shadow;
                half3 color = albedo.rgb * lighting + _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = PSXSnap(TransformObjectToHClip(input.positionOS.xyz));
                return output;
            }

            half frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
}
