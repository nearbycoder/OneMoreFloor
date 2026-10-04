"""All sound effects and voices for One More Floor, synthesized from scratch."""
import math
import os

import numpy as np

from dsp import *  # noqa: F401,F403
import dsp


def mono(x, peak=0.85):
    return normalize(fade(x, 0.001, 0.015), peak)


def stereo_verb(x, wet=0.18, secs=1.0, peak=0.85):
    if x.ndim == 1:
        x = np.stack([x, x], axis=1)
    pad = np.concatenate([x, np.zeros((int(secs * SR), 2))])
    y = convolve(pad, reverb_ir(secs, damp=0.6), wet)
    return normalize(y, peak)


def mixm(*xs):
    """Sum arrays of different lengths (zero-padded)."""
    n = max(len(x) for x in xs)
    out = np.zeros(n)
    for x in xs:
        out[:len(x)] += x
    return out


def seq(parts, dur):
    """parts: [(time, signal, gain)] -> mono mix."""
    out = np.zeros(int(dur * SR))
    for t0, s, g in parts:
        i = int(t0 * SR)
        e = min(len(out), i + len(s))
        if e > i:
            out[i:e] += s[:e - i] * g
    return out


# ----------------------------------------------------------------------------- mechanics

def btn_press():
    # bakelite clack + relay click + tiny spring
    n = int(0.16 * SR)
    t = np.arange(n) / SR
    clack = bandpass(noise(n), 2600, 1.5) * np.exp(-t / 0.006) + np.sin(2 * np.pi * 900 * t) * np.exp(-t / 0.01) * 0.6
    relay = seq([(0.045, bandpass(noise(int(0.03 * SR)), 5000, 2) * exp_decay(int(0.03 * SR), 0.004), 0.5)], 0.16)
    spring = np.sin(2 * np.pi * 2300 * t) * np.exp(-t / 0.04) * 0.08
    return mono(clack + relay + spring)


def door_open():
    # scissor gate folding: a rattle of brass slats plus a soft slide
    dur = 0.45
    out = np.zeros(int(dur * SR))
    for k in range(9):
        t0 = 0.02 + k * 0.032 + rng.uniform(-0.006, 0.006)
        f = rng.uniform(1800, 3200)
        s = mixm(woodblock(f, 0.4) * 0.5, bandpass(noise(int(0.04 * SR)), 4500, 3) * exp_decay(int(0.04 * SR), 0.006) * 0.4)
        i = int(t0 * SR)
        out[i:i + len(s)] += s[:len(out) - i]
    slide = bandpass(noise(len(out)), 1200, 0.8) * np.sin(np.linspace(0, np.pi, len(out))) * 0.25
    clunk = seq([(0.36, kick(0.12, 0.5, 120), 1.0)], dur)
    return mono(out + slide + clunk)


def door_close():
    dur = 0.4
    out = np.zeros(int(dur * SR))
    for k in range(8):
        t0 = 0.01 + k * 0.026
        s = woodblock(rng.uniform(1900, 3300), 0.45) * 0.5
        i = int(t0 * SR)
        out[i:i + len(s)] += s[:len(out) - i]
    out += seq([(0.24, kick(0.16, 0.9, 95), 1.0), (0.24, rim(0.5), 0.6)], dur)
    return mono(out)


def ding(freq):
    x = mixm(bell(freq, 1.6, 0.9), 0.5 * bell(freq * 2, 0.8, 0.5, 0.5))
    return stereo_verb(x, 0.22, 1.2)


def ding_two(f0, f1):
    a = bell(f0, 1.4, 0.9)
    b = bell(f1, 1.6, 0.9)
    x = seq([(0, a, 1.0), (0.22, b, 1.0)], 2.0)
    return stereo_verb(x, 0.22, 1.2)


