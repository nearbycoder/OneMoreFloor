"""Tiny procedural audio toolkit (numpy only) for One More Floor.

Everything is synthesized: oscillators, envelopes, FM electric piano, vibraphone, Karplus-Strong
strings, noise percussion, additive brass/organ, inharmonic bells, FFT convolution reverb.
Run through Blender's bundled Python (it ships numpy):  blender -b -P Tools/synth/make_audio.py
"""
import math
import wave

import numpy as np

SR = 44100
rng = np.random.default_rng(1931)


def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def midi_hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)


# ----------------------------------------------------------------------------- envelopes

def adsr(n, a=0.005, d=0.1, s=0.7, r=0.1, sustain_time=None):
    """Sample-count ADSR. sustain_time = seconds held before release (default: fill)."""
    a_n, d_n, r_n = int(a * SR), int(d * SR), int(r * SR)
    hold = n - a_n - d_n - r_n if sustain_time is None else int(sustain_time * SR)
    hold = max(0, hold)
    env = np.concatenate([
        np.linspace(0, 1, max(1, a_n), endpoint=False),
        np.linspace(1, s, max(1, d_n), endpoint=False),
        np.full(hold, s),
        np.linspace(s, 0, max(1, r_n)),
    ])
    if len(env) < n:
        env = np.concatenate([env, np.zeros(n - len(env))])
    return env[:n]


def exp_decay(n, tau, attack=0.002):
    t = np.arange(n) / SR
    env = np.exp(-t / max(tau, 1e-4))
    a = int(attack * SR)
    if a > 0:
        env[:a] *= np.linspace(0, 1, a)
    return env


def fade(x, fin=0.002, fout=0.01):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)
    return x


# ----------------------------------------------------------------------------- filters (vectorised via FFT)

def _fft_filter(x, response):
    n = len(x)
    size = 1 << int(math.ceil(math.log2(max(2, n))))
    X = np.fft.rfft(x, size)
    f = np.fft.rfftfreq(size, 1 / SR)
    y = np.fft.irfft(X * response(f), size)[:n]
    return y


def lowpass(x, cutoff, order=2):
    return _fft_filter(x, lambda f: 1 / np.sqrt(1 + (f / max(cutoff, 1)) ** (2 * order)))


def highpass(x, cutoff, order=2):
    return _fft_filter(x, lambda f: 1 / np.sqrt(1 + (max(cutoff, 1) / np.maximum(f, 1e-3)) ** (2 * order)))


def bandpass(x, center, q=2.0):
    bw = center / q
    return _fft_filter(x, lambda f: 1 / (1 + ((f - center) / (bw / 2)) ** 2))


def one_pole_lp(x, cutoff):
    """Time-domain one-pole lowpass (used for KS damping and sweeps)."""
    a = math.exp(-2 * math.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc = (1 - a) * x[i] + a * acc
        y[i] = acc
    return y


def svf_bandpass(x, center, q=1.0):
    """Chamberlin state-variable bandpass with a per-sample centre frequency (array or scalar)."""
    n = len(x)
    c = np.broadcast_to(np.asarray(center, dtype=np.float64), (n,))
    f = 2 * np.sin(np.pi * np.clip(c, 20, SR / 6) / SR)
    damp = 1.0 / q
    low = band = 0.0
    out = np.empty(n)
    for i in range(n):
        high = x[i] - low - damp * band
        band += f[i] * high
        low += f[i] * band
        out[i] = band
    return out


def noise(n):
    return rng.uniform(-1, 1, n)


# ----------------------------------------------------------------------------- oscillators / instruments

def sine(freq, dur, phase=0.0):
    t = t_axis(dur)
    return np.sin(2 * np.pi * freq * t + phase)


def sweep(f0, f1, dur, curve="exp"):
    n = int(dur * SR)
    if curve == "exp":
        f = f0 * (f1 / f0) ** np.linspace(0, 1, n)
    else:
        f = np.linspace(f0, f1, n)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def additive(freq, dur, partials, decays=None, brightness=None):
    """partials: list of (ratio, amp). decays: per-partial tau (s). brightness: env multiplying upper partials."""
    t = t_axis(dur)
    out = np.zeros_like(t)
    for i, (ratio, amp) in enumerate(partials):
        f = freq * ratio
        if f > SR / 2 - 100:
            continue
        p = amp * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi))
        if decays is not None:
            p *= np.exp(-t / decays[i])
        if brightness is not None and i > 0:
            p *= brightness ** min(i, 6)
        out += p
    return out


