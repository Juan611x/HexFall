# Wind shield art

Textures and mesh of the parry shield (an air sphere around the wizard), in the PSX style of DD-003: a real CC0 photo reduced to a tiny point-filtered texture on a low-poly mesh.

| File | Role |
|---|---|
| `source/Snow_blowing_under_lamp_1_1280px.jpg` | Source photo "Snow blowing under lamp 1" by W.carter (CC0), see `source/SOURCE.txt`. |
| `derive_air_texture.py` | Deterministic (Pillow): derives `T_AirShield.png` (128×64, tileable in U) and `T_AirWisp.png` (16×8) from the photo. |
| `build_air_shield_mesh.py` | Blender headless: builds `AirShield.fbx`, a flat-shaded 100-triangle UV sphere (U around the vertical axis, V pole to pole). |
| `requirements.txt` | Pinned dependency (`pillow`). |

## Rebuild

```sh
python -m venv .venv && .venv/Scripts/pip install -r art/vfx/wind_shield/requirements.txt
.venv/Scripts/python -I art/vfx/wind_shield/derive_air_texture.py art/vfx/wind_shield/source/Snow_blowing_under_lamp_1_1280px.jpg game/Assets/_Project/VFX/WindShield/Textures
blender -b --factory-startup --python art/vfx/wind_shield/build_air_shield_mesh.py
```

Unity keeps the import settings in the `.meta` files (point filter, no mipmaps, max size 128; shield: wrap U repeat, wrap V clamp, sRGB off), so regenerating over the same paths keeps the material references.

License: the photo is CC0; the scripts, textures derived from it and the mesh are covered in `docs/ASSET_CREDITS.md`.
