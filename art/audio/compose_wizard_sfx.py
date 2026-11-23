"""HexFall wizard sound effects: short chip-style one-shots for the action machine.

Pure Python (no dependencies) so anyone can regenerate them:

    python art/audio/compose_wizard_sfx.py [output_dir]

Writes 16-bit mono 22.05 kHz WAVs (default: game/Assets/_Project/Audio/Sfx/), deterministic (fixed
seed), normalized to about -3 dBFS peak with short fades to avoid clicks:
    WizardCast.wav    rising arcane chirp with a sparkle on top
    BoltImpact.wav    short noisy burst over a low thump
    ShieldRaise.wav   shimmering swell
    ParryDeflect.wav  bright metallic ping
    ParryPerfect.wav  brighter two-tone ping with a little sparkle
    Snap.wav          short bright finger snap (the wizard's body sound on a parry press)
    Huff.wav          breathy exhale (the wizard's body sound on an attack press)
"""

import math
import os
import random
import struct
import sys
import wave

RATE = 22050
PEAK = 10 ** (-3 / 20.0)  # about -3 dBFS
FADE = 0.004


def n_samples(seconds):
    return int(round(seconds * RATE))


def square(phase, duty=0.5):
    return 1.0 if (phase % 1.0) < duty else -1.0


def sine(phase):
    return math.sin(2 * math.pi * phase)


def chirp(f0, f1, seconds, gain, wave_fn=sine, curve=1.0):
    """Tone gliding from f0 to f1; curve > 1 stays low longer, < 1 rises early."""
    n = n_samples(seconds)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / max(n - 1, 1)
        f = f0 + (f1 - f0) * (t ** curve)
        phase += f / RATE
        out.append(wave_fn(phase) * gain)
    return out


def ping(freq, seconds, gain, decay, partials=((1.0, 1.0), (2.76, 0.5), (5.4, 0.25))):
    """Inharmonic bell-like partials with an exponential decay."""
    n = n_samples(seconds)
    out = [0.0] * n
    for ratio, level in partials:
        w = 2 * math.pi * freq * ratio / RATE
        for i in range(n):
            out[i] += math.sin(w * i) * level * math.exp(-decay * ratio * i / RATE)
    return [s * gain for s in out]


def noise(seconds, gain, rng, decay):
    n = n_samples(seconds)
    return [rng.uniform(-1, 1) * gain * math.exp(-decay * i / RATE) for i in range(n)]


def thump(f0, f1, seconds, gain, decay):
    n = n_samples(seconds)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / RATE
        phase += (f1 + (f0 - f1) * math.exp(-30 * t)) / RATE
        out.append(sine(phase) * gain * math.exp(-decay * t))
    return out


def lowpass(samples, alpha):
    out = []
    y = 0.0
    for s in samples:
        y += alpha * (s - y)
        out.append(y)
    return out


def shaped(samples, attack, release):
    n = len(samples)
    a = max(1, n_samples(attack))
    r = max(1, n_samples(release))
    out = []
    for i, s in enumerate(samples):
        g = min(1.0, i / a, (n - 1 - i) / r)
        out.append(s * max(0.0, g))
    return out


def mix(length, *layers):
    """layers: (start_seconds, samples)."""
    buf = [0.0] * n_samples(length)
    for start, samples in layers:
        offset = n_samples(start)
        for i, s in enumerate(samples):
            if offset + i < len(buf):
                buf[offset + i] += s
    return buf


def finish(samples):
    samples = shaped(samples, FADE, FADE * 2)
    peak = max(abs(s) for s in samples) or 1.0
    return [s * PEAK / peak for s in samples]


def wizard_cast():
    body = shaped(chirp(330, 1320, 0.30, 0.6, curve=1.6), 0.01, 0.08)
    edge = shaped(chirp(660, 2640, 0.30, 0.25, square, curve=1.6), 0.01, 0.08)
    sparkle = [
        (0.18 + 0.045 * k, shaped(ping(1760 * (1 + 0.25 * k), 0.12, 0.25, 25), 0.002, 0.05))
        for k in range(3)
    ]
    return finish(mix(0.35, (0.0, body), (0.0, edge), *sparkle))