def motor_loop():
    # 2 s seamless hum: mains-ish fundamental, gear whine, soft cable hiss
    dur = 2.0
    t = t_axis(dur)
    hum = (np.sin(2 * np.pi * 60 * t) * 0.5 + np.sin(2 * np.pi * 120 * t) * 0.35 + np.sin(2 * np.pi * 180 * t) * 0.12)
    whine = np.sin(2 * np.pi * 440 * t + 0.8 * np.sin(2 * np.pi * 3 * t)) * 0.06
    hiss = bandpass(noise(len(t)), 1500, 0.7) * 0.12
    x = hum * 0.6 + whine + hiss
    # make the loop seamless by crossfading the end into the start
    k = int(0.2 * SR)
    x[:k] = x[:k] * np.linspace(0, 1, k) + x[-k:] * np.linspace(1, 0, k)
    x = x[:-k]
    return normalize(x, 0.6)


def car_start():
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    clunk = kick(0.2, 0.8, 70)
    whir = sweep(80, 260, 0.5) * np.minimum(t / 0.1, 1) * np.exp(-t / 0.4) * 0.3
    return mono(seq([(0, clunk, 1.0)], 0.5) + whir)


def car_stop():
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    whir = sweep(260, 70, 0.3) * np.exp(-t[:int(0.3 * SR)] / 0.2) * 0.3
    thunk = kick(0.25, 1.0, 60)
    squeak = sine(1900, 0.12) * exp_decay(int(0.12 * SR), 0.05) * 0.06
    return mono(seq([(0, whir, 1.0), (0.18, thunk, 1.0), (0.17, squeak, 1.0)], 0.45))


def quick_ding():
    return stereo_verb(bell(1318.5, 0.6, 0.7, 0.6), 0.15, 0.6)


# ----------------------------------------------------------------------------- the building

def shuffle_lift():
    # heavy drawer pulled out: wooden grind + low whoosh
    dur = 0.6
    n = int(dur * SR)
    t = np.arange(n) / SR
    grind = lowpass(noise(n), 500) * (0.6 + 0.4 * np.sin(2 * np.pi * 37 * t)) * np.sin(np.pi * t / dur) ** 0.6
    whoosh = svf_bandpass(noise(n), 400 + 900 * t / dur, 1.0) * np.sin(np.pi * t / dur) * 0.6
    ratchet = np.zeros(n)
    for k in range(10):
        i = int((0.05 + k * 0.05) * SR)
        c = woodblock(700 + k * 30, 0.3)
        ratchet[i:i + len(c)] += c[:n - i] * 0.35
    return stereo_verb(grind * 0.7 + whoosh + ratchet, 0.12, 0.6, 0.8)


def shuffle_land():
    # floor slots home: thud, wooden knock, little bounce
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    thud = kick(0.5, 1.0, 58)
    knock = lowpass(noise(n), 900) * np.exp(-t / 0.03) * 0.6
    bounce = seq([(0.16, kick(0.2, 0.35, 70), 1.0), (0.26, kick(0.15, 0.15, 80), 1.0)], 0.6)
    dust = highpass(noise(n), 3000) * np.exp(-t / 0.15) * 0.05
    return stereo_verb(seq([(0, thud, 1.0)], 0.6) + knock + bounce + dust, 0.15, 0.7, 0.9)


def rumble():
    dur = 1.4
    n = int(dur * SR)
    t = np.arange(n) / SR
    env = np.sin(np.pi * t / dur) ** 0.5
    x = lowpass(noise(n), 120, 2) * env * 1.8
    x += np.sin(2 * np.pi * 38 * t) * np.sin(np.pi * t / dur) * 0.2
    # saturate so the rumble grows harmonics a laptop speaker can actually play, plus a rattle of the frame
    x = np.tanh(x * 2.2) * 0.6
    rattle = bandpass(noise(n), 160, 1.4) * env * (0.6 + 0.4 * np.sin(2 * np.pi * 11 * t)) * 1.4
    x += rattle + highpass(lowpass(noise(n), 700), 250) * env * 0.18
    return mono(x, 0.8)


