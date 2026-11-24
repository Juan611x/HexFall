// Math of the air shield (SG_WindShield), kept out of the graph so the graph stays three nodes wide.
// Called from Custom Function nodes in File mode: the function names below are the node names plus "_float".
//
//  WindShield_Snap     vertex stage. PSX vertex snapping (same grid rule as HexFall/PSX Lit): the vertex is snapped in
//                      clip space, then carried back to object space, because the Shader Graph Position block is
//                      object space and the pipeline transforms it again. The round trip is exact apart from the snap.
//  WindShield_Surface  fragment stage. Procedural wind veins from the object-space direction (no mesh UVs, so nothing
//                      pinches at the poles), pixelated to a coarse grid, posterised, with fresnel rim, pole fade and
//                      the spiral build (_Build) that assembles the shield bottom to top.
//
// No textures, no compute: WebGL2 friendly.

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

float2 WS_RotateXZ(float2 p, float turns)
{
    float s, c;
    sincos(turns * WS_TWO_PI, s, c);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

// A thin ridge where the noise crosses 0.5, stretched along the horizontal flow. Returns 0..1.
float WS_Vein(float3 dir, float turns, float stretch, float scale, float warp, float threshold)
{
    float3 p = dir;
    p.xz = WS_RotateXZ(p.xz, turns + p.y * 0.35); // swirl: higher rings turn a little ahead of lower ones
    p.xz *= scale;                                  // slow around the sphere...
    p.y = p.y * stretch + warp;                     // ...fast from bottom to top: thin horizontal veins
    float n = WS_Noise(p);
    float ridge = 1.0 - abs(n * 2.0 - 1.0);
    return saturate((ridge - threshold) / max(1.0 - threshold, 1e-3));
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

    // PSX texels: snap the direction to a coarse angular grid fixed on the sphere; the flow moves through it.
    float2 cells = max(noiseCells, float2(4, 4));
    float2 cell = (floor(float2(around, height) * cells) + 0.5) / cells;
    float phi = (cell.y - 0.5) * 3.14159265;
    float theta = (cell.x - 0.5) * WS_TWO_PI;
    float3 qdir = float3(cos(phi) * cos(theta), sin(phi), cos(phi) * sin(theta));

    // Two layers: a stretched horizontal flow, and a slower counter-turning layer that also warps the first.
    float spin = (1.0 - build) * buildSpin; // extra turns while assembling: the swirl spins faster
    float threshold = 1.0 - density * 0.3;
    float warpNoise = WS_Noise(float3(qdir.x * 1.7 - time * 0.05, qdir.y * 3.0, qdir.z * 1.7) + 7.3);
    float veinA = WS_Vein(qdir, time * flowSpeed + spin, 9.0, 1.5, (warpNoise - 0.5) * 0.9 + time * 0.045, threshold);
    float veinB = WS_Vein(qdir, -time * flowSpeed * 0.55 - spin * 0.6, 14.0, 2.2, warpNoise * 0.6 - time * 0.03 + 3.1, threshold);
    float veins = max(veinA, veinB * 0.75);

    // Fresnel: strong at the silhouette, faint facing the camera so the wizard stays visible.
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

    float mask = saturate(veins * shell * 1.4 + rim * 0.15) * polar * built;
    // The fringe is broken up (veins plus a few noise texels) so it reads as sparkling air, not a flat band.
    mask = saturate(mask + fringe * 0.5 * saturate(veins * 1.5 + step(0.62, jitter)) * polar);
    float steps = max(alphaSteps, 2.0);
    mask = floor(mask * steps + 0.5) / steps; // posterised alpha, PSX style

    color = baseColor.rgb * (1.0 + rim * 0.8 + fringe * 0.5);
    alpha = baseColor.a * mask;
}

#endif
