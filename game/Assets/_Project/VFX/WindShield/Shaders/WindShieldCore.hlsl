// Math of the air shield (SG_WindShield), kept out of the graph so the graph stays three nodes wide.
// Called from Custom Function nodes in File mode: the function names below are the node names plus "_float".
//
//  WindShield_Snap     vertex stage. PSX vertex snapping (same grid rule as HexFall/PSX Lit): the vertex is snapped in
//                      clip space, then carried back to object space, because the Shader Graph Position block is
//                      object space and the pipeline transforms it again. The round trip is exact apart from the snap.
//  WindShield_Surface  fragment stage. The air is two low-res wind-twirl images (Kenney Particle Pack, CC0, alpha =
//                      stroke) tiled around the Y axis from the object-space direction (no mesh UVs, so nothing
//                      pinches at the poles) and scrolled in opposite directions. Procedural noise only warps their
//                      UVs, so the strokes ripple and never sit still; it no longer draws the shape. Plus fresnel
//                      rim, pole fade and the spiral build (_Build) that assembles the shield bottom to top.
//
// Two texture samples, no compute: WebGL2 friendly.

#ifndef HEXFALL_WIND_SHIELD_CORE_INCLUDED
#define HEXFALL_WIND_SHIELD_CORE_INCLUDED

#define WS_TWO_PI 6.28318530718

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

// Twirl UV: u = turns around the Y axis times a WHOLE tile count, so the atan2 seam (around jumps by 1) lands on a
// whole number of tiles and the image repeats seamlessly; v = latitude. The sample uses an explicit mip 0 (the textures
// have no mips), so the seam never produces a huge derivative either.
float2 WS_TwirlUV(float around, float height, float2 tiling, float turns, float2 warp)
{
    return float2((around + turns) * tiling.x, height * tiling.y) + warp;
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

void WindShield_Surface_float(
    float3 positionOS, float4 baseColor, float build, float density, float alphaSteps, float2 noiseCells,
    float flowSpeed, float rimPower, float coreAlpha, float poleFade, float buildSpin,
    UnityTexture2D twirlA, UnityTexture2D twirlB, float2 tilingA, float2 tilingB, float warpAmount,
    out float3 color, out float alpha)
{
#ifdef SHADERGRAPH_PREVIEW
    float3 viewDir = float3(0, 0, 1);
    float3 normalWS = normalize(positionOS);
    float time = 0.0;
#else
    float3 viewDir = normalize(_WorldSpaceCameraPos - TransformObjectToWorld(positionOS));
    float3 normalWS = TransformObjectToWorldNormal(normalize(positionOS));
    float time = _Time.y;
#endif

    float3 dir = normalize(positionOS);
    float height = asin(clamp(dir.y, -1.0, 1.0)) / 3.14159265 + 0.5; // 0 bottom pole .. 1 top pole
    float around = atan2(dir.z, dir.x) / WS_TWO_PI + 0.5;             // 0..1 around the Y axis

    // PSX texels for the warp: snap the direction to a coarse angular grid fixed on the sphere; the noise moves through it.
    float2 cells = max(noiseCells, float2(4, 4));
    float2 cell = (floor(float2(around, height) * cells) + 0.5) / cells;
    float phi = (cell.y - 0.5) * 3.14159265;
    float theta = (cell.x - 0.5) * WS_TWO_PI;
    float3 qdir = float3(cos(phi) * cos(theta), sin(phi), cos(phi) * sin(theta));

    // Noise warp: two octaves of drifting value noise bend the twirl UVs, so the strokes ripple and are never static.
    float3 drift = float3(time * 0.07, time * 0.03, -time * 0.05);
    float2 warp = float2(WS_Noise(qdir * 2.2 + drift), WS_Noise(qdir * 2.2 + 17.0 - drift)) - 0.5;
    warp += (float2(WS_Noise(qdir * 5.0 + 5.0 + drift * 2.0), WS_Noise(qdir * 5.0 + 31.0 - drift * 2.0)) - 0.5) * 0.5;
    warp *= warpAmount;

    // Two layers scrolling around the sphere in opposite directions; building makes the swirl spin faster.
    float spin = (1.0 - build) * buildSpin;
    float2 uvA = WS_TwirlUV(around, height, tilingA, time * flowSpeed + spin, warp);
    float2 uvB = WS_TwirlUV(around, height, tilingB, -time * flowSpeed * 0.6 - spin * 0.6, warp.yx);
    float strokesA = SAMPLE_TEXTURE2D_LOD(twirlA.tex, twirlA.samplerstate, uvA, 0).a;
    float strokesB = SAMPLE_TEXTURE2D_LOD(twirlB.tex, twirlB.samplerstate, uvB, 0).a;
    float pattern = 1.0 - (1.0 - strokesA) * (1.0 - strokesB * 0.8); // screen blend: overlaps brighten, never exceed 1
    pattern = smoothstep(0.12, 0.8, pattern); // clears the faint haze between arms; still a smooth ramp, not posterised

    // Fresnel: strongest at the silhouette; coreAlpha is how much of the pattern shows facing the camera.
    float nDotV = saturate(dot(normalize(normalWS), viewDir));
    float rim = pow(1.0 - nDotV, max(rimPower, 0.1));
    float shell = saturate(coreAlpha + rim);

    // Pole fade: both poles converge many triangles; fade them out softly.
    float polar = 1.0 - smoothstep(1.0 - max(poleFade, 1e-3), 1.0, abs(dir.y));

    // Spiral build, bottom to top: the edge climbs a spiral and is ragged because the same noise drives it.
    float spiral = frac(around + height * 1.0);
    float jitter = WS_Noise(qdir * 5.0 + 11.0);
    float field = saturate(height * 0.65 + spiral * 0.25 + (jitter - 0.5) * 0.22 + 0.1);
    float reach = lerp(-0.25, 1.25, saturate(build));
    float edge = 0.1;
    float built = saturate((reach - field) / edge);
    float fringe = saturate(1.0 - abs(reach - field - edge * 0.5) / (edge * 0.5)) * step(0.001, build) * step(build, 0.999);

    // density is the pattern gain (0.4 = as drawn, lower = thinner air). The gradient stays smooth.
    float mask = saturate(pattern * shell * density * 2.5 + rim * 0.12) * polar * built;
    // The fringe is broken up (strokes plus a few noise texels) so it reads as sparkling air, not a flat band.
    mask = saturate(mask + fringe * 0.5 * saturate(pattern * 1.5 + step(0.62, jitter)) * polar);
    if (alphaSteps >= 2.0)
        mask = floor(mask * alphaSteps + 0.5) / alphaSteps; // optional posterisation, off by default (read as flat blocks)

    color = baseColor.rgb * (1.0 + rim * 0.8 + fringe * 0.5);
    alpha = baseColor.a * mask;
}

#endif