def rhodes(freq, dur, vel=0.8):
    """FM electric piano: 1:1 FM with decaying index plus a bell-like tine."""
    n = int((dur + 1.2) * SR)
    t = np.arange(n) / SR
    idx = (1.6 + 2.2 * vel) * np.exp(-t / 0.35) + 0.4
    mod = np.sin(2 * np.pi * freq * t)
    car = np.sin(2 * np.pi * freq * t + idx * mod)
    tine = np.sin(2 * np.pi * freq * 14.0 * t) * np.exp(-t / 0.03) * 0.25 * vel
    body = (car + tine) * exp_decay(n, 1.6 + 200 / freq, 0.003)
    # damper: release after dur
    rel = np.ones(n)
    k = int(dur * SR)
    if k < n:
        rel[k:] = np.exp(-np.arange(n - k) / (0.12 * SR))
    detune = np.sin(2 * np.pi * freq * 1.003 * t + idx * 0.8 * mod) * exp_decay(n, 1.4, 0.003) * 0.3
    return (body + detune) * rel * vel * 0.5


def vibraphone(freq, dur, vel=0.8, tremolo=5.6):
    n = int((dur + 1.5) * SR)
    t = np.arange(n) / SR
    tone = (np.sin(2 * np.pi * freq * t) * np.exp(-t / 2.2)
            + 0.35 * np.sin(2 * np.pi * freq * 4.0 * t) * np.exp(-t / 0.5)
            + 0.12 * np.sin(2 * np.pi * freq * 10.0 * t) * np.exp(-t / 0.12))
    strike = noise(n) * np.exp(-t / 0.004) * 0.3
    trem = 1 - 0.35 * (0.5 + 0.5 * np.sin(2 * np.pi * tremolo * t))
    rel = np.ones(n)
    k = int((dur + 0.4) * SR)
    if k < n:
        rel[k:] = np.exp(-np.arange(n - k) / (0.25 * SR))
    a = int(0.002 * SR)
    env = np.ones(n)
    env[:a] = np.linspace(0, 1, a)
    return (tone * trem + bandpass(strike, freq * 4, 3)) * rel * env * vel * 0.5


def karplus(freq, dur, damping=0.996, bright=0.5, pluck_noise=None):
    """Karplus-Strong plucked string."""
    n = int(dur * SR)
    period = max(2, int(SR / freq))
    buf = pluck_noise if pluck_noise is not None else noise(period)
    buf = one_pole_lp(buf, 800 + 6000 * bright)[:period].astype(np.float64)
    out = np.empty(n)
    idx = 0
    prev = 0.0
    for i in range(n):
        v = buf[idx]
        out[i] = v
        nv = damping * 0.5 * (v + prev)
        prev = v
        buf[idx] = nv
        idx = (idx + 1) % period
    return out


def upright_bass(freq, dur, vel=0.8):
    n = int((dur + 0.3) * SR)
    t = np.arange(n) / SR
    ks = karplus(freq, dur + 0.3, damping=0.997, bright=0.25)
    sub = np.sin(2 * np.pi * freq * t) * exp_decay(n, 0.9, 0.006)
    thump = np.sin(2 * np.pi * 60 * t) * np.exp(-t / 0.02) * 0.4
    env = np.ones(n)
    k = int(dur * SR)
    env[k:] = np.exp(-np.arange(n - k) / (0.06 * SR))
    x = (0.7 * ks + 0.6 * sub + thump) * env
    return lowpass(x, 900) * vel * 0.9


