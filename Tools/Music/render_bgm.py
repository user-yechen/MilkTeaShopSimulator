# -*- coding: utf-8 -*-
"""把 Tools/Music/specs/*.json 直接合成 WAV（占位音色），供 Unity 播放。

用法：
  python Tools/Music/render_bgm.py --spec Tools/Music/specs/day_loop.json --out Assets/Resources/Audio/BGM/day_loop.wav
"""
import argparse
import json
import math
import os
import wave

import numpy as np

SR = 44100
TPB = 480


def note_hz(n):
    return 440.0 * (2 ** ((n - 69) / 12.0))


def env(n, attack, decay, sr=SR):
    a = max(1, int(attack * sr))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a)
    d = np.exp(-np.linspace(0, 1, n) * decay)
    return e * d


def add(buf, start, sig):
    i = int(start * SR)
    end = min(len(buf), i + len(sig))
    if i >= len(buf):
        return
    buf[i:end] += sig[: end - i]


def piano(freq, dur):
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = np.zeros(n)
    for h, amp in ((1, 1.0), (2, 0.35), (3, 0.18), (4, 0.08)):
        sig += amp * np.sin(2 * math.pi * freq * h * t)
    return 0.22 * sig * env(n, 0.004, 3.2)


def guitar(freq, dur):
    n = int(dur * SR)
    period = max(2, int(SR / freq))
    buf = np.random.uniform(-1, 1, period)
    out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % period]
        buf[i % period] = 0.5 * (buf[i % period] + buf[(i + 1) % period]) * 0.995
    return 0.20 * out * env(n, 0.002, 2.2)


def pad(freq, dur):
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = np.zeros(n)
    for det in (-0.004, 0.0, 0.004):
        for h in (1, 2, 3):
            sig += (1.0 / h) * np.sin(2 * math.pi * freq * h * (1 + det) * t)
    return 0.06 * sig * env(n, 0.35, 0.6)


def kick(dur=0.18):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = 120 * np.exp(-t * 26) + 45
    return 0.55 * np.sin(2 * math.pi * np.cumsum(f) / SR) * np.exp(-t * 12)


def snare(dur=0.14):
    n = int(dur * SR)
    t = np.arange(n) / SR
    noise = np.random.uniform(-1, 1, n) * np.exp(-t * 30)
    tone = 0.3 * np.sin(2 * math.pi * 190 * t) * np.exp(-t * 24)
    return 0.35 * (noise + tone)


def hat(dur=0.05):
    n = int(dur * SR)
    t = np.arange(n) / SR
    noise = np.random.uniform(-1, 1, n)
    # 一阶高通
    hp = np.zeros(n)
    for i in range(1, n):
        hp[i] = 0.9 * (hp[i - 1] + noise[i] - noise[i - 1])
    return 0.12 * hp * np.exp(-t * 80)


CHORD = {"maj": [0, 4, 7], "min": [0, 3, 7], "7": [0, 4, 7, 10],
         "maj7": [0, 4, 7, 11], "min7": [0, 3, 7, 10]}
ROOT = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def parse_chord(sym):
    q = "maj"
    for suf in ("m7", "maj7", "7", "m"):
        if sym.endswith(suf):
            q = {"m7": "min7", "maj7": "maj7", "7": "7", "m": "min"}[suf]
            sym = sym[: -len(suf)]
            break
    return ROOT.get(sym, 0), q


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--spec", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    spec = json.load(open(args.spec, encoding="utf-8"))
    bpm = spec.get("bpm", 96)
    bars = spec.get("bars", 16)
    beats = spec.get("beatsPerBar", 4)
    chords = spec.get("chords", ["C", "Am", "F", "G"])
    inst = spec.get("instruments", {})

    beat = 60.0 / bpm
    total = bars * beats * beat + 1.2
    buf = np.zeros(int(total * SR))

    for bar in range(bars):
        root, qual = parse_chord(chords[bar % len(chords)])
        base = 60 + root  # C4 起
        notes = [base + i for i in CHORD.get(qual, CHORD["maj"])]
        t0 = bar * beats * beat

        if "piano" in inst:
            for i in range(beats * 2):
                n = notes[i % len(notes)] + (12 if i % 4 == 3 else 0)
                add(buf, t0 + i * beat / 2, piano(note_hz(n), beat * 1.6))
        if "guitar" in inst:
            for i in range(beats * 2):
                n = notes[i % len(notes)] - 12
                add(buf, t0 + i * beat / 2, guitar(note_hz(n), beat * 1.2))
        if "pad" in inst:
            for n in notes:
                add(buf, t0, pad(note_hz(n), beats * beat + 0.3))
        if "drums" in inst:
            for b in range(beats):
                if b % 2 == 0:
                    add(buf, t0 + b * beat, kick())
                else:
                    add(buf, t0 + b * beat, snare())
                add(buf, t0 + b * beat, hat())
                add(buf, t0 + (b + 0.5) * beat, hat())

    peak = np.max(np.abs(buf)) or 1.0
    buf = np.clip(buf / peak * 0.85, -1, 1)
    pcm = (buf * 32767).astype(np.int16)

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with wave.open(args.out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print("WROTE", args.out, round(len(pcm) / SR, 1), "s")


if __name__ == "__main__":
    main()
