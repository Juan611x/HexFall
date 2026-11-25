// Pieces of the air shield shared by SG_WindShield (through WindShieldCore.hlsl) and the hand-written
// HexFall/WindCurrent shader, so nothing is duplicated:
//
//  WS_Hash / WS_Noise  smooth 3D value noise in [0,1].
//  WindShield_Snap     vertex stage. PSX vertex snapping (same grid rule as HexFall/PSX Lit): the vertex is snapped in
//                      clip space, then carried back to object space, because the Shader Graph Position block is
//                      object space and the pipeline transforms it again. The round trip is exact apart from the snap.
//
// Needs the URP Core library (TransformObjectToHClip and friends) included first.

#ifndef HEXFALL_WIND_SHIELD_SHARED_INCLUDED
#define HEXFALL_WIND_SHIELD_SHARED_INCLUDED

float WS_Hash(float3 p)
{
    p = frac(p * 0.3183099 + float3(0.71, 0.113, 0.419));
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Smooth 3D value noise in [0,1].
float WS_Noise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = WS_Hash(i);
    float n100 = WS_Hash(i + float3(1, 0, 0));
    float n010 = WS_Hash(i + float3(0, 1, 0));
    float n110 = WS_Hash(i + float3(1, 1, 0));
    float n001 = WS_Hash(i + float3(0, 0, 1));
    float n101 = WS_Hash(i + float3(1, 0, 1));
    float n011 = WS_Hash(i + float3(0, 1, 1));
    float n111 = WS_Hash(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
}

void WindShield_Snap_float(float3 positionOS, float2 resolution, out float3 snappedOS)
{
#ifdef SHADERGRAPH_PREVIEW
    snappedOS = positionOS;
#else
    float4 clip = TransformObjectToHClip(positionOS);
    float2 grid = max(resolution, 1.0) * 0.5;
    float2 ndc = clip.xy / clip.w;
    ndc = round(ndc * grid) / grid;
    clip.xy = ndc * clip.w;
    float4 world = mul(UNITY_MATRIX_I_VP, clip);
    snappedOS = TransformWorldToObject(world.xyz / world.w);
#endif
}

#endif
