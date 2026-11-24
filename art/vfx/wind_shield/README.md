# Wind shield art

Mesh and textures of the parry shield (an air sphere around the wizard), in the PSX style of DD-003. The sphere's air is procedural (Shader Graph, no photo); a tiny CC0-photo sprite is still used by the wisp particles.

| File | Role |
|---|---|
| `build_air_shield_mesh.py` | Blender headless: builds `AirShield.fbx`, a 32 x 16 UV sphere (960 triangles, smooth shading, radius 0.5) with its poles on the vertical (Unity Y) axis, which is the shield's spin axis. |
| `source/Snow_blowing_under_lamp_1_1280px.jpg` | Source photo "Snow blowing under lamp 1" by W.carter (CC0), see `source/SOURCE.txt`. |
| `derive_air_texture.py` | Deterministic (Pillow): derives `T_AirWisp.png` (16x8) from the photo. |
| `requirements.txt` | Pinned dependency (`pillow`). |

The shield look lives in Unity, not here: `game/Assets/_Project/VFX/WindShield/Shaders/SG_WindShield.shadergraph` (URP Unlit, additive, ZWrite off) with its math in `WindShieldCore.hlsl`, called from two Custom Function nodes in File mode.

- `WindShield_Snap` (vertex): PSX vertex snapping. The Shader Graph Position block is object space, so the vertex is moved to clip space, snapped to the virtual 320x180 grid, and carried back to object space with `UNITY_MATRIX_I_VP` and `TransformWorldToObject`; the pipeline then transforms it again, so only the snap remains.
- `WindShield_Surface` (fragment): veins from procedural noise sampled from the object-space direction (not mesh UVs, so nothing pinches at the poles), quantised to a 128 x 64 angular grid, two animated layers, posterised alpha, fresnel rim, pole fade and the spiral `_Build` dissolve. `MagicShield` drives `_Build` (0 invisible, 1 complete) and the `_BaseColor` alpha through a MaterialPropertyBlock.

## Rebuild

```sh
python -m venv .venv && .venv/Scripts/pip install -r art/vfx/wind_shield/requirements.txt
.venv/Scripts/python -I art/vfx/wind_shield/derive_air_texture.py art/vfx/wind_shield/source/Snow_blowing_under_lamp_1_1280px.jpg game/Assets/_Project/VFX/WindShield/Textures
blender -b --factory-startup --python art/vfx/wind_shield/build_air_shield_mesh.py
```

Unity keeps the import settings in the `.meta` files (wisp: point filter, no mipmaps), so regenerating over the same paths keeps the references. The mesh keeps its name `AirShield`, so the prefab reference survives a re-export.

License: the photo is CC0; the scripts, the wisp sprite derived from it, the shader and the mesh are covered in `docs/ASSET_CREDITS.md`.
