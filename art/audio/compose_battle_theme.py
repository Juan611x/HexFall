"""HexFall battle theme: a seamless "magical battlefield" loop in D Phrygian dominant.

Pure Python (no dependencies) so anyone can regenerate it:

    python art/audio/compose_battle_theme.py <output.wav>

Same family as the menu theme (compose_menu_theme.py): chip-style voices written into a circular
buffer of exactly one loop, so tails and echoes wrap and the loop point is seamless. Magic is carried
by high strings and bells, battle by war drums, a galloping bass and low brass.

Form (16 bars each, 126 BPM, ~2 min):
    A  tension        drums, galloping bass, string ostinato
    B  theme          + solo violin melody, bell arpeggios
    C  magical breath drums down, celesta, sustained strings
    B' climax         melody an octave up, low brass, drum fills; resolves on D back into A
"""

import math
import random
import struct
import sys
import wave

RATE = 22050
BPM = 126
BEAT = 60.0 / BPM
BARS = 64
LOOP_SAMPLES = int(round(BARS * 4 * BEAT * RATE))

# D Phrygian dominant: D Eb F# G A Bb C.
CHORDS = {
    "D": (62, 66, 69),
    "Eb": (63, 67, 70),
    "Cm": (60, 63, 67),
    "Gm": (62, 67, 70),
    "Bb": (62, 65, 70),
}
ROOTS = {"D": 38, "Eb": 39, "Cm": 36, "Gm": 43, "Bb": 34}

SECTIONS = [  # (name, chords: one per 2 bars)
    ("A", ["D", "Eb", "D", "Cm", "D", "Eb", "Cm", "D"]),
    ("B", ["Gm", "Eb", "Cm", "D", "Gm", "Eb", "Cm", "D"]),
    ("C", ["Bb", "Cm", "Gm", "D", "Bb", "Cm", "Eb", "D"]),
    ("B2", ["Gm", "Eb", "Cm", "D", "Gm", "Eb", "Cm", "D"]),
]

# Solo violin, one 8-bar phrase over Gm Eb Cm D: (beat in phrase, length in beats, midi).
MELODY = [
    (0, 1.5, 62), (1.5, 0.5, 67), (2, 2, 69), (4, 1, 70), (5, 1, 69), (6, 2, 67),
    (8, 1.5, 67), (9.5, 0.5, 70), (10, 2, 74), (12, 1, 75), (13, 1, 74), (14, 2, 70),
    (16, 1, 72), (17, 1, 70), (18, 1, 69), (19, 1, 67), (20, 2, 66), (22, 1, 63), (23, 1, 66),
]
ENDING_OPEN = [(24, 2, 69), (26, 1, 70), (27, 1, 72), (28, 4, 74)]   # first phrase rises
ENDING_CLOSED = [(24, 3, 69), (27, 1, 66), (28, 4, 62)]              # second phrase lands on D


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


def saw_voice(midi, seconds, gain, attack, release, cutoff, detune_cents=0.0, vibrato=0.0, decay=0.0):
    """Sawtooth (two detuned copies when detune_cents > 0) through a one-pole low-pass."""
    length = int(seconds * RATE)
    base = hz(midi)
    spread = 2 ** (detune_cents / 1200.0)
    p1 = p2 = 0.0
    lp = 0.0
    out = []
    for i in range(length):
        t = i / RATE
        vib = 0.0
        if vibrato and t > 0.15:
            vib = vibrato * math.sin(2 * math.pi * 5.8 * t) * min(1.0, (t - 0.15) / 0.25)
        f = base * 2 ** (vib / 12.0)
        p1 = (p1 + f * spread / RATE) % 1.0
        s = 2.0 * p1 - 1.0
        if detune_cents:
            p2 = (p2 + f / spread / RATE) % 1.0
            s = 0.5 * (s + 2.0 * p2 - 1.0)
        lp += cutoff * (s - lp)
        amp = math.exp(-decay * t) if decay else 1.0
        out.append(lp * gain * amp * envelope(i, length, attack, release))
    return out


