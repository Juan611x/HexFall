"""Imports the Stark Crafts PSX night sky and writes a darker copy for the Sandbox skybox.

The sky has no formal licence (see docs/ASSET_CREDITS.md), so it is a local import: both files land
in game/Assets/ThirdParty/StarkCrafts/, which is gitignored. Download PSX_Nightsky_HDRI.zip from
https://starkcrafts.itch.io/psx-forest-asset-collection-by-starkcrafts and run, from the repository root:

    blender -b --factory-startup --python art/environment/build_night_sky.py -- <path/to/PSX_Nightsky_HDRI.zip>

Darkening: the sky is scaled and partly desaturated, while the moon (bright pixels) keeps most of its
brightness so the moonlight still reads. Tune SKY_GAIN, DESATURATION and MOON_GAIN below.

Outputs:
    game/Assets/ThirdParty/StarkCrafts/NightSky/PSX_NIGHTSKY.hdr      (original, unmodified)
    game/Assets/ThirdParty/StarkCrafts/NightSky/T_NightSky_Dark.hdr   (darkened)
"""

import os
import sys
import tempfile
import zipfile

import bpy
import numpy as np

SKY_GAIN = 0.5
DESATURATION = 0.25
MOON_GAIN = 0.9
MOON_FROM = 0.4   # luminance where the moon mask starts
MOON_TO = 1.0     # luminance where a pixel counts fully as moon

SOURCE_NAME = "PSX_NIGHTSKY.hdr"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "game", "Assets", "ThirdParty", "StarkCrafts", "NightSky")
LUMA = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)


def extract_source(zip_path):
    with zipfile.ZipFile(zip_path) as archive:
        member = next(n for n in archive.namelist() if n.endswith(SOURCE_NAME))
        data = archive.read(member)
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, SOURCE_NAME)
    with open(path, "wb") as f:
        f.write(data)
    return path


def darken(rgb):
    lum = rgb @ LUMA
    grey = lum[..., None]
    sky = (rgb * (1.0 - DESATURATION) + grey * DESATURATION) * SKY_GAIN
    moon = np.clip((lum - MOON_FROM) / (MOON_TO - MOON_FROM), 0.0, 1.0)[..., None]
    return sky * (1.0 - moon) + rgb * MOON_GAIN * moon


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    source = extract_source(args[0])

    img = bpy.data.images.load(source)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    px[..., :3] = darken(px[..., :3])

    out = bpy.data.images.new("T_NightSky_Dark", w, h, float_buffer=True)
    out.pixels[:] = px.ravel()
    out.filepath_raw = os.path.join(OUT_DIR, "T_NightSky_Dark.hdr")
    out.file_format = "HDR"
    out.save()
    print("NIGHT_SKY_OK", w, h, float((px[..., :3] @ LUMA).mean()))


main()
