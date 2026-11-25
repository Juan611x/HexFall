# Wind shield art

Mesh and textures of the parry shield (an air sphere around the wizard), in the PSX style of DD-003. The sphere's air is drawn by two low-res wind images (Kenney Particle Pack, CC0) that procedural noise only warps; a tiny CC0-photo sprite is used by the wisp particles.

| File | Role |
|---|---|
| `build_air_shield_mesh.py` | Blender headless: builds `AirShield.fbx`, a 32 x 16 UV sphere (960 triangles, smooth shading, radius 0.5) with its poles on the vertical (Unity Y) axis, which is the shield's spin axis. |
| `build_current_ring_mesh.py` | Blender headless: builds `AirCurrentRing.fbx`, the thin band (256 triangles, radius 0.5, 0.16 tall, U around, V across) that each wind current is drawn on. Its axis is on Unity Y. |
| `source/Snow_blowing_under_lamp_1_1280px.jpg` | Source photo "Snow blowing under lamp 1" by W.carter (CC0), see `source/SOURCE.txt`. |
| `derive_air_texture.py` | Deterministic (Pillow): derives `T_AirWisp.png` (16x8) from the photo. |
| `derive_air_twirls.py` | Deterministic (Blender headless, numpy; no Pillow needed): derives `T_AirTwirl_A.png`, `T_AirTwirl_B.png`, `T_AirTrace.png` (128x128, white RGB, real alpha from luminance, smooth gradient) from the Kenney originals in `game/Assets/ThirdParty/Kenney/ParticlePack/`. |
| `requirements.txt` | Pinned dependency (`pillow`). |

The shield look lives in Unity, not here: `game/Assets/_Project/VFX/WindShield/Shaders/SG_WindShield.shadergraph` (URP Unlit, additive, ZWrite off) with its math in `WindShieldCore.hlsl`, called from two Custom Function nodes in File mode.

- `WindShield_Snap` (vertex): PSX vertex snapping. The Shader Graph Position block is object space, so the vertex is moved to clip space, snapped to the virtual 320x180 grid, and carried back to object space with `UNITY_MATRIX_I_VP` and `TransformWorldToObject`; the pipeline then transforms it again, so only the snap remains.
- `WindShield_Surface` (fragment): two Texture2D layers (`_TwirlTexA` = `twirl_03`, `_TwirlTexB` = `twirl_02`) sampled at u = turns around Y x a WHOLE tile count (`_TwirlTilingA` 4x2, `_TwirlTilingB` 3x2, so the atan2 seam lands on a tile border), v = latitude, from the object-space direction (not mesh UVs, so nothing pinches at the poles). Layer A scrolls one way at `_FlowSpeed`, layer B the other way at 0.6x. Value noise on a 128 x 64 angular grid (`_NoiseCells`) only warps the UVs by `_WarpAmount` so the strokes ripple and never sit still. The texture alpha is the stroke; `_Density` is its gain, `_CoreAlpha` how much shows facing the camera, plus fresnel rim, pole fade and the spiral `_Build` dissolve. `_AlphaSteps` posterisation is optional and off (0): it read as flat blocks. `MagicShield` drives `_Build` (0 invisible, 1 complete) and the `_BaseColor` alpha through a MaterialPropertyBlock.

Wind currents (part c of the air shield): `WindCurrents` (child of the shield, 4 pooled ring renderers sharing `M_WindCurrent`) places each ring at the height and tilt that `WindCurrentScheduler` picks (scaled to hug the sphere, `WindCurrentArc.RingRadius`), and `WindCurrentArc` turns a current's life progress into an arc: the head sweeps around the sphere, the arc grows, holds and fades. The shield build gates them (`WindCurrentArc.Gate`: closed below 40 % build, open at 100 %). The hand-written `HexFall/WindCurrent` shader draws only the arc (`_Tail`..`_Head` in turns, noisy borders) with `T_AirTrace` sliding along the band; `_Head/_Tail/_Fade/_Seed` come from a MaterialPropertyBlock per ring. The PSX snap and the value noise live in `WindShieldShared.hlsl`, used by both this shader and the sphere's include.

## Rebuild

```sh
python -m venv .venv && .venv/Scripts/pip install -r art/vfx/wind_shield/requirements.txt
.venv/Scripts/python -I art/vfx/wind_shield/derive_air_texture.py art/vfx/wind_shield/source/Snow_blowing_under_lamp_1_1280px.jpg game/Assets/_Project/VFX/WindShield/Textures
blender -b --factory-startup --python art/vfx/wind_shield/build_air_shield_mesh.py
blender -b --factory-startup --python art/vfx/wind_shield/build_current_ring_mesh.py
blender -b --factory-startup --python art/vfx/wind_shield/derive_air_twirls.py -- game/Assets/ThirdParty/Kenney/ParticlePack game/Assets/_Project/VFX/WindShield/Textures
```

Unity keeps the import settings in the `.meta` files (point filter, no mipmaps, alpha is transparency), so regenerating over the same paths keeps the references. The mesh keeps its name `AirShield`, so the prefab reference survives a re-export.

License: the photo and the Kenney Particle Pack are CC0; the scripts, the sprites derived from them, the shader and the mesh are covered in `docs/ASSET_CREDITS.md`.