def jam():
    dur = 0.8
    n = int(dur * SR)
    t = np.arange(n) / SR
    grind = bandpass(noise(n), 900, 2) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 23 * t))) * np.exp(-t / 0.4)
    squeal = np.sin(2 * np.pi * (1400 + 120 * np.sin(2 * np.pi * 9 * t)) * t) * np.exp(-t / 0.25) * 0.25
    sparks = np.zeros(n)
    for k in range(18):
        i = int(rng.uniform(0, 0.6) * SR)
        c = highpass(noise(int(0.01 * SR)), 5000) * 0.5
        sparks[i:i + len(c)] += c[:n - i]
    clank = seq([(0.0, woodblock(500, 0.8), 1.0), (0.0, kick(0.2, 0.7, 90), 1.0)], dur)
    return stereo_verb(grind + squeal + sparks + clank, 0.15, 0.7)


def floor_depart():
    dur = 1.4
    n = int(dur * SR)
    t = np.arange(n) / SR
    creak = np.sin(2 * np.pi * (180 + 60 * np.sin(2 * np.pi * 1.3 * t)) * t) * np.exp(-t / 0.4) * 0.3
    creak = bandpass(creak + noise(n) * 0.1 * np.exp(-t / 0.3), 600, 1.5)
    whoosh = svf_bandpass(noise(n), 300 + 1600 * (t / dur), 1.2) * np.sin(np.pi * np.clip((t - 0.2) / 1.2, 0, 1)) * 0.8
    return stereo_verb(creak + whoosh, 0.25, 1.2)


def floor_arrive():
    dur = 1.1
    n = int(dur * SR)
    t = np.arange(n) / SR
    whoosh = svf_bandpass(noise(n), 1800 - 1400 * (t / dur), 1.2) * np.sin(np.pi * np.clip(t / 0.8, 0, 1)) * 0.8
    thud = seq([(0.72, kick(0.35, 1.0, 50), 1.0)], dur)
    return stereo_verb(whoosh + thud, 0.2, 1.0)


def leaving_tick():
    a = bell(1760, 0.4, 0.6, 0.4)
    return stereo_verb(seq([(0, a, 1.0), (0.12, a, 0.7)], 0.7), 0.12, 0.5, 0.7)


# ----------------------------------------------------------------------------- passengers & scoring

def board_hop():
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    boing = np.sin(2 * np.pi * np.cumsum(300 + 500 * (1 - np.exp(-t / 0.05))) / SR) * np.exp(-t / 0.08)
    tap = rim(0.3)
    return mono(boing * 0.8 + seq([(0.0, tap, 0.4)], 0.22))


def exit_cheer():
    notes = [72, 76, 79, 84]
    parts = [(i * 0.055, sine(midi_hz(m), 0.22) * exp_decay(int(0.22 * SR), 0.08), 0.6) for i, m in enumerate(notes)]
    return stereo_verb(seq(parts, 0.6), 0.15, 0.5)


def coin():
    a = bell(1975.5, 0.5, 0.6, 0.3)
    b = bell(2637.0, 0.7, 0.7, 0.3)
    x = seq([(0, a, 1.0), (0.07, b, 1.0)], 0.9)
    return stereo_verb(x, 0.12, 0.5, 0.75)


def cash_register():
    dur = 1.2
    ring = seq([(0.12, bell(2093, 1.0, 0.9, 0.6), 1.0), (0.12, bell(2637, 0.9, 0.6, 0.6), 1.0)], dur)
    clunk = seq([(0.0, woodblock(800, 0.6), 1.0), (0.03, kick(0.15, 0.5, 120), 1.0), (0.06, bandpass(noise(int(0.12 * SR)), 3000, 1) * exp_decay(int(0.12 * SR), 0.03), 0.5)], dur)
    return stereo_verb(ring + clunk, 0.15, 0.7)


