# Rendering

HexFall targets a PlayStation 1 look (see DD-003). Three layers produce it.

## Pipeline

1. **Low-resolution render**: both URP assets (`PC_RPAsset`, `Mobile_RPAsset`) render at scale 0.5 with Point upscaling and no MSAA, so pixels stay hard-edged.
2. **`HexFall/PSX Lit`** (object shader): vertex snapping, affine texture mapping, per-vertex lighting.
3. **`HexFall/PSX Post`** (full-screen pass): color depth reduction and ordered dithering, run by a `FullScreenPassRendererFeature` on `PC_Renderer` and `Mobile_Renderer` (injection point: after post-processing). It runs on the low-resolution target before the upscale, so one dither cell equals one rendered pixel. UI Toolkit is drawn as an overlay afterwards and is not affected.

## PSX Lit parameters (per material)

| Property | Default | Effect |
|---|---|---|
| `_SnapResolution` | 320, 180 | Virtual grid that clip-space vertices snap to. Lower = more wobble. |
| `_AffineStrength` | 0.6 | 0 = perspective-correct UVs, 1 = fully affine (texture swimming on large quads). |
| `_BaseMap` / `_BaseColor` | - | Albedo texture and tint. |
| `_EmissionColor` (HDR) | black | Added after lighting; unaffected by lights. |
| `_Cull` | Back | Face culling mode. |

Lighting is Gouraud: ambient (SH), main light and additional lights are evaluated per vertex. Only the main light shadow is sampled per pixel. Because lights are per vertex, point/spot lights only show on a surface as far as its mesh density allows. WebGL 2 compatible; no extra keywords beyond URP's lighting set.

## PSX Post parameters (`M_PSXPost`)

| Property | Default | Effect |
|---|---|---|
| `_ColorLevels` | 32 | Levels per channel (32 is roughly 15-bit color). Lower = coarser banding. |
| `_DitherStrength` | 0.75 | 4x4 Bayer dither amplitude in color steps (0 = hard banding). |
| `_Saturation` | 1 | Saturation multiplier applied before quantization. |

## Material usage

| Shader | Materials |
|---|---|
| PSX Lit | `M_Wizard`, `M_WizardStaff`, `M_ArenaFloor`, `M_ArenaStone`, `M_ArenaRune` |
| URP Unlit (additive) | Moonbeam materials (banded by PSX Post only) |

New 3D materials should use `HexFall/PSX Lit`.
