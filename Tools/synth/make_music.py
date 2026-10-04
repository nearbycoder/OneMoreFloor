"""The soundtrack: elevator muzak that gets nervous when you do.

"Going Up" (gameplay): 104 BPM bossa in F, 16 bars, five stems that loop sample-exactly together:
    bed      Rhodes comp + upright bass + brushes/shaker/rim + soft kick   (always on)
    melody   vibraphone tune                                               (calm)
    trouble  pizzicato tritone ostinato + ticking woodblock + tremolo cello (rising trouble)
    rush     congas, timbales, horn stabs                                  (rush hour / hot streak)
    night    Leslie organ pad                                              (night shifts, vampires)
"Lobby Lounge" (title): 88 BPM, same changes, gentler.
Plus stingers: shift start, clock-out cadence, finale.
"""
import os

import numpy as np

from dsp import *  # noqa: F401,F403

CHORDS = {
    "F9": (41, [4, 7, 11, 14]), "Gm9": (43, [3, 7, 10, 14]), "C13": (36, [4, 10, 14, 21]), "Am7": (45, [3, 7, 10]),
    "D7b9": (38, [4, 10, 13]), "Dm7": (38, [3, 7, 10]), "Gm7": (43, [3, 7, 10]), "C7": (36, [4, 7, 10]),
    "Bbmaj7": (46, [4, 7, 11]), "Bbm6": (46, [3, 7, 9]), "Eb9": (39, [4, 10, 14]), "D7": (38, [4, 7, 10]),
    "Fmaj7": (41, [4, 7, 11]),
}

# two chords per bar (half-bar each)
PROG = [("F9", "F9"), ("Gm9", "C13"), ("F9", "F9"), ("Am7", "D7b9"), ("Gm9", "Gm9"), ("C13", "C13"), ("Am7", "Dm7"), ("Gm7", "C7"),
        ("Bbmaj7", "Bbmaj7"), ("Bbm6", "Eb9"), ("Am7", "Am7"), ("D7", "D7"), ("Gm7", "Gm7"), ("C7", "C7"), ("Fmaj7", "D7"), ("Gm7", "C7")]

# (beat, midi, beats)
MELODY = [
    (0, 69, 1.5), (1.5, 72, 0.5), (2, 76, 1), (3, 74, 1),
    (4, 72, 1.5), (5.5, 70, 0.5), (6, 69, 1), (7, 67, 1),
    (8, 69, 0.5), (8.5, 72, 0.5), (9, 77, 1.5), (10.5, 76, 0.5), (11, 72, 1),
    (12, 76, 1), (13, 74, 0.5), (13.5, 72, 0.5), (14, 75, 1), (15, 74, 1),
    (16, 74, 1.5), (17.5, 77, 0.5), (18, 77, 1), (19, 74, 1),
    (20, 76, 1.5), (21.5, 69, 0.5), (22, 70, 1), (23, 67, 1),
    (24, 69, 1), (25, 72, 1), (26, 77, 1), (27, 76, 0.5), (27.5, 74, 0.5),
    (28, 72, 2.5),
    (32, 74, 1.5), (33.5, 77, 0.5), (34, 81, 1), (35, 79, 1),
    (36, 77, 1.5), (37.5, 73, 0.5), (38, 72, 1), (39, 70, 1),
    (40, 72, 1), (41, 76, 1), (42, 79, 1.5), (43.5, 76, 0.5),
    (44, 78, 1.5), (45.5, 76, 0.5), (46, 74, 1), (47, 72, 1),
    (48, 70, 1), (49, 74, 1), (50, 77, 1.5), (51.5, 74, 0.5),
    (52, 76, 1.5), (53.5, 79, 0.5), (54, 76, 1), (55, 72, 1),
    (56, 69, 0.5), (56.5, 72, 0.5), (57, 77, 1), (58, 78, 1.5), (59.5, 74, 0.5),
    (60, 79, 1), (61, 76, 1), (62, 72, 2),
]

