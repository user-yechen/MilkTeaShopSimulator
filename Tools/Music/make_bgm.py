# -*- coding: utf-8 -*-
"""按 JSON spec 生成 MIDI（无第三方依赖，纯标准库）。

用法：
  python Tools/Music/make_bgm.py --spec Tools/Music/specs/day_loop.json --out ArtSource/Audio/BGM/day_loop.mid

音色按 General MIDI：
  0   Acoustic Grand Piano（钢琴）
  89  Warm Pad / 90 Polysynth（合成器铺底）
  24  Nylon Guitar / 25 Steel Guitar（木吉他）
  通道 10（索引 9）为架子鼓
"""
import argparse
import json
import os
import struct

TPQ = 480  # ticks per quarter note

NOTE_BASE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3,
             "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8,
             "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def note_number(name, octave):
    return 12 * (octave + 1) + NOTE_BASE[name]


def parse_chord(sym):
    """C / Am / F / G7 / Dm7 -> (root, quality)"""
    quality = "maj"
    if sym.endswith("m7"):
        quality, sym = "min7", sym[:-2]
    elif sym.endswith("maj7"):
        quality, sym = "maj7", sym[:-4]
    elif sym.endswith("7"):
        quality, sym = "7", sym[:-1]
    elif sym.endswith("m"):
        quality, sym = "min", sym[:-1]
    root = sym
    return root, quality


def chord_notes(root, quality, octave):
    r = note_number(root, octave)
    if quality == "maj":
        return [r, r + 4, r + 7]
    if quality == "min":
        return [r, r + 3, r + 7]
    if quality == "7":
        return [r, r + 4, r + 7, r + 10]
    if quality == "maj7":
        return [r, r + 4, r + 7, r + 11]
    if quality == "min7":
        return [r, r + 3, r + 7, r + 10]
    return [r, r + 4, r + 7]


def vlq(value):
    if value < 0:
        raise ValueError("negative delta: %d" % value)
    out = [value & 0x7F]
    value >>= 7
    while value:
        out.append((value & 0x7F) | 0x80)
        value >>= 7
    return bytes(reversed(out))


def track_chunk(events):
    """events: list of (delta_ticks, bytes)"""
    data = bytearray()
    for delta, payload in events:
        data += vlq(delta)
        data += payload
    data += vlq(0) + b"\xff\x2f\x00"
    return b"MTrk" + struct.pack(">I", len(data)) + bytes(data)


def note_on(ch, note, vel):
    return bytes([0x90 | ch, note, vel])


def note_off(ch, note):
    return bytes([0x80 | ch, note, 0x40])


def program(ch, prog):
    return bytes([0xC0 | ch, prog])


def meta_tempo(bpm):
    us = int(60_000_000 / bpm)
    return b"\xff\x51\x03" + struct.pack(">I", us)[1:]


def meta_name(text):
    b = text.encode("utf-8")
    return b"\xff\x03" + vlq(len(b)) + b


def build(spec):
    bpm = spec.get("bpm", 96)
    bars = spec.get("bars", 16)
    beats_per_bar = spec.get("beatsPerBar", 4)
    chords = spec.get("chords", ["C", "Am", "F", "G"])
    instruments = spec.get("instruments", {})

    tracks = []

    # tempo / name
    ev = [(0, meta_name(spec.get("name", "bgm"))), (0, meta_tempo(bpm))]
    tracks.append(track_chunk(ev))

    def bar_chord(bar):
        return chords[bar % len(chords)]

    def add_track(name, ch, prog, pattern):
        ev = [(0, meta_name(name)), (0, program(ch, prog))]
        last = 0
        for bar in range(bars):
            root, quality = parse_chord(bar_chord(bar))
            base_oct = 4 if pattern in ("arp", "block") else 3
            notes = chord_notes(root, quality, base_oct)
            step = TPQ // 2  # eighth

            if pattern == "arp":
                seq = [notes[0], notes[2], notes[1], notes[0] + 12,
                       notes[2], notes[1], notes[0], notes[2]]
                for i, n in enumerate(seq):
                    t = bar * beats_per_bar * TPQ + i * step
                    ev.append((t - last, note_on(ch, n, 72 if i % 2 == 0 else 58)))
                    last = t
                    ev.append((step - 40, note_off(ch, n)))
                    last += step - 40
            elif pattern == "finger":
                seq = [notes[0], notes[2], notes[1] + 12, notes[2],
                       notes[0], notes[2], notes[1] + 12, notes[1]]
                for i, n in enumerate(seq):
                    t = bar * beats_per_bar * TPQ + i * step
                    ev.append((t - last, note_on(ch, n, 64)))
                    last = t
                    ev.append((step - 40, note_off(ch, n)))
                    last += step - 40
            elif pattern in ("sustain", "block"):
                vel = 64 if pattern == "block" else 46
                for n in notes:
                    t = bar * beats_per_bar * TPQ
                    ev.append((t - last, note_on(ch, n, vel)))
                    last = t
                hold = beats_per_bar * TPQ - 30
                ev.append((hold, note_off(ch, notes[0])))
                for n in notes[1:]:
                    ev.append((0, note_off(ch, n)))
                last += hold
        tracks.append(track_chunk(ev))

    # 钢琴
    if "piano" in instruments:
        cfg = instruments["piano"]
        add_track("Piano", 0, cfg.get("program", 0), cfg.get("pattern", "arp"))
    # 合成器 Pad
    if "pad" in instruments:
        cfg = instruments["pad"]
        add_track("Pad", 1, cfg.get("program", 89), cfg.get("pattern", "sustain"))
    # 木吉他
    if "guitar" in instruments:
        cfg = instruments["guitar"]
        add_track("Guitar", 2, cfg.get("program", 24), cfg.get("pattern", "finger"))

    # 鼓（通道 9）
    if "drums" in instruments:
        ch = 9
        ev = [(0, meta_name("Drums"))]
        last = 0
        for bar in range(bars):
            start = bar * beats_per_bar * TPQ
            by_time = {}
            for beat in range(beats_per_bar):
                t = start + beat * TPQ
                by_time.setdefault(t, []).append((36 if beat % 2 == 0 else 38,
                                                  100 if beat % 2 == 0 else 92))
                by_time.setdefault(t, []).append((42, 60))
                by_time.setdefault(t + TPQ // 2, []).append((42, 48))

            for t in sorted(by_time):
                for i, (n, v) in enumerate(by_time[t]):
                    ev.append((t - last if i == 0 else 0, note_on(ch, n, v)))
                last = t
                first = True
                for n, v in by_time[t]:
                    ev.append((30 if first else 0, note_off(ch, n)))
                    first = False
                last += 30
        tracks.append(track_chunk(ev))

    header = b"MThd" + struct.pack(">IHHH", 6, 1, len(tracks), TPQ)
    return header + b"".join(tracks)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--spec", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    with open(args.spec, encoding="utf-8") as f:
        spec = json.load(f)

    data = build(spec)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "wb") as f:
        f.write(data)
    print("WROTE", args.out, len(data), "bytes")


if __name__ == "__main__":
    main()
