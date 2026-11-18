"""Render hexfall_logo.svg to the PNG and ICO files the game ships.

The SVG is the source of truth. This renderer understands only what that file uses:
flat <polygon> fills and <path> fills with a "translate scale translate" transform.
Shapes are drawn 4x oversized and downsampled for anti-aliasing; the output is deterministic.

Usage (from the repository root, in a venv with requirements.txt installed):
    python art/logo/export_logo.py
"""
import re
from pathlib import Path

from fontTools.pens.freetypePen import FreeTypePen
from fontTools.svgLib.path import parse_path
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
SVG = HERE / "hexfall_logo.svg"
VIEWBOX = 300.0
SUPERSAMPLE = 4

# Output path -> square size in pixels.
PNG_OUTPUTS = {
    HERE / "export" / "hexfall_logo_1024.png": 1024,
    REPO / "game/Assets/_Project/UI/Textures/T_LogoEmblem.png": 256,
    REPO / "game/Assets/_Project/Art/Icons/T_AppIcon.png": 1024,
}
ICO_OUTPUT = REPO / "game/Assets/WebGLTemplates/HexFall/TemplateData/favicon.ico"
ICO_SIZES = [16, 32, 48, 64]

TRANSFORM_RE = re.compile(
    r"translate\(([-\d.]+),([-\d.]+)\)\s*scale\(([-\d.]+),([-\d.]+)\)\s*translate\(([-\d.]+),([-\d.]+)\)"
)


def hex_rgba(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def parse_svg(text):
    """Return drawing ops in document order: ("polygon", fill, points) or ("path", fill, transform, d)."""
    ops = []
    for match in re.finditer(r"<(polygon|path)\b([^>]*)/?>", text):
        tag, attrs = match.group(1), dict(re.findall(r'([\w-]+)="([^"]*)"', match.group(2)))
        fill = hex_rgba(attrs["fill"])
        if tag == "polygon":
            nums = [float(n) for n in re.split(r"[ ,]+", attrs["points"].strip())]
            ops.append(("polygon", fill, list(zip(nums[0::2], nums[1::2]))))
        else:
            ops.append(("path", fill, tuple(float(n) for n in TRANSFORM_RE.fullmatch(attrs["transform"]).groups()), attrs["d"]))
    return ops


def render(ops, size):
    big = size * SUPERSAMPLE
    k = big / VIEWBOX
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    for op in ops:
        if op[0] == "polygon":
            _, fill, points = op
            draw.polygon([(x * k, y * k) for x, y in points], fill=fill)
            continue
        _, fill, (a, b, sx, sy, c, e), d = op
        # SVG maps (x, y) to (a + sx*(x + c), b + sy*(y + e)) with y down; FreeType images are y up.
        pen = FreeTypePen(None)
        parse_path(d, pen)
        tx = k * (a + sx * c)
        ty = big - k * (b + sy * e)
        mask = pen.image(width=big, height=big, transform=(k * sx, 0, 0, -k * sy, tx, ty)).getchannel("A")
        img.paste(fill, (0, 0), mask)
    return img.resize((size, size), Image.LANCZOS)


def main():
    ops = parse_svg(SVG.read_text(encoding="utf-8"))
    for path, size in PNG_OUTPUTS.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        render(ops, size).save(path, optimize=True)
        print("wrote", path.relative_to(REPO))
    ICO_OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    render(ops, 256).save(ICO_OUTPUT, sizes=[(s, s) for s in ICO_SIZES])
    print("wrote", ICO_OUTPUT.relative_to(REPO))


if __name__ == "__main__":
    main()
