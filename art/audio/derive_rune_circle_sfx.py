"""Derive the rune circle and pickup sounds from the CC0 "150 Combat Sound Effects" compilation
by untamedpixels (https://opengameart.org/node/195932), the compilation already used for the spell casts.

Pure Python (standard library only), deterministic:

    python -I art/audio/derive_rune_circle_sfx.py <extracted-compilation-dir> <output-dir>

For every clip below it reads a 16-bit PCM WAV (mono or stereo, any sample rate) and writes a 16-bit
mono 48 kHz WAV with:
    - stereo averaged to mono and linear-interpolation resampling to 48 kHz,
    - a trim to the wanted length, a short fade-in and a linear fade-out (no clicks, no hard tail),
    - peak normalisation to -0.5 dBFS, like the other clips of the compilation already in the project
      (the in-game level is set per component and by the player's effects volume).
It prints each clip's duration and peak.

Source ids (see LICENSES.txt next to the output): s01 CoMiGo's Bleeps and Bloops, s07 lentikula Druid
Spell Impacts, s08 lentikula Healing Spell Impacts. All CC0 1.0.
"""

import math
import os
import struct
import sys
import wave

RATE = 48000
PEAK = 10 ** (-0.5 / 20.0)  # -0.5 dBFS

# output name: (source path inside the compilation, kept seconds from the start, fade-in s, fade-out s)
CLIPS = {
    # Teleport: the whole 0.77 s burst, only the tail fades.
    "RuneTeleport.wav": ("03_explosions_destruction/GravityBomb_01_s01.wav", 0.77, 0.002, 0.08),
    # Launch: keep the attack and the body of the 3.57 s wind, drop the long tail.
    "RuneLaunch.wav": ("04_magic_spells/Wind Spell Impact 1_s07.wav", 1.9, 0.005, 0.45),
    # Health potion and shield refill: the sparkle is over after about a second, drop the silence.
    "PickupHeal.wav": ("04_magic_spells/Healing Spell Impact 10_s08.wav", 1.3, 0.003, 0.3),
    "PickupShield.wav": ("04_magic_spells/Healing Spell Impact 9_s08.wav", 1.4, 0.003, 0.3),
}


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


def resample(samples, source_rate, target_rate):
    if source_rate == target_rate:
        return list(samples)
    count = int(len(samples) * target_rate / source_rate)
    out = []
    for i in range(count):
        position = i * source_rate / target_rate
        index = int(position)
        fraction = position - index
        a = samples[index]
        b = samples[index + 1] if index + 1 < len(samples) else a
        out.append(a + (b - a) * fraction)
    return out


def shape(samples, seconds, fade_in, fade_out):
    samples = samples[: int(seconds * RATE)]
    count = len(samples)
    fade_in_count = min(count, int(fade_in * RATE))
    fade_out_count = min(count, int(fade_out * RATE))
    out = []
    for i, value in enumerate(samples):
        if i < fade_in_count:
            value *= i / fade_in_count
        remaining = count - 1 - i
        if remaining < fade_out_count:
            value *= remaining / fade_out_count
        out.append(value)
    return out


def normalise(samples):
    peak = max(abs(v) for v in samples)
    gain = PEAK * 32768.0 / peak if peak > 0 else 1.0
    return [max(-32768, min(32767, int(round(v * gain)))) for v in samples]


def write_mono(path, samples):
    with wave.open(path, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(struct.pack("<%dh" % len(samples), *samples))


def main(argv):
    if len(argv) != 3:
        raise SystemExit(__doc__)
    source_dir, out_dir = argv[1], argv[2]
    os.makedirs(out_dir, exist_ok=True)
    for name, (relative, seconds, fade_in, fade_out) in CLIPS.items():
        rate, samples = read_mono(os.path.join(source_dir, relative))
        out = normalise(shape(resample(samples, rate, RATE), seconds, fade_in, fade_out))
        write_mono(os.path.join(out_dir, name), out)
        peak = max(abs(v) for v in out)
        print("%s: %.3f s, peak %.2f dBFS" % (name, len(out) / RATE, 20 * math.log10(peak / 32768.0)))


if __name__ == "__main__":
    main(sys.argv)