def bolt_impact(rng):
    burst = shaped(lowpass(noise(0.22, 0.9, rng, 16), 0.55), 0.001, 0.05)
    body = thump(150, 45, 0.3, 0.9, 9)
    crack = shaped(chirp(900, 200, 0.06, 0.4, square), 0.001, 0.03)
    return finish(mix(0.3, (0.0, burst), (0.0, body), (0.0, crack)))


def shield_raise():
    n = n_samples(0.4)
    out = [0.0] * n
    for k, f in enumerate((440, 554, 659, 880)):
        for i in range(n):
            t = i / n
            vibrato = 1 + 0.01 * math.sin(2 * math.pi * (6 + k) * i / RATE)
            swell = math.sin(math.pi * t) ** 1.5
            out[i] += sine(f * vibrato * i / RATE) * swell * (0.35 - 0.05 * k)
    shimmer = chirp(900, 2200, 0.4, 0.12, sine, curve=1.2)
    return finish([a + b * math.sin(math.pi * i / n) for i, (a, b) in enumerate(zip(out, shimmer))])


def parry_deflect():
    ring = ping(1480, 0.35, 0.7, 11)
    tick = shaped(noise(0.02, 0.5, random.Random(7), 120), 0.0005, 0.01)
    return finish(mix(0.35, (0.0, ring), (0.0, tick)))


def parry_perfect():
    low = ping(1320, 0.3, 0.6, 12)
    high = ping(1976, 0.4, 0.6, 10)
    sparkle = [
        (0.1 + 0.05 * k, shaped(ping(3000 + 400 * k, 0.1, 0.18, 30, ((1.0, 1.0),)), 0.002, 0.04))
        for k in range(3)
    ]
    return finish(mix(0.45, (0.0, low), (0.09, high), *sparkle))


def highpass(samples, alpha):
    out = []
    y = 0.0
    for s in samples:
        y += alpha * (s - y)
        out.append(s - y)
    return out


def snap(rng):
    """Finger snap: a very short bright click, a quick band-passed pop and a tiny low knock."""
    click = shaped(highpass(noise(0.03, 1.0, rng, 180), 0.35), 0.0003, 0.012)
    pop = shaped(highpass(lowpass(noise(0.09, 0.8, rng, 55), 0.6), 0.12), 0.0005, 0.03)
    knock = shaped(thump(420, 160, 0.05, 0.35, 70), 0.0005, 0.02)
    return finish(mix(0.12, (0.0, click), (0.0, pop), (0.001, knock)))


def huff(rng):
    """Breathy exhale: noise through a lowpass whose cutoff falls (a downward formant feel)."""
    n = n_samples(0.3)
    out = []
    y1 = 0.0
    y2 = 0.0
    for i in range(n):
        t = i / (n - 1)
        alpha = 0.55 - 0.40 * t  # darker as the breath runs out
        raw = rng.uniform(-1, 1)
        y1 += alpha * (raw - y1)
        y2 += alpha * (y1 - y2)
        attack = min(1.0, t / 0.08)
        env = attack * math.exp(-5.0 * max(0.0, t - 0.08))
        flutter = 1 + 0.12 * math.sin(2 * math.pi * 23 * i / RATE)
        out.append(y2 * env * flutter)
    return finish(highpass(out, 0.03))


def write_wav(path, samples):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples))


def main(argv):
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    out_dir = argv[1] if len(argv) > 1 else os.path.join(root, "game", "Assets", "_Project", "Audio", "Sfx")
    os.makedirs(out_dir, exist_ok=True)
    rng = random.Random(1)
    clips = {
        "WizardCast": wizard_cast(),
        "BoltImpact": bolt_impact(rng),
        "ShieldRaise": shield_raise(),
        "ParryDeflect": parry_deflect(),
        "ParryPerfect": parry_perfect(),
        "Snap": snap(random.Random(11)),
        "Huff": huff(random.Random(13)),
    }
    for name, samples in clips.items():
        write_wav(os.path.join(out_dir, name + ".wav"), samples)
        print("%s.wav  %.2f s  peak %.3f" % (name, len(samples) / RATE, max(abs(s) for s in samples)))


if __name__ == "__main__":
    main(sys.argv)
