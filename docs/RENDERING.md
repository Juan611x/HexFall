# Rendering

HexFall targets a PlayStation 1 look (see DD-003). Three layers produce it.

## Pipeline

1. **Low-resolution render**: both URP assets (`PC_RPAsset`, `Mobile_RPAsset`) render at scale 0.5 with Point upscaling and no MSAA, so pixels stay hard-edged.
2. **`HexFall/PSX Lit`** (object shader): vertex snapping, affine texture mapping, per-vertex lighting.
3. **`HexFall/PSX Post`** (full-screen pass): color depth reduction and ordered dithering, run by a `FullScreenPassRendererFeature` on `PC_Renderer` and `Mobile_Renderer` (injection point: after post-processing). It runs on the low-resolution target before the upscale, so one dither cell equals one rendered pixel. The menu UI is composited into this pass too (see below).

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

## UI composite

UI Toolkit normally draws as a screen overlay after the camera, outside the post pass. To give the menu the same pixel grid, color depth and dither:

- `PsxUiTarget` (on the `MenuUI` object) instantiates the panel settings at runtime and renders the panel into a `RenderTexture` (`targetTexture`) sized like the URP render target: `screen size x renderScale` (truncated, same as URP). It rebuilds when the screen size or render scale changes and clears the texture every frame.
- The texture is published as global `_PSXUITex` plus `_PSXUIEnabled` (1 while bound). `HexFall/PSX Post` alpha-composites it (premultiplied) over the scene, point-sampled, before saturation, quantization and dither. With `_PSXUIEnabled` at 0 the shader output is unchanged.
- Input: `PanelSettings.SetScreenToPanelSpaceFunction` uses `ScreenToPanelMapper` (pure, unit-tested): flips the bottom-left screen origin to the top-left panel origin, scales screen size to texture size, and returns NaN outside the screen.
- Layout still scales from the 1280x720 reference, so the panel is minified when the texture is smaller. Texts that were 15-16 px (labels, hints, links, divider) are now 18 px to stay legible at half render scale. The old scanline overlay was removed: the dither already provides the grain and a per-pixel texture aliases at this resolution.

## Menu wizard cast

Any `Button` click or keyboard/gamepad submit in the menu panel fires the wizard `Cast` trigger (`MenuClickCastReaction` -> `IWizardAnimator` -> `AnimatorWizardAnimator`, wired in `MenuBootstrap`). The AnyState -> `Spell1` transition allows self transition, so each click restarts the cast.

## Material usage

| Shader | Materials |
|---|---|
| PSX Lit | `M_Wizard`, `M_WizardStaff`, `M_ArenaFloor`, `M_ArenaStone`, `M_ArenaRune` |
| URP Unlit (additive) | Moonbeam materials (banded by PSX Post only) |

New 3D materials should use `HexFall/PSX Lit`.
