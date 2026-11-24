"""Generate a horizontally tileable wind-streak mask (grayscale, black background).

Thin, curved, tapered streaks run mostly horizontally; they wrap in X so the texture
can be mapped around a sphere's vertical axis. Deterministic: same seed, same image.
Usage: python generate_wind_streaks.py OUT.png [--seed N] [--width 512] [--height 256] [--count 70]
Shipped texture: game/Assets/_Project/VFX/WindShield/Textures/T_WindStreaks.png (defaults, --seed 11).
"""
import argparse
import math
import random

from PIL import Image, ImageDraw, ImageFilter

SS = 3  # supersampling factor


def draw_streak(draw, w, h, rng):
    length = rng.uniform(0.25, 0.75) * w
    x0 = rng.uniform(0, w)
    y0 = rng.uniform(0.08, 0.92) * h
    thick = rng.uniform(1.0, 3.0) * SS
    bright = rng.uniform(0.35, 1.0)
    amp = rng.uniform(1, 5) * SS
    freq = rng.uniform(0.3, 0.9)
    phase = rng.uniform(0, math.tau)
    steps = max(24, int(length / SS))
    for i in range(steps):
        t = i / (steps - 1)
        taper = math.sin(math.pi * t) ** 0.8  # soft, pointed ends
        x = x0 + t * length
        y = y0 + amp * math.sin(phase + math.tau * freq * t)
        r = max(0.3, thick * taper) / 2
        v = int(255 * bright * taper)
        for dx in (-w, 0, w):  # wrap in X
            draw.ellipse([x + dx - r, y - r, x + dx + r, y + r], fill=v)


def build(width, height, seed, count):
    rng = random.Random(seed)
    w, h = width * SS, height * SS
    img = Image.new("L", (w, h), 0)
    layer_draw = ImageDraw.Draw(img)
    for _ in range(count):
        draw_streak(layer_draw, w, h, rng)
    # blur wraps nothing by itself: tile 3x, blur, crop the centre so the seam stays continuous
    tiled = Image.new("L", (w * 3, h), 0)
    for k in range(3):
        tiled.paste(img, (k * w, 0))
    tiled = tiled.filter(ImageFilter.GaussianBlur(0.9 * SS))
    img = tiled.crop((w, 0, 2 * w, h)).resize((width, height), Image.LANCZOS)
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--seed", type=int, default=11)
    ap.add_argument("--width", type=int, default=512)
    ap.add_argument("--height", type=int, default=256)
    ap.add_argument("--count", type=int, default=44)
    args = ap.parse_args()
    build(args.width, args.height, args.seed, args.count).save(args.out, optimize=True)


if __name__ == "__main__":
    main()
