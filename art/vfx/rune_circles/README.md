# Rune circle textures

Generates the glowing rune ring textures used by the rune circle shader (`SG_RuneCircle`). The orange one ships from the Flex Unit tutorial pack; the purple teleport circle is generated here.

| File | Role |
|---|---|
| `generate_rune_circle.py` | Deterministic generator (Pillow): concentric rings, procedural runes and a glow, RGB on black. The same seed and colors give the same image. |
| `requirements.txt` | Pinned dependency (`pillow`). |

## Rebuild

```sh
python -m venv .venv && .venv/Scripts/pip install -r art/vfx/rune_circles/requirements.txt
.venv/Scripts/python art/vfx/rune_circles/generate_rune_circle.py game/Assets/_Project/VFX/RuneCircles/Textures/T_RuneCircle_Teleport.png --seed 7 --core f3e8ff --glow 9333ea --size 512
```

Unity keeps its import settings (`.meta`), so regenerating over the same path keeps the material references.

## Parameters

| Option | Default | Meaning |
|---|---|---|
| `out` (positional) | none | Output PNG path. |
| `--seed` | `7` | Rune layout seed. |
| `--core` | `f3e8ff` | Hex color of the bright stroke core. |
| `--glow` | `9333ea` | Hex color of the halo around the strokes. |
| `--size` | `512` | Square output size in pixels. |

## Output

`game/Assets/_Project/VFX/RuneCircles/Textures/T_RuneCircle_Teleport.png` (512x512, seed 7, core `#f3e8ff`, glow `#9333ea`), used by `M_RuneCircle_Teleport`.

License: self-made, see `docs/ASSET_CREDITS.md`.