def square_voice(midi, seconds, gain, attack, release, cutoff):
    length = int(seconds * RATE)
    f = hz(midi)
    phase = 0.0
    lp = 0.0
    out = []
    for i in range(length):
        phase = (phase + f / RATE) % 1.0
        lp += cutoff * ((1.0 if phase < 0.5 else -1.0) - lp)
        out.append(lp * gain * envelope(i, length, attack, release))
    return out


def bell(midi, seconds, gain):
    length = int(seconds * RATE)
    f = hz(midi)
    out = []
    for i in range(length):
        t = i / RATE
        s = math.sin(2 * math.pi * f * t) + 0.35 * math.sin(2 * math.pi * f * 2.76 * t) * math.exp(-6 * t)
        out.append(s * gain * math.exp(-3.4 * t) * min(1.0, i / 40.0))
    return out


def war_drum(gain, rng):
    """Taiko-like hit: sine falling from 110 Hz to 42 Hz plus a short noise skin."""
    length = int(0.45 * RATE)
    phase = 0.0
    out = []
    for i in range(length):
        t = i / RATE
        f = 42.0 + 68.0 * math.exp(-t * 18.0)
        phase += 2 * math.pi * f / RATE
        body = math.sin(phase) * math.exp(-t * 7.0)
        skin = rng.uniform(-1.0, 1.0) * math.exp(-t * 60.0) * 0.5
        out.append((body + skin) * gain)
    return out