LOUNGE_MELODY = [
    (0, 72, 2), (2, 76, 1), (3, 74, 1), (4, 72, 3), (7, 70, 1),
    (8, 69, 2), (10, 72, 1), (11, 77, 1), (12, 76, 3), (15, 75, 1),
    (16, 74, 2), (18, 77, 1), (19, 81, 1), (20, 79, 3), (23, 76, 1),
    (24, 77, 1.5), (25.5, 76, 0.5), (26, 74, 1), (27, 72, 1), (28, 72, 4),
    (32, 74, 2), (34, 77, 1), (35, 81, 1), (36, 80, 3), (39, 77, 1),
    (40, 76, 2), (42, 72, 1), (43, 76, 1), (44, 78, 3), (47, 74, 1),
    (48, 74, 2), (50, 77, 1), (51, 74, 1), (52, 76, 3), (55, 72, 1),
    (56, 77, 1.5), (57.5, 74, 0.5), (58, 78, 2), (60, 77, 4),
]


def voicing(chord, center=64):
    root, tones = CHORDS[chord]
    notes = []
    for t in tones:
        pc = (root + t) % 12
        n = pc + 12 * ((center - 6 - pc) // 12 + 1)
        while n < center - 7:
            n += 12
        while n > center + 7:
            n -= 12
        notes.append(n)
    return sorted(set(notes))


def chord_at(beat):
    bar = int(beat // 4) % len(PROG)
    half = 0 if (beat % 4) < 2 else 1
    return PROG[bar][half]


def render(bpm, bars, parts, ir, wet=0.22):
    beat = 60.0 / bpm
    dur = bars * 4 * beat
    tr = Track(dur)
    parts(tr, beat)
    loop = tr.looped()
    out = convolve(loop, ir, wet, loop=True)
    # circular highpass at exactly the loop length keeps the loop seamless
    n = len(out)
    f = np.fft.rfftfreq(n, 1 / SR)
    resp = 1 / np.sqrt(1 + (40.0 / np.maximum(f, 1e-3)) ** 4)
    for c in range(2):
        out[:, c] = np.fft.irfft(np.fft.rfft(out[:, c]) * resp, n)
    return out


# ----------------------------------------------------------------------------- stems

def bed(tr, beat, lounge=False):
    bars = len(PROG)
    comp_a = [(0, 1.5), (1.5, 0.5), (3, 1.0)] if not lounge else [(0, 3.5)]
    comp_b = [(0.5, 1.0), (2, 1.5), (3.5, 0.5)] if not lounge else [(2, 2.0)]
    for b in range(bars):
        for k, (st, ln) in enumerate(comp_a if b % 2 == 0 else comp_b):
            at = b * 4 + st
            ch = chord_at(at)
            vel = 0.55 + (0.15 if k == 0 else 0.0) + rng.uniform(-0.05, 0.05)
            for i, n in enumerate(voicing(ch)):
                tr.add(rhodes(midi_hz(n), ln * beat * 0.95, vel), at * beat + i * 0.008, 0.6, pan=-0.15 + 0.1 * i)
        # bass: root (dotted), fifth, next root, approach
        r1 = CHORDS[PROG[b][0]][0]
        r2 = CHORDS[PROG[b][1]][0]
        nxt = CHORDS[PROG[(b + 1) % bars][0]][0]
        if lounge:
            pattern = [(0, r1, 1.8), (2, r2 + 7 if r1 == r2 else r2, 1.8)]
        else:
            pattern = [(0, r1, 1.4), (1.5, r1 + 7, 0.45), (2, r2 if r2 != r1 else r1 + 7, 1.4),
                       (3.5, nxt + (1 if nxt > r2 else -1), 0.45)]
        for st, n, ln in pattern:
            while n > 52:
                n -= 12
            tr.add(upright_bass(midi_hz(n), ln * beat, 0.9), (b * 4 + st) * beat, 0.55, pan=0.05)
        # drums
        if not lounge:
            for st, v in ((0, 0.55), (1.5, 0.25), (2, 0.45), (3.5, 0.25)):
                tr.add(kick(0.3, v, 60), (b * 4 + st) * beat, 0.4)
            for i in range(16):
                acc = 0.55 if i % 2 == 1 else 0.3
                tr.add(shaker(acc + rng.uniform(-0.05, 0.05)), (b * 4 + i * 0.25) * beat + rng.uniform(-0.004, 0.004), 0.5, pan=0.35)
            clave = [0, 3, 6] if b % 2 == 0 else [2, 5]
            for e in clave:
                tr.add(rim(0.55), (b * 4 + e * 0.5) * beat, 0.45, pan=-0.3)
        for st in (1, 3):
            tr.add(brush(0.42 * beat * 2, 0.5), (b * 4 + st - 0.15) * beat, 0.42 if not lounge else 0.3, pan=0.15)
            tr.add(snare_brush_tap(0.35), (b * 4 + st) * beat, 0.25, pan=0.15)


def melody(tr, beat, notes=MELODY, octave=0, gain=0.62):
    for st, n, ln in notes:
        vel = 0.75 + rng.uniform(-0.08, 0.08)
        tr.add(vibraphone(midi_hz(n + octave), ln * beat, vel), st * beat, gain, pan=0.2)


def trouble(tr, beat):
    for b in range(len(PROG)):
        for i in range(8):
            at = b * 4 + i * 0.5
            root = CHORDS[chord_at(at)][0]
            off = [0, 1, 6, 7, 0, 1, 6, 11][i]
            n = 60 + (root + off) % 12
            tr.add(pizzicato(midi_hz(n), 0.3, 0.7 if i % 2 == 0 else 0.5), at * beat, 0.5, pan=-0.35 + 0.1 * (i % 3))
            tr.add(woodblock(1500 if i % 2 == 0 else 1100, 0.4), at * beat, 0.18, pan=0.45)
        # tremolo cello on the bar root
        root = CHORDS[PROG[b][0]][0]
        n = root + 12
        dur = 4 * beat
        x = brass(midi_hz(n), dur, 0.55, 0.35)
        tt = np.arange(len(x)) / SR
        x = lowpass(x * (0.6 + 0.4 * np.sin(2 * np.pi * 12 * tt)), 1400)
        tr.add(x, b * 4 * beat, 0.5, pan=0.0)


def rush(tr, beat):
    hits = [(0.5, 196, False), (1.0, 330, True), (1.5, 180, False), (2.5, 247, False), (3.0, 330, True), (3.5, 196, False)]
    for b in range(len(PROG)):
        for st, f, slap in hits:
            tr.add(conga(f, 0.7 + rng.uniform(-0.1, 0.1), slap), (b * 4 + st) * beat, 0.55, pan=-0.4 if f < 220 else 0.3)
        if b % 2 == 1:
            ch = chord_at(b * 4 + 3.5)
            for i, n in enumerate(voicing(ch, 70)):
                tr.add(brass(midi_hz(n), 0.35 * beat, 0.9), (b * 4 + 3.5) * beat, 0.4, pan=0.1 * i - 0.1)
        if b % 8 == 7:
            for k in range(8):
                tr.add(timbale(620 if k % 2 == 0 else 760, 0.6 + k * 0.04), (b * 4 + 2 + k * 0.25) * beat, 0.45, pan=0.35)
        tr.add(woodblock(900, 0.5), (b * 4 + 0) * beat, 0.25, pan=0.5)  # cowbell-ish downbeat


def night(tr, beat):
    for b in range(len(PROG)):
        for h in range(2):
            ch = PROG[b][h]
            for n in voicing(ch, 60):
                tr.add(organ(midi_hz(n), 2 * beat * 0.98, 0.7), (b * 4 + h * 2) * beat, 0.5, pan=-0.2)


# ----------------------------------------------------------------------------- stingers

def sting_start():
    tr = Track(3.0)
    tr.add(bell(1046.5, 1.6, 0.9), 0.0, 0.5)
    tr.add(bell(1318.5, 1.6, 0.9), 0.25, 0.5)
    for i, n in enumerate(voicing("F9", 66)):
        tr.add(brass(midi_hz(n), 0.6, 0.9), 0.55, 0.35, pan=-0.2 + 0.1 * i)
        tr.add(rhodes(midi_hz(n), 1.6, 0.7), 0.55, 0.3)
    tr.add(upright_bass(midi_hz(41), 1.2, 1.0), 0.55, 0.9)
    tr.add(kick(0.4, 0.8, 50), 0.55, 0.8)
    return tr.oneshot(0.5)


def sting_clockout():
    beat = 60 / 104
    tr = Track(4.0)
    seqs = [("Gm9", 0), ("C13", 2), ("F9", 4)]
    for ch, st in seqs:
        for i, n in enumerate(voicing(ch, 64)):
            tr.add(rhodes(midi_hz(n), (2 if ch != "F9" else 4) * beat, 0.65), st * beat + i * 0.01, 0.4)
        r = CHORDS[ch][0]
        tr.add(upright_bass(midi_hz(r), 1.8 * beat, 0.9), st * beat, 0.9)
    for k, n in enumerate([72, 76, 79, 81, 84]):
        tr.add(vibraphone(midi_hz(n), 0.8, 0.8), (4 + k * 0.25) * beat, 0.55)
    tr.add(brush(0.6, 0.5), 4 * beat, 0.4)
    return tr.oneshot(1.0)


def sting_rush():
    tr = Track(1.6)
    for i, n in enumerate(voicing("C13", 70)):
        tr.add(brass(midi_hz(n), 0.18, 1.0), 0.0, 0.4)
        tr.add(brass(midi_hz(n + 5), 0.5, 1.0), 0.28, 0.4)
    for k in range(4):
        tr.add(timbale(700, 0.8), k * 0.07, 0.5)
    return tr.oneshot(0.6)


def sting_finale():
    tr = Track(7.0)
    for i, n in enumerate(voicing("F9", 64) + [77, 81]):
        tr.add(rhodes(midi_hz(n), 5.0, 0.6), i * 0.12, 0.35)
        tr.add(organ(midi_hz(n), 5.5, 0.4), 0.2, 0.3)
    for k, n in enumerate([84, 88, 91, 96]):
        tr.add(bell(midi_hz(n), 2.5, 0.7), 1.0 + k * 0.4, 0.35)
    tr.add(upright_bass(midi_hz(41), 4.0, 0.9), 0.0, 0.9)
    return tr.oneshot(1.5)


def main(outdir):
    os.makedirs(outdir, exist_ok=True)
    ir = reverb_ir(2.2, damp=0.45)
    report = []
    stems = {
        "going_up_bed": (104, bed, 0.2),
        "going_up_melody": (104, melody, 0.3),
        "going_up_trouble": (104, trouble, 0.2),
        "going_up_rush": (104, rush, 0.18),
        "going_up_night": (104, night, 0.35),
    }
    levels = {"going_up_bed": -16.5, "going_up_melody": -19.0, "going_up_trouble": -20.0, "going_up_rush": -20.0, "going_up_night": -23.0}
    for name, (bpm, fn, wet) in stems.items():
        x = render(bpm, len(PROG), fn, ir, wet)
        # level each stem to its target RMS (relative mix), then guard the peak
        x = x * (10 ** ((levels[name] - rms_db(x)) / 20))
        if np.max(np.abs(x)) > 0.95:
            x = soft_clip(x, 1.2) * 0.95
        write_wav(os.path.join(outdir, name + ".wav"), x)
        seam = float(np.max(np.abs(x[0] - x[-1])))
        report.append(f"{name:20s} {len(x) / SR:6.2f}s rms {rms_db(x):6.1f} dB peak {np.max(np.abs(x)):.2f} seam {seam:.3f}")

    lounge = render(88, len(PROG), lambda tr, beat: (bed(tr, beat, lounge=True), melody(tr, beat, LOUNGE_MELODY, -12, 0.7)), ir, 0.28)
    lounge = lounge * (10 ** ((-16.0 - rms_db(lounge)) / 20))
    write_wav(os.path.join(outdir, "lobby_lounge.wav"), np.clip(lounge, -0.97, 0.97))
    report.append(f"lobby_lounge         {len(lounge) / SR:6.2f}s rms {rms_db(lounge):6.1f} dB")

    for name, fn in (("sting_start", sting_start), ("sting_clockout", sting_clockout), ("sting_rush", sting_rush), ("sting_finale", sting_finale)):
        x = convolve(fn(), ir, 0.25)
        x = normalize(x, 0.85)
        write_wav(os.path.join(outdir, name + ".wav"), x)
        report.append(f"{name:20s} {len(x) / SR:6.2f}s rms {rms_db(x):6.1f} dB")
    return report
