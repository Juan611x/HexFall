"""HexFall main-menu theme: a seamless retro loop in D Phrygian dominant.

Pure Python (no dependencies) so anyone can regenerate it:

    python art/audio/compose_menu_theme.py <output.wav> [--preview-with <ambient.wav> <preview.wav>]

Voices are synthesized like sfxr/console sound chips (triangle bass, pulse
arpeggio, square lead with vibrato, sine bells, noise hats). Every voice
writes into a circular buffer of exactly one loop, so note tails and echoes
wrap around and the loop point is seamless.
"""

import math
import random
import struct
import sys
import wave

RATE = 22050
BPM = 84
BEAT = 60.0 / BPM
BARS = 32
LOOP_SAMPLES = int(round(BARS * 4 * BEAT * RATE))

# Chords per 2 bars. Section A (bars 1-16) and B (bars 17-32).
# D major / Eb major / C minor / G minor all live in D Phrygian dominant (D Eb F# G A Bb C).
CHORDS = {
    "D": (62, 66, 69),
    "Eb": (63, 67, 70),
    "Cm": (60, 63, 67),
    "Gm": (67, 70, 74),
}
ROOTS = {"D": 38, "Eb": 39, "Cm": 36, "Gm": 43}
PROGRESSION = ["D", "Eb", "D", "Cm"] * 2 + ["Gm", "Eb", "Cm", "D"] * 2  # one chord per 2 bars

# Lead melody for section B, 8 bars (beat offset within the phrase, length in beats, midi).
MELODY = [
    (0, 2, 74), (2, 1, 70), (3, 1, 72),
    (4, 1, 74), (5, 1, 75), (6, 2, 74),
    (8, 2, 75), (10, 1, 79), (11, 1, 78),
    (12, 3, 75), (15, 1, 74),
    (16, 2, 72), (18, 1, 75), (19, 1, 74),
    (20, 1, 72), (21, 1, 70), (22, 2, 69),
    (24, 1, 69), (25, 1, 70), (26, 1, 69), (27, 1, 66),
    (28, 4, 62),
]
MELODY_ENDING = [  # replaces the last two bars on the repeat
    (24, 1, 69), (25, 1, 70), (26, 1, 72), (27, 1, 75),
    (28, 4, 74),
]


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12.0)


def at(beats):
    return int(round(beats * BEAT * RATE))


class Bus:
    def __init__(self):
        self.buf = [0.0] * LOOP_SAMPLES

    def add(self, start, samples):
        n = LOOP_SAMPLES
        buf = self.buf
        for i, s in enumerate(samples):
            buf[(start + i) % n] += s


def envelope(i, length, attack, release):
    a = int(attack * RATE)
    r = int(release * RATE)
    if i < a:
        return i / max(a, 1)
    if i > length - r:
        return max(0.0, (length - i) / max(r, 1))
    return 1.0


def triangle_note(midi, beats, gain):
    length = at(beats)
    f = hz(midi)
    out = []
    phase = 0.0
    for i in range(length):
        phase = (phase + f / RATE) % 1.0
        tri = 4.0 * abs(phase - 0.5) - 1.0
        out.append(tri * gain * envelope(i, length, 0.005, 0.04))
    return out


def pulse_note(midi, beats, gain, duty, decay):
    length = at(beats)
    f = hz(midi)
    out = []
    phase = 0.0
    lp = 0.0
    for i in range(length):
        phase = (phase + f / RATE) % 1.0
        sq = 1.0 if phase < duty else -1.0
        lp += 0.35 * (sq - lp)
        amp = math.exp(-decay * i / RATE)
        out.append(lp * gain * amp * envelope(i, length, 0.003, 0.02))
    return out


def lead_note(midi, beats, gain):
    length = at(beats)
    base = hz(midi)
    out = []
    phase = 0.0
    lp = 0.0
    for i in range(length):
        t = i / RATE
        vib = 0.0 if t < 0.18 else 0.25 * math.sin(2 * math.pi * 5.5 * t) * min(1.0, (t - 0.18) / 0.3)
        f = base * 2 ** (vib / 12.0)
        phase = (phase + f / RATE) % 1.0
        sq = 1.0 if phase < 0.5 else -1.0
        lp += 0.22 * (sq - lp)
        out.append(lp * gain * envelope(i, length, 0.02, 0.12))
    return out


def bell_note(midi, seconds, gain):
    length = int(seconds * RATE)
    f = hz(midi)
    out = []
    for i in range(length):
        t = i / RATE
        amp = math.exp(-3.2 * t)
        s = math.sin(2 * math.pi * f * t) + 0.35 * math.sin(2 * math.pi * f * 2.76 * t) * math.exp(-6 * t)
        out.append(s * gain * amp * min(1.0, i / 40.0))
    return out


