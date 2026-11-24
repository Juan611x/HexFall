"""Generate a glowing rune circle texture (RGB, black background) for the rune circle shader.

Deterministic: the same seed and colors always give the same image.
Usage: python generate_rune_circle.py OUT.png [--seed N] [--core RRGGBB] [--glow RRGGBB] [--size 512]
Shipped texture: game/Assets/_Project/VFX/RuneCircles/Textures/T_RuneCircle_Teleport.png (--seed 7 --core f3e8ff --glow 9333ea --size 512).
"""
import argparse
import math
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

SS = 4  # supersampling factor


def hex_rgb(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def polar(c, r, a):
    return (c + r * math.cos(a), c + r * math.sin(a))


def ring(draw, c, r, w):
    draw.ellipse([c - r, c - r, c + r, c + r], outline=255, width=max(1, int(w)))


def rune(draw, cx, cy, h, angle, rng, w):
    """A procedural glyph: 2-4 strokes between points of a 2x3 grid, rotated to face the centre."""
    pts = [(x, y) for y in (-1.0, 0.0, 1.0) for x in (-0.5, 0.5)]
    strokes = rng.randint(2, 4)
    used = set()
    ca, sa = math.cos(angle), math.sin(angle)

    def tr(p):
        x, y = p[0] * h * 0.5, p[1] * h * 0.5
        return (cx + x * ca - y * sa, cy + x * sa + y * ca)

    # a spine keeps every rune readable as one symbol
    spine = rng.choice([(0, 4), (1, 5)])
    draw.line([tr(pts[spine[0]]), tr(pts[spine[1]])], fill=255, width=w)
    for _ in range(strokes):
        a, b = rng.sample(range(6), 2)
        if (a, b) in used or (b, a) in used:
            continue
        used.add((a, b))
        draw.line([tr(pts[a]), tr(pts[b])], fill=255, width=w)
    if rng.random() < 0.35:
        p = tr(pts[rng.randrange(6)])
        r = h * 0.12
        draw.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], outline=255, width=w)


def rune_band(draw, c, r, count, h, rng, w):
    for i in range(count):
        a = 2 * math.pi * i / count
        x, y = polar(c, r, a)
        rune(draw, x, y, h, a + math.pi / 2, rng, w)


def star(draw, c, r, n, step, rot, w):
    pts = [polar(c, r, rot + 2 * math.pi * i / n) for i in range(n)]
    for i in range(n):
        draw.line([pts[i], pts[(i + step) % n]], fill=255, width=w)
    return pts


def polygon(draw, c, r, n, rot, w):
    pts = [polar(c, r, rot + 2 * math.pi * i / n) for i in range(n)]
    draw.line(pts + [pts[0]], fill=255, width=w, joint="curve")


def line_mask(size, seed):
    big = size * SS
    c = big / 2
    s = big / 512.0  # layout is authored for 512 px
    img = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(img)
    rng = random.Random(seed)
    thick, mid, thin = int(5 * s), int(3 * s), int(2 * s)

    # outer frame: double ring with a rune band between
    ring(d, c, 246 * s, thick)
    ring(d, c, 238 * s, thin)
    rune_band(d, c, 222 * s, 40, 18 * s, rng, thin)
    ring(d, c, 205 * s, mid)

    # heptagram with orbs on its points
    rot = -math.pi / 2
    tips = star(d, c, 205 * s, 7, 3, rot, mid)
    polygon(d, c, 205 * s, 7, rot, thin)
    for p in tips:
        r = 16 * s
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=0, outline=255, width=mid)
        rune(d, p[0], p[1], 14 * s, rng.random() * math.pi, rng, thin)

    # inner band: clear the star lines first so the inner rings read cleanly
    r = 118 * s
    d.ellipse([c - r, c - r, c + r, c + r], fill=0)
    ring(d, c, 118 * s, mid)
    rune_band(d, c, 104 * s, 20, 14 * s, rng, thin)
    ring(d, c, 90 * s, thin)

    # core: hexagon (HexFall) inside a hexagram, centre dot
    star(d, c, 76 * s, 6, 2, rot, thin)
    polygon(d, c, 44 * s, 6, rot, mid)
    ring(d, c, 20 * s, thin)
    r = 7 * s
    d.ellipse([c - r, c - r, c + r, c + r], fill=255)

    # short ticks on the outer ring
    for i in range(72):
        a = 2 * math.pi * i / 72
        d.line([polar(c, 246 * s, a), polar(c, (252 if i % 6 == 0 else 249) * s, a)], fill=255, width=thin)
    return img.resize((size, size), Image.LANCZOS)


def colorize(mask, core, glow):
    """Core lines tinted toward white over a wide coloured bloom, on black."""
    wide = mask.filter(ImageFilter.GaussianBlur(mask.width / 40))
    near = mask.filter(ImageFilter.GaussianBlur(mask.width / 200))
    bloom = ImageChops.add(wide.point(lambda v: min(255, v * 2.2)), near.point(lambda v: min(255, v * 1.4)))
    out = Image.new("RGB", mask.size, (0, 0, 0))
    glow_layer = Image.merge("RGB", [bloom.point(lambda v, k=k: v * k // 255) for k in glow])
    core_layer = Image.merge("RGB", [mask.point(lambda v, k=k: v * k // 255) for k in core])
    out = ImageChops.add(out, glow_layer)
    return ImageChops.screen(out, core_layer)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--core", default="f3e8ff")
    ap.add_argument("--glow", default="9333ea")
    ap.add_argument("--size", type=int, default=512)
    args = ap.parse_args()
    mask = line_mask(args.size, args.seed)
    colorize(mask, hex_rgb(args.core), hex_rgb(args.glow)).save(args.out, optimize=True)


if __name__ == "__main__":
    main()