def streak_up(step):
    f = midi_hz(72 + step)
    return mono(sine(f, 0.15) * exp_decay(int(0.15 * SR), 0.05) + sine(f * 2, 0.15) * exp_decay(int(0.15 * SR), 0.03) * 0.3, 0.6)


def fanfare(level):
    base = [60, 64, 67, 72] if level == 2 else [60, 64, 67, 72, 76] if level == 3 else [60, 64, 67, 72, 76, 79]
    parts = []
    for i, m in enumerate(base):
        parts.append((i * 0.07, brass(midi_hz(m), 0.18 if i < len(base) - 1 else 0.6, 0.8), 0.6))
    return stereo_verb(seq(parts, 1.4), 0.2, 0.9)


def complaint():
    # grumble ("hmph") + rubber stamp
    n = int(0.7 * SR)
    t = np.arange(n) / SR
    f = 140 - 40 * t
    voice = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.18)
    voice = bandpass(np.sign(voice) * np.abs(voice) ** 0.5, 500, 1.2) * 0.6
    stamp = seq([(0.3, kick(0.25, 1.0, 85), 1.0), (0.3, bandpass(noise(int(0.08 * SR)), 1200, 1) * exp_decay(int(0.08 * SR), 0.02), 0.8)], 0.7)
    return stereo_verb(voice + stamp, 0.12, 0.6)


def storm_off():
    dur = 0.9
    out = np.zeros(int(dur * SR))
    for k in range(4):
        s = kick(0.15, 0.7 - k * 0.12, 110) + bandpass(noise(int(0.15 * SR)), 800, 1) * exp_decay(int(0.15 * SR), 0.03) * 0.5
        i = int(k * 0.2 * SR)
        out[i:i + len(s)] += s[:len(out) - i]
    return mono(out)


def refuse():
    n = int(0.3 * SR)
    t = np.arange(n) / SR
    buzz = np.sign(np.sin(2 * np.pi * 150 * t)) * 0.3 + np.sign(np.sin(2 * np.pi * 151.5 * t)) * 0.3
    buzz = lowpass(buzz, 1800) * adsr(n, 0.005, 0.05, 0.8, 0.05)
    return mono(seq([(0, buzz[:int(0.12 * SR)], 1.0), (0.15, buzz[:int(0.12 * SR)], 1.0)], 0.3), 0.6)


def poof():
    dur = 0.9
    n = int(dur * SR)
    t = np.arange(n) / SR
    puff = lowpass(noise(n), 1500) * np.exp(-t / 0.1)
    flutter = np.zeros(n)
    for k in range(26):
        i = int(rng.uniform(0.05, 0.75) * SR)
        c = bandpass(noise(int(0.025 * SR)), rng.uniform(1500, 3500), 3) * 0.5
        flutter[i:i + len(c)] += c[:n - i]
    squeak = sweep(3000, 4200, 0.08) * 0.1
    return stereo_verb(puff + flutter + seq([(0.15, squeak, 1.0), (0.35, squeak, 0.7)], dur), 0.2, 0.8)


def sparkle():
    dur = 1.0
    out = np.zeros(int(dur * SR))
    notes = [84, 88, 91, 96, 91, 96, 100]
    for i, m in enumerate(notes):
        s = bell(midi_hz(m), 0.5, 0.5, 0.4)
        k = int(i * 0.06 * SR)
        out[k:k + len(s)] += s[:len(out) - k]
    return stereo_verb(out, 0.3, 1.0, 0.7)


def wilt():
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    x = sweep(660, 330, 0.6) * np.exp(-t / 0.3) * 0.5 + sweep(990, 495, 0.6) * np.exp(-t / 0.2) * 0.2
    return mono(x, 0.6)


