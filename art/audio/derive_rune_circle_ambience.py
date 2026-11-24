"""Derive the rune circle ambient loops from the CC0 "30 CC0 SFX loops" pack by rubberduck
(https://opengameart.org/content/30-cc0-sfx-loops).

The original loops are far quieter than the effect clips (ambient_01 RMS -31 dBFS, weird_03 -25 dBFS,
effects about -15 dBFS), so the ambience was inaudible. This script levels them. Deterministic:

    python -m venv .venv && .venv/Scripts/pip install -r art/audio/requirements.txt
    .venv/Scripts/python -I art/audio/derive_rune_circle_ambience.py

It reads the ORIGINAL OGGs (kept untouched) from game/Assets/ThirdParty/OpenGameArt/Rubberduck30Loops/ and
writes mono 48 kHz 16-bit WAVs next to them:
    ambient_01.ogg -> RuneAmbienceLaunch.wav
    weird_03.ogg   -> RuneAmbienceTeleport.wav
Processing: channels averaged to mono, then ONE constant gain so the RMS is -20 dBFS, capped so the
peak stays at or below -1 dBFS. No fades, trims or resampling (the sources are already 48 kHz; the script
refuses any other rate), so the loop point is preserved. It prints RMS, peak and the wrap discontinuity.
"""

import os
import sys

import numpy as np
import soundfile as sf

RATE = 48000
TARGET_RMS_DB = -20.0
MAX_PEAK_DB = -1.0

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
LOOPS = os.path.join(ROOT, "game", "Assets", "ThirdParty", "OpenGameArt", "Rubberduck30Loops")
CLIPS = {"ambient_01.ogg": "RuneAmbienceLaunch.wav", "weird_03.ogg": "RuneAmbienceTeleport.wav"}


def db(x):
    return 20.0 * np.log10(max(float(x), 1e-12))


def derive(src, dst):
    data, rate = sf.read(src, dtype="float64", always_2d=True)
    if rate != RATE:
        raise SystemExit(f"{src}: expected {RATE} Hz, got {rate} Hz (resampling would risk the loop point)")
    mono = data.mean(axis=1)
    rms = float(np.sqrt(np.mean(mono ** 2)))
    peak = float(np.max(np.abs(mono)))
    gain = min(10 ** (TARGET_RMS_DB / 20.0) / rms, 10 ** (MAX_PEAK_DB / 20.0) / peak)
    out = mono * gain
    sf.write(dst, out, RATE, subtype="PCM_16")
    back, _ = sf.read(dst, dtype="float64")
    steps = np.abs(np.diff(back))
    wrap = abs(back[0] - back[-1])
    print(f"{os.path.basename(dst)}: {len(back) / RATE:.3f} s, gain {db(gain):+.2f} dB, "
          f"RMS {db(np.sqrt(np.mean(back ** 2))):.2f} dBFS, peak {db(np.max(np.abs(back))):.2f} dBFS "
          f"(was RMS {db(rms):.2f}, peak {db(peak):.2f}); wrap step {wrap:.5f} vs max inner step "
          f"{steps.max():.5f}, mean inner step {steps.mean():.5f}")


def main():
    for src, dst in CLIPS.items():
        derive(os.path.join(LOOPS, src), os.path.join(LOOPS, dst))


if __name__ == "__main__":
    sys.exit(main())
