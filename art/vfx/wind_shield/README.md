# Wind shield texture

Generates the streak mask used by the `HexFall/WindShield` shader (the parry shield, an air sphere around the wizard).

| File | Role |
|---|---|
| `generate_wind_streaks.py` | Deterministic generator (Pillow): thin, curved, tapered, mostly horizontal streaks on black, grayscale. Tileable in X, so it wraps around the sphere's vertical axis. The same seed gives the same image. |
| `requirements.txt` | Pinned dependency (`pillow`). |

## Rebuild

```sh
python -m venv .venv && .venv/Scripts/pip install -r art/vfx/wind_shield/requirements.txt
.venv/Scripts/python art/vfx/wind_shield/generate_wind_streaks.py game/Assets/_Project/VFX/WindShield/Textures/T_WindStreaks.png --seed 11
```

Unity keeps its import settings (`.meta`: wrap U repeat, wrap V clamp, sRGB off, mipmaps on), so regenerating over the same path keeps the material reference.

## Parameters

| Option | Default | Meaning |
|---|---|---|
| `out` (positional) | none | Output PNG path. |
| `--seed` | `11` | Streak layout seed. |
| `--width` / `--height` | `512` / `256` | Output size; 2:1 matches a full turn around a sphere. |
| `--count` | `44` | Number of streaks. |

License: self-made, see `docs/ASSET_CREDITS.md`.