def splash():
    dur = 1.0
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = lowpass(noise(n), 3000) * np.exp(-t / 0.18) + bandpass(noise(n), 700, 1) * np.exp(-t / 0.35) * 0.6
    drops = np.zeros(n)
    for k in range(12):
        i = int(rng.uniform(0.1, 0.8) * SR)
        f = rng.uniform(900, 2200)
        d = sweep(f, f * 1.6, 0.04) * exp_decay(int(0.04 * SR), 0.015) * 0.3
        drops[i:i + len(d)] += d[:n - i]
    return stereo_verb(x + drops, 0.2, 0.8)


def kid_giggle():
    out = np.zeros(int(0.7 * SR))
    for k in range(5):
        f = 520 + 80 * (k % 2) + k * 25
        s = sine(f, 0.09) * exp_decay(int(0.09 * SR), 0.04)
        s *= 1 + 0.5 * np.sin(2 * np.pi * 30 * t_axis(0.09))
        i = int(k * 0.1 * SR)
        out[i:i + len(s)] += s
    return mono(bandpass(out, 1000, 0.6) * 2)


def button_mash():
    dur = 0.6
    parts = [(k * 0.06 + rng.uniform(0, 0.02), btn_press(), 0.5) for k in range(8)]
    return mono(seq(parts, dur))


def harrumph():
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    f = 95 + 25 * np.exp(-t / 0.1)
    v = np.sin(2 * np.pi * np.cumsum(f) / SR)
    v = bandpass(np.tanh(v * 3), 400, 1) * adsr(n, 0.02, 0.1, 0.6, 0.15)
    return mono(v)


def record_scratch():
    dur = 0.45
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = 300 + 900 * np.abs(np.sin(2 * np.pi * 3.2 * t))
    x = bandpass(noise(n), 1, 1) * 0
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.3 + bandpass(noise(n), 2000, 1) * 0.4
    x *= np.exp(-t / 0.25)
    return mono(x, 0.7)


def tick():
    return mono(woodblock(2000, 0.6) * 0.6 + woodblock(1000, 0.3) * 0.3, 0.6)


def alarm_bell():
    dur = 1.0
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = bell(1800, 1.0, 0.8, 1.2) * (0.6 + 0.4 * np.sign(np.sin(2 * np.pi * 18 * t)))
    return stereo_verb(x * np.exp(-t / 0.5), 0.15, 0.6)


def punch_clock():
    dur = 1.0
    x = seq([(0.0, kick(0.2, 1.0, 140), 1.0), (0.0, woodblock(600, 0.9), 1.0),
             (0.06, bandpass(noise(int(0.1 * SR)), 3000, 1) * exp_decay(int(0.1 * SR), 0.02), 0.6),
             (0.25, bell(1567.98, 0.8, 0.8), 1.0)], dur)
    return stereo_verb(x, 0.18, 0.7)


def sad_trombone():
    notes = [(0.0, 62, 0.35), (0.38, 61, 0.35), (0.76, 60, 0.35), (1.14, 59, 1.1)]
    out = np.zeros(int(2.6 * SR))
    for t0, m, d in notes:
        s = brass(midi_hz(m - 12), d, 0.9, 0.6)
        if d > 1:
            tt = t_axis(len(s) / SR)
            s *= 1 + 0.15 * np.sin(2 * np.pi * 6 * tt) * np.clip(tt - 0.2, 0, 1)
        i = int(t0 * SR)
        out[i:i + len(s)] += s[:len(out) - i]
    return stereo_verb(out, 0.2, 1.0)


def star_chime(i):
    m = [76, 79, 84][i]
    x = mixm(bell(midi_hz(m), 1.5, 0.9), bell(midi_hz(m + 12), 0.8, 0.4))
    return stereo_verb(x, 0.3, 1.3)


def new_record():
    parts = [(k * 0.08, bell(midi_hz(m), 0.9, 0.7), 1.0) for k, m in enumerate([72, 76, 79, 84, 88])]
    x = seq(parts, 1.6) + seq([(0.4, brass(midi_hz(60), 0.8, 0.7), 0.5), (0.4, brass(midi_hz(64), 0.8, 0.7), 0.4)], 1.6)
    return stereo_verb(x, 0.25, 1.2)


