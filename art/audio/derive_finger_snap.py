"""Derive the parry-press finger snap from a real recording.

The source is Freesound sound 177496 by Snapper4298 ("Finger Snaps" soundfont pack), kept as the original
stereo WAV in game/Assets/ThirdParty/FingerSnaps/ (see LICENSE.txt there). Pure Python (standard library
only), deterministic:

    python -I art/audio/derive_finger_snap.py [source.wav] [output.wav]

Defaults: the source above and game/Assets/_Project/Audio/Sfx/Snap.wav (overwritten in place). Steps:
    - the two channels averaged to mono (no resampling: it stays 44.1 kHz so the snap keeps its high end),
    - the silence before the transient dropped, leaving a 3 ms pre-roll before the loudest sample,
    - a fixed 0.22 s kept from there (the natural ring decays below -50 dB by then) with a linear
      fade-out over the last 60 ms,
    - peak normalisation to -3 dBFS like the synthesized wizard sounds (compose_wizard_sfx.py).
"""

import math
import os
import struct
import sys
import wave

PEAK = 10 ** (-3 / 20.0)  # about -3 dBFS
PRE_ROLL = 0.003  # seconds kept before the loudest sample
KEEP = 0.22  # seconds kept in total
FADE_OUT = 0.06  # seconds, linear


def read_mono(path):
    with wave.open(path, "rb") as wav:
        if wav.getsampwidth() != 2:
            raise SystemExit("Expected 16-bit PCM: " + path)
        channels = wav.getnchannels()
        rate = wav.getframerate()
        raw = wav.readframes(wav.getnframes())
    values = struct.unpack("<%dh" % (len(raw) // 2), raw)
    if channels == 1:
        return rate, [float(v) for v in values]
    return rate, [sum(values[i:i + channels]) / channels for i in range(0, len(values) - channels + 1, channels)]


def derive(samples, rate):
    loudest = max(range(len(samples)), key=lambda i: abs(samples[i]))
    start = max(0, loudest - int(PRE_ROLL * rate))
    clip = samples[start:start + int(KEEP * rate)]
    count = len(clip)
    fade = min(count, int(FADE_OUT * rate))
    for i in range(count):
        remaining = count - 1 - i
        if remaining < fade:
            clip[i] *= remaining / fade
    peak = max(abs(v) for v in clip)
    gain = PEAK * 32768.0 / peak
    return [max(-32768, min(32767, int(round(v * gain)))) for v in clip]


def main(argv):
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    source = argv[1] if len(argv) > 1 else os.path.join(root, "game", "Assets", "ThirdParty", "FingerSnaps", "177496_snapper4298.wav")
    target = argv[2] if len(argv) > 2 else os.path.join(root, "game", "Assets", "_Project", "Audio", "Sfx", "Snap.wav")
    rate, samples = read_mono(source)
    out = derive(samples, rate)
    with wave.open(target, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(struct.pack("<%dh" % len(out), *out))
    peak = max(abs(v) for v in out)
    print("Snap.wav: %.3f s, %d Hz mono, peak %.2f dBFS" % (len(out) / rate, rate, 20 * math.log10(peak / 32768.0)))


if __name__ == "__main__":
    main(sys.argv)
