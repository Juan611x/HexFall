# Wind shield art

Mesh and textures of the parry shield (an air sphere around the wizard), in the PSX style of DD-003. The sphere's air is drawn by two low-res wind images (Kenney Particle Pack, CC0) that procedural noise only warps; the air motes around the sphere are 16 px curls cut from the same images.

| File | Role |
|---|---|
| `build_air_shield_mesh.py` | Blender headless: builds `AirShield.fbx`, a 32 x 16 UV sphere (960 triangles, smooth shading, radius 0.5) with its poles on the vertical (Unity Y) axis, which is the shield's spin axis. |
| `build_current_ring_mesh.py` | Blender headless: builds `AirCurrentRing.fbx`, the band (256 triangles, radius 0.5, 0.338 tall, U around, V across) that each wind current is drawn on. Its axis is on Unity Y. |
| `derive_air_twirls.py` | Deterministic (Blender headless, numpy;): derives `T_AirTwirl_A.png`, `T_AirTwirl_B.png`, `T_AirTrace.png` (128x128, white RGB, real alpha from luminance, smooth gradient) and `T_AirMote.png` (64x16: a 4-frame flipbook of 16x16 open arc fragments cropped from the twirl arms and the trace_04 streak, faded at the crop border, never a whole twirl) from the Kenney originals in `game/Assets/ThirdParty/Kenney/ParticlePack/`. |

The shield look lives in Unity, not here: `game/Assets/_Project/VFX/WindShield/Shaders/SG_WindShield.shadergraph` (URP Unlit, additive, ZWrite off) with its math in `WindShieldCore.hlsl`, called from two Custom Function nodes in File mode.

- `WindShield_Snap` (vertex): PSX vertex snapping. The Shader Graph Position block is object space, so the vertex is moved to clip space, snapped to the virtual 320x180 grid, and carried back to object space with `UNITY_MATRIX_I_VP` and `TransformWorldToObject`; the pipeline then transforms it again, so only the snap remains.
- `WindShield_Surface` (fragment): two Texture2D layers (`_TwirlTexA` = `twirl_03`, `_TwirlTexB` = `twirl_02`) sampled at u = turns around Y x a WHOLE tile count (`_TwirlTilingA` 4x2, `_TwirlTilingB` 3x2, so the atan2 seam lands on a tile border), v = latitude, from the object-space direction (not mesh UVs, so nothing pinches at the poles). Layer A scrolls one way at `_FlowSpeed`, layer B the other way at 0.6x. Value noise on a 128 x 64 angular grid (`_NoiseCells`) only warps the UVs by `_WarpAmount` so the strokes ripple and never sit still. The texture alpha is the stroke; `_Density` is its gain, `_CoreAlpha` how much shows facing the camera, plus fresnel rim, pole fade and the spiral `_Build` dissolve. `_AlphaSteps` posterisation is optional and off (0): it read as flat blocks. `MagicShield` drives `_Build` (0 invisible, 1 complete) and the `_BaseColor` alpha through a MaterialPropertyBlock.

Air motes (part b): `WindMotes` (child of the shield, one looping Shuriken system, Local space, regular billboards, `M_WindMote` additive near-white with the `T_AirMote` flipbook and a random frame/rotation per mote) is driven by `MagicShield` from the build phase through the pure `ShieldMotes.For(phase)` profile (spawn sphere radius, emission rate, radial and orbital speed): while ASSEMBLING they spawn outside the surface and drift in (negative radial), while HELD a few orbit on the surface, from DISASSEMBLING on emission stops, the live motes get a positive radial speed (they blow outward and fade) and `ShieldMotes.PuffCount` adds one outward puff. `MagicShield.Update` keeps stepping while motes or currents are alive, and a new Flash clears and restarts them. `WindBurst` is a one-shot outward ring of the same motes on a parried hit.

Wind currents (part c of the air shield): `WindCurrents` (child of the shield, 6 pooled ring renderers sharing `M_WindCurrent`, 3-8 allowed) places each ring at the height (-0.85..0.85 of the sphere radius, gap 0.22) and tilt that `WindCurrentScheduler` picks (scaled to hug the sphere, `WindCurrentArc.RingRadius`; the band thins out with `BandScaleY` and the tilt shrinks with `TiltScale` toward the poles so a ring never crosses them), and `WindCurrentArc` turns a current's life progress into an arc: the head sweeps around the sphere, the arc grows, holds and fades. The shield build gates them (`WindCurrentArc.Gate`: closed below 40 % build, open at 100 %). The hand-written `HexFall/WindCurrent` shader draws only the arc (`_Tail`..`_Head` in turns, noisy borders) with `T_AirTrace` sliding along the band; `_Head/_Tail/_Fade/_Seed` come from a MaterialPropertyBlock per ring. The PSX snap and the value noise live in `WindShieldShared.hlsl`, used by both this shader and the sphere's include.

## Rebuild

```sh
blender -b --factory-startup --python art/vfx/wind_shield/build_air_shield_mesh.py
blender -b --factory-startup --python art/vfx/wind_shield/build_current_ring_mesh.py
blender -b --factory-startup --python art/vfx/wind_shield/derive_air_twirls.py -- game/Assets/ThirdParty/Kenney/ParticlePack game/Assets/_Project/VFX/WindShield/Textures
```

Unity keeps the import settings in the `.meta` files (point filter, no mipmaps, alpha is transparency), so regenerating over the same paths keeps the references. The mesh keeps its name `AirShield`, so the prefab reference survives a re-export.

License: the Kenney Particle Pack is CC0; the scripts, the sprites derived from them, the shader and the mesh are covered in `docs/ASSET_CREDITS.md`.
