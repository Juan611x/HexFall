# HexFall logo

Chosen logo: **version 7, "Fused HF"**. Cinzel Decorative Black `H` and `F` share one centred, stretched stem inside a green hexagon with a violet ring.

| File | Role |
|---|---|
| `hexfall_logo.svg` | Source of truth (300×300 viewBox). Edit this file, then re-export. |
| `build_fused_hf.py` | Stage 1: fuses the real Cinzel glyph outlines on a shared stem and stretches it. Outputs a raw SVG path in font units. |
| `export_logo.py` | Stage 2: renders the SVG to every shipped file (deterministic). |
| `export/hexfall_logo_1024.png` | Reference render for docs and mockups. |

## Rebuild

```sh
python -m venv .venv && .venv/Scripts/pip install -r art/logo/requirements.txt
.venv/Scripts/python art/logo/export_logo.py
```

`export_logo.py` writes:

- `game/Assets/_Project/UI/Textures/T_LogoEmblem.png` (256, Title screen emblem)
- `game/Assets/_Project/Art/Icons/T_AppIcon.png` (1024, Player Settings default icon)
- `game/Assets/WebGLTemplates/HexFall/TemplateData/favicon.ico` (16/32/48/64)

## How the path was made

1. `python art/logo/build_fused_hf.py game/Assets/_Project/UI/Fonts/CinzelDecorative-Black.ttf 577 fused.png fused_path.txt`
   shifts `F` by +474 font units so its stem lands on the right stem of `H`, then moves the shared stem and its foot serif down 577 units.
2. Hand edit (kept in the SVG, not scripted): the top-left serif of `F` was removed so its edge follows the `H` swash curve (segment `V610Q581 656.4 596.9 700`).
3. Placement: `translate(150,136) scale(0.12,-0.12) translate(-683.5,-335.5)` centres the shared stem on the hex axis; the shadow is the same path offset by (+6, +6).

Colors: ring `#6d28d9`, face `#0f9c88`, bevel `#5eead4`, letters `#f5f3ff`, shadow `#07060c`.

License: derivative of Cinzel Decorative (SIL OFL 1.1), see `docs/ASSET_CREDITS.md`.