def pizzicato(freq, dur=0.35, vel=0.8):
    x = karplus(freq, dur, damping=0.985, bright=0.6)
    return fade(lowpass(x, 3000) * exp_decay(len(x), 0.12, 0.001) * vel, 0.001, 0.03)


def brass(freq, dur, vel=0.8, bright_peak=0.9):
    n = int((dur + 0.15) * SR)
    t = np.arange(n) / SR
    bright = np.clip(bright_peak * (1 - np.exp(-t / 0.03)) * np.exp(-t / (dur * 1.5 + 0.1)) + 0.25, 0, 1)
    partials = [(k, 1.0 / k) for k in range(1, 14)]
    x = additive(freq, dur + 0.15, partials, brightness=bright)
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.5 * t) * np.clip(t / 0.3, 0, 1)
    env = adsr(n, 0.02, 0.08, 0.75, 0.12)
    return x * env * vib * vel * 0.25


def organ(freq, dur, vel=0.7):
    n = int((dur + 0.1) * SR)
    t = np.arange(n) / SR
    draw = [(0.5, 0.6), (1, 1.0), (1.5, 0.5), (2, 0.7), (3, 0.3), (4, 0.35), (6, 0.12), (8, 0.1)]
    x = additive(freq, dur + 0.1, draw)
    leslie = 1 + 0.18 * np.sin(2 * np.pi * 6.2 * t)
    env = adsr(n, 0.03, 0.05, 0.9, 0.08)
    return x * leslie * env * vel * 0.18


def bell(freq, dur=2.0, vel=0.8, bright=1.0):
    partials = [(1.0, 1.0), (2.0, 0.5), (2.76, 0.45 * bright), (5.4, 0.25 * bright), (8.93, 0.12 * bright), (13.3, 0.06 * bright)]
    decays = [dur, dur * 0.7, dur * 0.45, dur * 0.25, dur * 0.12, dur * 0.06]
    x = additive(freq, dur, partials, decays)
    n = len(x)
    a = int(0.001 * SR)
    x[:a] *= np.linspace(0, 1, a)
    return x * vel * 0.4


def kick(dur=0.35, vel=0.8, pitch=55):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = pitch + 90 * np.exp(-t / 0.04)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.12)
    click = noise(n) * np.exp(-t / 0.002) * 0.2
    return (x + click) * vel


def brush(dur=0.35, vel=0.5):
    n = int(dur * SR)
    t = np.arange(n) / SR
    env = np.sin(np.clip(t / dur, 0, 1) * np.pi) ** 0.7
    x = bandpass(noise(n), 3500, 0.7) * env
    return x * vel * 0.6


def snare_brush_tap(vel=0.6):
    n = int(0.18 * SR)
    t = np.arange(n) / SR
    x = bandpass(noise(n), 2500, 0.8) * np.exp(-t / 0.05) + np.sin(2 * np.pi * 190 * t) * np.exp(-t / 0.03) * 0.3
    return x * vel


def shaker(vel=0.4):
    n = int(0.09 * SR)
    t = np.arange(n) / SR
    env = np.minimum(t / 0.01, 1) * np.exp(-t / 0.03)
    return highpass(noise(n), 6000) * env * vel


def rim(vel=0.6):
    n = int(0.08 * SR)
    t = np.arange(n) / SR
    x = (np.sin(2 * np.pi * 1700 * t) * 0.6 + np.sin(2 * np.pi * 520 * t) * 0.4) * np.exp(-t / 0.012)
    x += bandpass(noise(n), 3000, 2) * np.exp(-t / 0.006) * 0.5
    return x * vel


def woodblock(freq=1150, vel=0.6):
    n = int(0.12 * SR)
    t = np.arange(n) / SR
    x = np.sin(2 * np.pi * freq * t) * np.exp(-t / 0.025) + 0.4 * np.sin(2 * np.pi * freq * 2.7 * t) * np.exp(-t / 0.01)
    return x * vel