def snare(gain, rng):
    length = int(0.12 * RATE)
    prev = 0.0
    out = []
    for i in range(length):
        t = i / RATE
        n = rng.uniform(-1.0, 1.0)
        hp = n - 0.6 * prev
        prev = n
        tone = math.sin(2 * math.pi * 185.0 * t) * math.exp(-t * 40.0) * 0.4
        out.append((hp * math.exp(-t * 28.0) + tone) * gain)
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
    rng = random.Random(11)
    drums, bass, strings, lead, magic, brass = Bus(), Bus(), Bus(), Bus(), Bus(), Bus()
    eighth = BEAT / 2

    for section_index, (section, chords) in enumerate(SECTIONS):
        section_bar = section_index * 16
        battle = section != "C"
        climax = section == "B2"

        for chord_index, name in enumerate(chords):
            bar0 = section_bar + chord_index * 2
            tones = CHORDS[name]
            root = ROOTS[name]

            for bar in (bar0, bar0 + 1):
                beat0 = bar * 4
                last_bar_of_phrase = (bar - section_bar) % 4 == 3

                # War drums: 1 and 3; pickup into the next bar every second bar; only beat 1 in C.
                if battle:
                    for b, g in ((0, 0.55), (2, 0.45)):
                        drums.add(at(beat0 + b), war_drum(g, rng))
                    if climax:
                        drums.add(at(beat0 + 2.5), war_drum(0.3, rng))
                    if bar % 2 == 1:
                        drums.add(at(beat0 + 3.5), war_drum(0.3, rng))
                        drums.add(at(beat0 + 3.75), war_drum(0.35, rng))
                else:
                    drums.add(at(beat0), war_drum(0.32, rng))

                # March snare: quiet sixteenths with accents on 2 and 4; a crescendo fill closes each phrase.
                if battle:
                    for step in range(16):
                        if last_bar_of_phrase and step >= 8:
                            g = 0.06 + 0.02 * (step - 8)
                        elif step in (4, 12):
                            g = 0.16
                        elif step % 2 == 1:
                            continue
                        else:
                            g = 0.04
                        drums.add(at(beat0 + step * 0.25), snare(g, rng))

                # Galloping bass (eighth + two sixteenths per beat); long notes in C.
                if battle:
                    for b in range(4):
                        for off, length in ((0, 0.45), (0.5, 0.22), (0.75, 0.22)):
                            bass.add(at(beat0 + b + off), saw_voice(root, length * BEAT, 0.20, 0.004, 0.03, 0.18))
                elif bar == bar0:
                    bass.add(at(beat0), saw_voice(root, 8 * BEAT, 0.16, 0.08, 0.6, 0.12))

            # Strings: sixteenth spiccato ostinato in battle sections, a sustained pad in C.
            if battle:
                pattern = [0, 0, 2, 0, 1, 0, 2, 1]
                notes = [t + 12 for t in tones]
                for step in range(32):
                    midi = notes[pattern[step % 8]]
                    strings.add(at(bar0 * 4 + step * 0.25),
                                saw_voice(midi, 0.22 * BEAT, 0.07, 0.004, 0.03, 0.35, detune_cents=7.0, decay=9.0))
            else:
                for t in tones:
                    strings.add(at(bar0 * 4), saw_voice(t, 8 * BEAT, 0.045, 0.35, 0.6, 0.12, detune_cents=8.0, vibrato=0.12))

            # Magic: bells on chord changes in A; eighth-note bell arpeggios in B; celesta sixteenths in C.
            if section == "A":
                magic.add(at(bar0 * 4), bell(tones[0] + 24, 2.2, 0.09))
            elif section == "C":
                arp = [tones[0] + 24, tones[1] + 24, tones[2] + 24, tones[0] + 36]
                for step in range(32):
                    magic.add(at(bar0 * 4 + step * 0.25), bell(arp[[0, 1, 2, 3, 2, 1][step % 6]], 0.7, 0.05))
            else:
                arp = [tones[0] + 24, tones[2] + 24, tones[1] + 24, tones[2] + 24]
                for step in range(16):
                    magic.add(at(bar0 * 4 + step * 0.5), bell(arp[step % 4], 1.0, 0.045))

            # Low brass under the climax, one held chord tone per chord.
            if climax:
                brass.add(at(bar0 * 4), square_voice(root + 12, 7.5 * BEAT, 0.06, 0.12, 0.5, 0.08))
                brass.add(at(bar0 * 4), square_voice(root + 19, 7.5 * BEAT, 0.04, 0.12, 0.5, 0.08))

        # Sparkle: a fast descending bell run announcing B, C and B'.
        if section != "A":
            for k, midi in enumerate([98, 94, 93, 90, 86, 82, 81, 78]):
                magic.add(at(section_bar * 4 + k * 0.125), bell(midi, 0.6, 0.06))

        # Solo violin: two 8-bar phrases in B (open, then closed), an octave up in B'.
        if section in ("B", "B2"):
            shift = 12 if climax else 0
            for phrase, ending in enumerate((ENDING_OPEN, ENDING_CLOSED)):
                start = (section_bar + phrase * 8) * 4
                for beat, length, midi in MELODY + ending:
                    lead.add(at(start + beat),
                             saw_voice(midi + shift, length * BEAT, 0.11, 0.04, 0.1, 0.25, detune_cents=4.0, vibrato=0.3))

    wet = [l + m for l, m in zip(lead.buf, magic.buf)]
    wet = echo(wet, 0.75, 0.32, 0.3)
    mix = [w + d + b + s + br for w, d, b, s, br in zip(wet, drums.buf, bass.buf, strings.buf, brass.buf)]
    shaped = [math.tanh(1.3 * s) for s in mix]
    peak = max(abs(s) for s in shaped) or 1.0
    return [s * 0.89 / peak for s in shaped]


def write_wav(path, samples, rate=RATE):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples))


def rms(samples):
    return math.sqrt(sum(s * s for s in samples) / max(len(samples), 1))


def main(argv):
    out = argv[1]
    theme = compose()
    assert len(theme) == LOOP_SAMPLES
    write_wav(out, theme)
    print("THEME", out, "seconds %.2f" % (len(theme) / RATE), "rms %.3f" % rms(theme),
          "peak %.3f" % max(abs(s) for s in theme))


if __name__ == "__main__":
    main(sys.argv)
