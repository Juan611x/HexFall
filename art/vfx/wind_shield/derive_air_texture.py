"""Derives the air wisp sprite from the CC0 photo "Snow blowing under lamp 1" (W.carter, Wikimedia Commons).

Deterministic (no randomness). Reads the photo stored under source/ and writes one tiny mask:

    T_AirWisp.png    16x8 grayscale: the sprite of the wind wisp particles

Steps: crop (2:1) -> grayscale -> flatten the lamp (remove the large-scale brightness with a blurred
copy, clip the highlights) -> autocontrast -> streakiest strip -> faded ends -> LANCZOS downscale ->
posterise (8 levels). The shield sphere does not use this photo: its air comes from the Kenney twirls (derive_air_twirls.py).

Usage (repository root):

    python -I art/vfx/wind_shield/derive_air_texture.py \
        art/vfx/wind_shield/source/Snow_blowing_under_lamp_1_1280px.jpg \
        game/Assets/_Project/VFX/WindShield/Textures
"""

import argparse
import os

from PIL import Image, ImageFilter, ImageOps

CROP_CENTRE = (0.3, 0.5)  # fraction of the photo (x, y)
CROP_WIDTH = 0.6          # fraction of the photo width; height follows from the 2:1 ratio
HIGHLIGHT_PERCENTILE = 0.995
LEVELS = 16
WISP_SIZE = (16, 8)


def crop_2to1(img):
    w, h = img.size
    cw = round(w * CROP_WIDTH)
    ch = cw // 2
    cx, cy = round(w * CROP_CENTRE[0]), round(h * CROP_CENTRE[1])
    left = min(max(cx - cw // 2, 0), w - cw)
    top = min(max(cy - ch // 2, 0), h - ch)
    return img.crop((left, top, left + cw, top + ch))


def flatten_lamp(gray):
    """Keeps the streaks, drops the lamp: divide by a blurred copy (local contrast), clip highlights."""
    w, h = gray.size
    low = gray.filter(ImageFilter.GaussianBlur(radius=w / 24))
    px, lp = gray.load(), low.load()
    ratio = [[px[x, y] / (lp[x, y] + 6.0) for x in range(w)] for y in range(h)]
    flat = sorted(v for row in ratio for v in row)
    hi = flat[int(len(flat) * HIGHLIGHT_PERCENTILE)]
    lo = flat[int(len(flat) * 0.03)]
    out = Image.new("L", gray.size)
    op = out.load()
    for y in range(h):
        for x in range(w):
            op[x, y] = round(255 * max(0.0, min(1.0, (ratio[y][x] - lo) / (hi - lo))))
    return out


def posterise(gray, levels=LEVELS):
    step = 255 / (levels - 1)
    return gray.point(lambda v: round(round(v / step) * step))


def derive(photo_path, out_dir):
    img = Image.open(photo_path).convert("RGB")
    gray = ImageOps.autocontrast(flatten_lamp(crop_2to1(img).convert("L")), cutoff=1)
    # Wisp sprite: the streakiest horizontal strip of the flattened photo, faded at the ends so the
    # stretched particle has soft-but-chunky tips, then cut to a tiny point-filtered sprite.
    w, h = gray.size
    best, best_score = 0, -1.0
    strip_h = h // 8
    px = gray.load()
    for top in range(0, h - strip_h, max(strip_h // 2, 1)):
        score = sum(px[x, y] for y in range(top, top + strip_h) for x in range(0, w, 4))
        if score > best_score:
            best, best_score = top, score
    strip = gray.crop((w // 4, best, w // 4 + w // 2, best + strip_h)).resize(WISP_SIZE, Image.LANCZOS)
    strip = ImageOps.autocontrast(strip)
    sp = strip.load()
    sw, sh = strip.size
    for x in range(sw):
        ex = min(x, sw - 1 - x) / (sw * 0.3)
        for y in range(sh):
            ey = min(y, sh - 1 - y) / (sh * 0.35)
            k = min(min(ex, 1.0), min(ey, 1.0) + 0.0)
            sp[x, y] = round(sp[x, y] * max(0.0, min(1.0, k)))
    posterise(strip, 8).save(os.path.join(out_dir, "T_AirWisp.png"), optimize=True)


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("photo")
    ap.add_argument("out_dir")
    args = ap.parse_args()
    derive(args.photo, args.out_dir)
