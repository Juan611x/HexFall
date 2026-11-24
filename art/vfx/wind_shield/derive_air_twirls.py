"""Derives the low-res wind textures of the air shield from the CC0 Kenney Particle Pack.

Run headless inside Blender (its bundled Python has numpy and the image API, so no extra install):

    blender -b --factory-startup --python art/vfx/wind_shield/derive_air_twirls.py -- \
        game/Assets/ThirdParty/Kenney/ParticlePack game/Assets/_Project/VFX/WindShield/Textures

Deterministic (no randomness). The originals are white on an opaque black background (no alpha), so each one is:
box-filtered down to SIZE x SIZE, luminance -> alpha (normalised so the brightest stroke is opaque, a mild gamma
lifts the faint outer arms), RGB forced to white. The gradient stays smooth (8-bit alpha); it is NOT posterised,
because hard alpha levels made the sphere read as flat blocks. The Unity importer keeps Point filtering, no mipmaps.

    twirl_03.png -> T_AirTwirl_A.png   main sphere layer
    twirl_02.png -> T_AirTwirl_B.png   second, counter-rotating layer
    trace_04.png -> T_AirTrace.png     reserved for the wind currents and motes (not used by the sphere)
"""

import os
import sys

import bpy
import numpy as np

SIZE = 128
GAMMA = 0.8
JOBS = (("twirl_03.png", "T_AirTwirl_A.png"), ("twirl_02.png", "T_AirTwirl_B.png"), ("trace_04.png", "T_AirTrace.png"))


def read_luminance(path):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "Non-Color"  # raw values, no sRGB decoding
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, img.channels if img.channels else 4)
    bpy.data.images.remove(img)
    return px[..., :3].mean(axis=2)


def box_downscale(a, size):
    h, w = a.shape
    assert h % size == 0 and w % size == 0, "source must be a multiple of the target size"
    return a.reshape(size, h // size, size, w // size).mean(axis=(1, 3))


def to_rgba(lum):
    alpha = np.clip(lum / lum.max(), 0.0, 1.0) ** GAMMA
    rgba = np.ones((*alpha.shape, 4), dtype=np.float32)
    rgba[..., 3] = alpha
    return rgba


def save_png(rgba, path):
    h, w = rgba.shape[:2]
    img = bpy.data.images.new("derived", w, h, alpha=True)
    img.colorspace_settings.name = "Non-Color"
    img.alpha_mode = "STRAIGHT"
    img.pixels = rgba.ravel().tolist()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    src, out = (os.path.abspath(p) for p in args[:2])
    for original, derived in JOBS:
        lum = box_downscale(read_luminance(os.path.join(src, original)), SIZE)
        save_png(to_rgba(lum), os.path.join(out, derived))
        print("derived", derived, lum.shape)


main()
