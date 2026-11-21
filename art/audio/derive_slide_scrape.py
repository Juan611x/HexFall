"""Derive the slide scrape sound from the CC0 "Scrapes" pack by AntumDeluge (OpenGameArt).

Pure Python (no dependencies):

    python -I art/audio/derive_slide_scrape.py <scrape-1.wav> <SlideScrape.wav>

Reads a 16-bit mono PCM WAV and writes a 16-bit mono WAV at the same sample rate with:
    - a -8 dB gain (the source peaks at -6 dBFS, so the result peaks near -14 dBFS),
    - a 10 ms linear fade-in and a 150 ms linear fade-out to avoid clicks and a hard tail.
It prints the resulting peak and RMS in dBFS.
"""

import math
import struct
import sys
import wave

GAIN_DB = -8.0
FADE_IN_SECONDS = 0.010
FADE_OUT_SECONDS = 0.150


def read_samples(path):
    with wave.open(path, "rb") as wav:
        if wav.getnchannels() != 1 or wav.getsampwidth() != 2:
            raise SystemExit("Expected a 16-bit mono WAV.")
        rate = wav.getframerate()
        raw = wav.readframes(wav.getnframes())
    return rate, list(struct.unpack("<%dh" % (len(raw) // 2), raw))


def process(samples, rate):
    gain = 10 ** (GAIN_DB / 20.0)
    count = len(samples)
    fade_in = min(count, int(FADE_IN_SECONDS * rate))
    fade_out = min(count, int(FADE_OUT_SECONDS * rate))
    out = []
    for i, sample in enumerate(samples):
        value = sample * gain
        if i < fade_in:
            value *= i / fade_in
        remaining = count - 1 - i
        if remaining < fade_out:
            value *= remaining / fade_out
        out.append(max(-32768, min(32767, int(round(value)))))
    return out


def write_samples(path, rate, samples):
    with wave.open(path, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(struct.pack("<%dh" % len(samples), *samples))


def level_db(value):
    return 20 * math.log10(value / 32768.0) if value > 0 else float("-inf")


def main(argv):
    if len(argv) != 3:
        raise SystemExit(__doc__)
    rate, samples = read_samples(argv[1])
    out = process(samples, rate)
    write_samples(argv[2], rate, out)
    peak = max(abs(s) for s in out)
    rms = math.sqrt(sum(s * s for s in out) / len(out))
    print("%d samples at %d Hz, peak %.1f dBFS, rms %.1f dBFS" % (len(out), rate, level_db(peak), level_db(rms)))


if __name__ == "__main__":
    main(sys.argv)