def conga(freq=220, vel=0.7, slap=False):
    n = int(0.4 * SR)
    t = np.arange(n) / SR
    f = freq * (1 + 0.3 * np.exp(-t / 0.01))
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / (0.08 if slap else 0.2))
    x += bandpass(noise(n), 1800 if slap else 900, 1.5) * np.exp(-t / (0.02 if slap else 0.01)) * (0.8 if slap else 0.3)
    return x * vel


def timbale(freq=600, vel=0.7):
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    x = np.sin(2 * np.pi * freq * t) * np.exp(-t / 0.18) + 0.5 * np.sin(2 * np.pi * freq * 1.6 * t) * np.exp(-t / 0.08)
    x += highpass(noise(n), 2500) * np.exp(-t / 0.03) * 0.5
    return x * vel


# ----------------------------------------------------------------------------- mixing

class Track:
    """A stereo buffer you place sounds into by time (seconds)."""

    def __init__(self, dur):
        self.n = int(dur * SR)
        self.buf = np.zeros((self.n + SR * 4, 2))

    def add(self, x, at, gain=1.0, pan=0.0):
        i = int(at * SR)
        if i >= len(self.buf):
            return
        x = np.asarray(x, dtype=np.float64)
        if x.ndim == 1:
            l = math.cos((pan + 1) * math.pi / 4)
            r = math.sin((pan + 1) * math.pi / 4)
            x = np.stack([x * l, x * r], axis=1) * math.sqrt(2)
        if i < 0:
            x = x[-i:]
            i = 0
        end = min(len(self.buf), i + len(x))
        if end > i:
            self.buf[i:end] += x[:end - i] * gain

    def looped(self):
        """Fold everything that rang past the loop end back onto the start: seamless loop."""
        out = self.buf[:self.n].copy()
        tail = self.buf[self.n:]
        k = min(len(tail), self.n)
        out[:k] += tail[:k]
        return out

    def oneshot(self, extra=1.0):
        return self.buf[:self.n + int(extra * SR)].copy()


def reverb_ir(seconds=1.8, predelay=0.012, damp=0.5, seed=7):
    r = np.random.default_rng(seed)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    env = np.exp(-t / (seconds / 6.5))
    ir = np.zeros((n, 2))
    for c in range(2):
        nz = r.uniform(-1, 1, n) * env
        ir[:, c] = lowpass(nz, 9000 - 6000 * damp, 1)
    pd = int(predelay * SR)
    ir = np.concatenate([np.zeros((pd, 2)), ir])
    # a few early reflections
    for k, (d, g) in enumerate(((0.017, 0.5), (0.029, 0.4), (0.041, 0.3), (0.057, 0.22))):
        i = int(d * SR)
        ir[i, k % 2] += g
    return ir / np.sqrt(np.sum(ir ** 2) / 2)


def convolve(x, ir, wet=0.25, loop=False):
    """Stereo FFT convolution. With loop=True the reverb tail wraps around (circular)."""
    n = len(x)
    out = np.zeros_like(x)
    size = 1 << int(math.ceil(math.log2(n + len(ir))))
    for c in range(2):
        X = np.fft.rfft(x[:, c], size)
        H = np.fft.rfft(ir[:, c], size)
        y = np.fft.irfft(X * H, size)
        if loop:
            full = y[:n].copy()
            tail = y[n:n + len(ir)]
            k = min(len(tail), n)
            full[:k] += tail[:k]
            out[:, c] = full
        else:
            out[:, c] = y[:n]
    return x * (1 - wet * 0.5) + out * wet


def normalize(x, peak=0.89):
    m = np.max(np.abs(x))
    return x if m < 1e-9 else x * (peak / m)


def rms_db(x):
    return 20 * math.log10(max(1e-9, float(np.sqrt(np.mean(np.square(x))))))


def soft_clip(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive)


def write_wav(path, x):
    x = np.asarray(x)
    if x.ndim == 1:
        x = x[:, None]
    x = np.clip(x, -1, 1)
    data = (x * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(data.shape[1])
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