def ui_hover():
    return mono(sine(2400, 0.04) * exp_decay(int(0.04 * SR), 0.01), 0.35)


def ui_click():
    return mono(mixm(rim(0.6) * 0.8, sine(1200, 0.06) * exp_decay(int(0.06 * SR), 0.015) * 0.4), 0.6)


def ui_back():
    return mono(sweep(900, 500, 0.08) * exp_decay(int(0.08 * SR), 0.03), 0.5)


def ui_whoosh():
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    return mono(svf_bandpass(noise(n), 600 + 2400 * t / 0.35, 1.2) * np.sin(np.pi * t / 0.35), 0.5)


def ocean_loop():
    dur = 8.0
    n = int(dur * SR)
    t = np.arange(n) / SR
    swell = 0.55 + 0.45 * np.sin(2 * np.pi * t / dur * 2 - 1.2)
    x = lowpass(noise(n), 900) * swell + highpass(noise(n), 3000) * swell ** 3 * 0.25
    k = int(0.5 * SR)
    x[:k] = x[:k] * np.linspace(0, 1, k) + x[-k:] * np.linspace(1, 0, k)
    x = x[:-k]
    return normalize(np.stack([x, np.roll(x, 2000)], axis=1), 0.5)


def gull():
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    f = 1300 + 500 * np.sin(np.pi * t / 0.5)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR)
    x = bandpass(np.tanh(x * 2), 1800, 1.2) * np.sin(np.pi * t / 0.5) ** 2
    return stereo_verb(seq([(0, x, 1.0), (0.55, x[:int(0.35 * SR)] * 0.8, 1.0)], 1.0), 0.25, 1.0, 0.5)


def city_loop():
    dur = 10.0
    n = int(dur * SR)
    t = np.arange(n) / SR
    rumble_ = lowpass(noise(n), 200) * 0.8
    hum = bandpass(noise(n), 700, 0.5) * 0.15
    x = rumble_ + hum
    for k in range(3):
        c = int(rng.uniform(1, 8) * SR)
        honk = brass(rng.choice([392, 440, 349]), 0.18, 0.4, 0.5)
        x[c:c + len(honk)] += lowpass(honk, 1500)[:n - c] * 0.15
    k = int(0.6 * SR)
    x[:k] = x[:k] * np.linspace(0, 1, k) + x[-k:] * np.linspace(1, 0, k)
    x = x[:-k]
    return normalize(np.stack([x, np.roll(x, 3000)], axis=1), 0.35)


# ----------------------------------------------------------------------------- voices

VOWELS = {"a": (800, 1150, 2900), "e": (400, 1700, 2600), "i": (300, 2100, 3000), "o": (450, 800, 2830), "u": (325, 700, 2530)}

VOICE_PITCH = {"commuter": 130, "houseplant": 260, "mirror": 115, "vampire": 95, "courier": 150, "swimmer": 175, "kid": 330, "tycoon": 85, "bellhop": 190}


def syllable(f0, vowel, dur, kind):
    n = int(dur * SR)
    t = np.arange(n) / SR
    glide = f0 * (1 + 0.12 * np.sin(np.pi * t / dur) * (1 if rng.random() < 0.5 else -1))
    if kind == "vampire":
        glide *= 1 + 0.02 * np.sin(2 * np.pi * 6 * t)
    phase = np.cumsum(glide) / SR
    src = np.zeros(n)
    for h in range(1, 30):
        if h * f0 > SR / 2 - 500:
            break
        src += np.sin(2 * np.pi * h * phase) / h
    if kind in ("vampire", "houseplant"):
        src += noise(n) * (0.4 if kind == "vampire" else 0.25)
    if kind == "swimmer":
        src *= 1 + 0.4 * np.sin(2 * np.pi * 22 * t)  # gurgle
    f1, f2, f3 = VOWELS[vowel]
    shift = {"kid": 1.25, "houseplant": 1.15, "tycoon": 0.85, "vampire": 0.9}.get(kind, 1.0)
    y = bandpass(src, f1 * shift, 4) + 0.6 * bandpass(src, f2 * shift, 6) + 0.25 * bandpass(src, f3 * shift, 8)
    env = adsr(n, 0.01, 0.03, 0.8, 0.04)
    return y * env