def hat(gain, rng):
    length = int(0.05 * RATE)
    out = []
    prev = 0.0
    for i in range(length):
        n = rng.uniform(-1.0, 1.0)
        hp = n - prev
        prev = n
        out.append(hp * gain * math.exp(-60.0 * i / RATE))
    return out


def echo(buf, delay_beats, feedback, mix):
    d = at(delay_beats)
    n = len(buf)
    wet = [0.0] * n
    for _ in range(2):  # two passes so the circular feedback settles across the loop seam
        for i in range(n):
            wet[i] = buf[(i - d) % n] + feedback * wet[(i - d) % n]
    return [buf[i] + mix * wet[i] for i in range(n)]


def compose():
    rng = random.Random(7)
    bass, arp, lead, bells, hats = Bus(), Bus(), Bus(), Bus(), Bus()

    for chord_index, name in enumerate(PROGRESSION):
        bar0 = chord_index * 2
        tones = CHORDS[name]
        root = ROOTS[name]

        # Arpeggio: sixteenths over the chord plus the octave.
        pattern = [0, 2, 1, 3, 2, 1, 0, 2]
        notes = list(tones) + [tones[0] + 12]
        for step in range(32):
            midi = notes[pattern[step % 8]]
            arp.add(at(bar0 * 4 + step * 0.25), pulse_note(midi, 0.25, 0.09, 0.25, 6.0))

        # Bass enters at bar 5; eighth-note pattern with rests.
        if bar0 >= 4:
            for bar in (bar0, bar0 + 1):
                for step, offset in enumerate([0, None, 0, 7, 0, None, 12, 7]):
                    if offset is None:
                        continue
                    bass.add(at(bar * 4 + step * 0.5), triangle_note(root + offset, 0.45, 0.24))

        # Arcane bells on each chord change, high register.
        bells.add(at(bar0 * 4), bell_note(tones[0] + 24, 2.4, 0.11))
        bells.add(at(bar0 * 4 + 3), bell_note(tones[2] + 12, 1.6, 0.06))

        # Soft hats on the off-beats in section B.
        if bar0 >= 16:
            for eighth in range(16):
                if eighth % 2 == 1:
                    hats.add(at(bar0 * 4 + eighth * 0.5), hat(0.05, rng))

    # Lead melody: section B, two 8-bar phrases.
    for phrase, start_bar in enumerate((16, 24)):
        notes = MELODY if phrase == 0 else MELODY[:-5] + MELODY_ENDING
        for beat, length, midi in notes:
            lead.add(at(start_bar * 4 + beat), lead_note(midi, length, 0.13))

    fx = [a + l + b for a, l, b in zip(arp.buf, lead.buf, bells.buf)]
    fx = echo(fx, 0.75, 0.35, 0.32)
    mix = [x + y + z for x, y, z in zip(fx, bass.buf, hats.buf)]

    shaped = [math.tanh(1.2 * s) for s in mix]
    peak = max(abs(s) for s in shaped) or 1.0
    return [s * 0.89 / peak for s in shaped]


def write_wav(path, samples, rate=RATE):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples))


def read_wav(path):
    with wave.open(path, "rb") as w:
        if w.getframerate() != RATE or w.getnchannels() != 1 or w.getsampwidth() != 2:
            raise ValueError("preview ambient must be mono 16-bit at %d Hz" % RATE)
        n = w.getnframes()
        return [v / 32768.0 for v in struct.unpack("<%dh" % n, w.readframes(n))]


def rms(samples):
    return math.sqrt(sum(s * s for s in samples) / max(len(samples), 1))


def main(argv):
    out = argv[1]
    theme = compose()
    write_wav(out, theme)
    print("THEME", out, "seconds %.2f" % (len(theme) / RATE), "rms %.3f" % rms(theme))
    if "--preview-with" in argv:
        i = argv.index("--preview-with")
        ambient, preview = argv[i + 1], argv[i + 2]
        amb = read_wav(ambient)
        length = min(len(amb), RATE * 75)
        theme_gain, ambient_gain = 0.75, 0.55
        mixed = [theme[k % len(theme)] * theme_gain + amb[k] * ambient_gain for k in range(length)]
        peak = max(abs(s) for s in mixed) or 1.0
        mixed = [s * 0.89 / peak if peak > 0.89 else s for s in mixed]
        write_wav(preview, mixed)
        print("PREVIEW", preview, "ambient rms %.3f" % rms(amb[:length]))


if __name__ == "__main__":
    main(sys.argv)
