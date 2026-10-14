#!/usr/bin/env python3
"""Synthesises AVATapper's sound effects and tunes into ../src/AVATapper/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVATapper", "Sounds")
RNG = random.Random(1983)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def seq(bpm, notes, kind="pulse", gain=0.55, release=0.6):
    beat = 60.0 / bpm
    out = []
    for note, beats in notes:
        d = beats * beat
        out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=kind, release=release, gain=gain))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))
    # At the tap and along the bar.
    write("Pour.wav", mix(tone(0.42, 1400, 900, "noise", attack=0.03, release=0.2, gain=0.6), tone(0.42, 300, 600, "sine", release=0.2, gain=0.3)), 0.3)
    write("Slide.wav", tone(0.35, 700, 300, "noise", attack=0.01, release=0.6), 0.25)
    write("Catch.wav", mix(tone(0.06, 1800, 1200, "tri", release=0.8), tone(0.05, 500, 200, "noise", release=1.0, gain=0.4)), 0.3)
    write("Clink.wav", mix(tone(0.25, 3100, kind="tri", release=1.2), tone(0.25, 4200, kind="tri", release=1.4, gain=0.5)), 0.35)
    write("Smash.wav", mix(tone(0.5, 5200, 2200, "noise", release=0.8), *[tone(0.4, f, kind="tri", release=1.3, gain=0.3) for f in [2800, 3500, 4300, 5000]]), 0.45)
    write("Door.wav", cat(tone(0.08, 180, 140, "square", release=0.8, gain=0.6), tone(0.12, 900, 400, "noise", release=0.9, gain=0.3)), 0.25)
    write("Tip.wav", cat(*[tone(0.05, f, kind="tri", release=0.6) for f in [2600, 3000, 2800]]), 0.3)
    write("Jukebox.wav", cat(*[tone(0.1, hz(n), kind="pulse", release=0.4) for n in ["C5", "E5", "G5", "C6", "G5", "C6", "E6"]]), 0.4)
    write("Grab.wav", cat(tone(0.15, 300, 180, "saw", release=0.5), tone(0.35, 200, 80, "square", release=0.6)), 0.4)
    write("Oops.wav", cat(tone(0.2, hz("E4"), kind="square", release=0.4), tone(0.2, hz("D#4"), kind="square", release=0.4), tone(0.5, hz("D4"), kind="square", release=0.6, vibrato=0.02)), 0.35)
    write("Step.wav", tone(0.05, 220, 160, "square", release=0.9), 0.2)
    # The bonus round.
    write("Shake.wav", cat(*[tone(0.05, 2000, 1000, "noise", release=0.6) for _ in range(8)]), 0.3)
    write("Swap.wav", tone(0.15, 600, 900, "tri", release=0.6), 0.25)
    write("Fizz.wav", tone(1.0, 6000, 2500, "noise", attack=0.02, release=0.5), 0.4)
    write("Correct.wav", cat(*[tone(0.09, hz(n), kind="pulse", release=0.3) for n in ["C5", "E5", "G5", "C6", "E6", "G6"]]), 0.4)
    write("ExtraLife.wav", cat(*[tone(0.08, hz(n), kind="pulse", release=0.3) for n in ["E6", "B5", "E6", "B5", "E6", "G6", "B6"]]), 0.4)
    write("LevelClear.wav", tune(150, [("G4", 0.5), ("C5", 0.5), ("E5", 0.5), ("G5", 1), ("E5", 0.5), ("G5", 2)], [("C3", 2), ("G2", 1), ("C3", 2)], kind="pulse"), 0.45)

    # Serving: a jaunty honky-tonk rag in F over a stride bass (new). Loops.
    stride = []
    for root, chord in [("F2", "A3"), ("F2", "A3"), ("C3", "G3"), ("C3", "G3"), ("D3", "A3"), ("G2", "B3"), ("C3", "E3"), ("F2", "A3")] * 2:
        stride += [(root, 0.5), (chord, 0.5), (root, 0.5), (chord, 0.5)]
    lead = [("C5", 0.5), ("D5", 0.5), ("F5", 1), ("A5", 0.5), ("G5", 0.5), ("F5", 1), ("E5", 0.5), ("G5", 0.5), ("C6", 1), ("Bb5", 0.5), ("A5", 0.5), ("G5", 1),
            ("A5", 0.5), ("F5", 0.5), ("D5", 1), ("B4", 0.5), ("D5", 0.5), ("G5", 1), ("E5", 0.5), ("G5", 0.5), ("C5", 1), ("F5", 2),
            ("A5", 0.5), ("C6", 0.5), ("A5", 1), ("F5", 0.5), ("A5", 0.5), ("G5", 1), ("E5", 0.5), ("C5", 0.5), ("E5", 1), ("G5", 1), ("-", 1),
            ("D5", 0.5), ("F5", 0.5), ("A5", 1), ("G5", 0.5), ("B4", 0.5), ("D5", 1), ("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("E5", 0.5), ("F5", 2)]
    bass = seq(160, stride, kind="tri", gain=0.45, release=0.4)
    top = seq(160, lead, kind="pulse", gain=0.26, release=0.5)
    write("Music.wav", mix(bass, top), 0.42)

    write("TitleTune.wav", tune(132,
                                [("F4", 0.5), ("A4", 0.5), ("C5", 1), ("D5", 0.5), ("C5", 0.5), ("A4", 1), ("Bb4", 0.5), ("A4", 0.5), ("G4", 1), ("C5", 2),
                                 ("F4", 0.5), ("A4", 0.5), ("C5", 1), ("F5", 0.5), ("E5", 0.5), ("D5", 1), ("C5", 0.5), ("G4", 0.5), ("A4", 0.5), ("G4", 0.5), ("F4", 2)],
                                [("F2", 2), ("D2", 2), ("Bb1", 2), ("C2", 2), ("F2", 2), ("Bb1", 2), ("C2", 2), ("F2", 2)], kind="pulse"), 0.45)
    write("StartTune.wav", tune(160, [("C4", 0.5), ("F4", 0.5), ("A4", 0.5), ("C5", 1.5), ("A4", 0.5), ("C5", 2)], [("F2", 2), ("C2", 1), ("F2", 2)], kind="pulse"), 0.45)
    write("GameOver.wav", tune(84, [("C5", 1), ("Bb4", 1), ("A4", 1), ("G4", 1), ("F4", 3)], [("F3", 2), ("D3", 2), ("C3", 1), ("F2", 3)], kind="tri"), 0.45)


if __name__ == "__main__":
    main()