def voice_clip(kind, seed):
    r = np.random.default_rng(seed)
    f0 = VOICE_PITCH[kind]
    parts = []
    t0 = 0.0
    for k in range(r.integers(2, 5)):
        d = r.uniform(0.06, 0.12)
        v = r.choice(list(VOWELS))
        p = f0 * r.uniform(0.9, 1.25)
        parts.append((t0, syllable(p, v, d, kind), 1.0))
        t0 += d + r.uniform(0.0, 0.03)
    return mono(seq(parts, t0 + 0.05), 0.7)


# ----------------------------------------------------------------------------- export

def all_sfx():
    out = {
        "btn_press": btn_press(), "door_open": door_open(), "door_close": door_close(),
        "ding_up": ding_two(1046.5, 1318.5), "ding_down": ding_two(1318.5, 1046.5), "ding_quick": quick_ding(),
        "motor_loop": motor_loop(), "car_start": car_start(), "car_stop": car_stop(),
        "shuffle_lift": shuffle_lift(), "shuffle_land": shuffle_land(), "rumble": rumble(), "jam": jam(),
        "floor_depart": floor_depart(), "floor_arrive": floor_arrive(), "leaving_tick": leaving_tick(),
        "board_hop": board_hop(), "exit_cheer": exit_cheer(), "coin": coin(), "cash_register": cash_register(),
        "fanfare_2": fanfare(2), "fanfare_3": fanfare(3), "fanfare_4": fanfare(4),
        "complaint": complaint(), "storm_off": storm_off(), "refuse": refuse(), "poof": poof(), "sparkle": sparkle(),
        "wilt": wilt(), "splash": splash(), "kid_giggle": kid_giggle(), "button_mash": button_mash(), "harrumph": harrumph(),
        "record_scratch": record_scratch(), "tick": tick(), "alarm_bell": alarm_bell(), "punch_clock": punch_clock(),
        "sad_trombone": sad_trombone(), "new_record": new_record(),
        "star_1": star_chime(0), "star_2": star_chime(1), "star_3": star_chime(2),
        "ui_hover": ui_hover(), "ui_click": ui_click(), "ui_back": ui_back(), "ui_whoosh": ui_whoosh(),
        "amb_ocean": ocean_loop(), "gull": gull(), "amb_city": city_loop(),
    }
    for i in range(12):
        out[f"streak_{i}"] = streak_up(i)
    for kind in VOICE_PITCH:
        for k in range(5):
            v = voice_clip(kind, k * 1000 + sum(map(ord, kind)))
            # level every variant to the same loudness so a random pick never jumps out or gets lost
            v = v * 10 ** ((-14.0 - active_rms_db(v)) / 20)
            out[f"voice_{kind}_{k}"] = v * min(1.0, 0.89 / max(1e-9, float(np.max(np.abs(v)))))
    return out


def main(outdir):
    os.makedirs(outdir, exist_ok=True)
    report = []
    for name, x in all_sfx().items():
        x = finish(x, loop=name.startswith(("amb_", "motor_loop")))
        write_wav(os.path.join(outdir, name + ".wav"), x)
        peak = float(np.max(np.abs(x)))
        report.append(f"{name:18s} {len(x) / SR:5.2f}s peak {peak:.2f} rms {rms_db(x):6.1f} dB")
    return report
