"""Objective audio audit for every sound in the game (no ears required, but no substitute for them).

    blender -b --python-exit-code 1 -P Tools/synth/audit.py -- [--mix capture.wav] [--json out.json]

Per file: loudness (ITU-R BS.1770 K-weighting; integrated for music, max-momentary for effects), true peak
(4x oversampled), DC offset, leading silence (latency), clicks at the start/end, loop seams, spectral balance
and stereo correlation. Then:
  - effects are checked *as the game plays them*: every Sfx/SfxLater/Voice call in the C# is parsed and its
    volume applied, and each call is compared with its category's median (UI, voices, gameplay, ...);
  - --mix analyses a recording of the final in-game mix (made with -omfRecordAudio).
Prints FAIL/WARN lines and exits non-zero on FAILs.
"""
import glob
import json
import math
import os
import re
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
AUDIO = os.path.join(ROOT, "Assets", "Resources", "Audio")
LOOPS = ("going_up_", "lobby_lounge", "amb_", "motor_loop")


def read(path):
    with wave.open(path) as w:
        sr, ch, width, n = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
        raw = w.readframes(n)
    data = np.frombuffer(raw, dtype=np.int16 if width == 2 else np.int32).astype(np.float64)
    data /= 32768.0 if width == 2 else 2147483648.0
    return sr, data.reshape(-1, ch).T


# ----------------------------------------------------------------------------- loudness

def _biquad_response(b, a, sr, n):
    w = np.linspace(0, math.pi, n // 2 + 1)
    z = np.exp(-1j * w)
    return (b[0] + b[1] * z + b[2] * z * z) / (a[0] + a[1] * z + a[2] * z * z)


def _k_weight_power(sr, n):
    """|H|^2 of the BS.1770 K-weighting (shelf + high-pass) at rfft bins, pyloudnorm's parameters."""
    def shelf(G, Q, fc):
        A = 10 ** (G / 40)
        w0 = 2 * math.pi * fc / sr
        alpha = math.sin(w0) / (2 * Q)
        c = math.cos(w0)
        b = [A * ((A + 1) + (A - 1) * c + 2 * math.sqrt(A) * alpha), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - 2 * math.sqrt(A) * alpha)]
        a = [(A + 1) - (A - 1) * c + 2 * math.sqrt(A) * alpha, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - 2 * math.sqrt(A) * alpha]
        return b, a

    def highpass(Q, fc):
        w0 = 2 * math.pi * fc / sr
        alpha = math.sin(w0) / (2 * Q)
        c = math.cos(w0)
        return [(1 + c) / 2, -(1 + c), (1 + c) / 2], [1 + alpha, -2 * c, 1 - alpha]

    h = _biquad_response(*shelf(3.99984385397, 0.7071752369554193, 1681.9744509555319), sr, n)
    h *= _biquad_response(*highpass(0.5003270373253953, 38.13547087613982), sr, n)
    return np.abs(h) ** 2


def block_loudness(chans, sr, block=0.4, hop=0.1):
    """Momentary loudness (LUFS) of each 400 ms block, K-weighted in the frequency domain."""
    n = int(sr * block)
    step = int(sr * hop)
    length = chans.shape[1]
    if length < n:
        chans = np.pad(chans, ((0, 0), (0, n - length)))
        length = n
    kw = _k_weight_power(sr, n)
    out = []
    for start in range(0, length - n + 1, step):
        power = 0.0
        for c in chans:
            X = np.fft.rfft(c[start:start + n])
            p = np.abs(X) ** 2 * kw
            ms = (p[0] + 2 * p[1:-1].sum() + p[-1]) / (n * n)
            power += ms
        out.append(-0.691 + 10 * math.log10(max(power, 1e-12)))
    return np.array(out)


def integrated(blocks):
    b = blocks[blocks > -70]
    if len(b) == 0:
        return -70.0
    rel = 10 * math.log10(np.mean(10 ** (b / 10))) - 10
    b = b[b > rel]
    return 10 * math.log10(np.mean(10 ** (b / 10))) if len(b) else -70.0


def true_peak(chans):
    peak = 0.0
    for c in chans:
        n = len(c)
        X = np.fft.rfft(c)
        up = np.fft.irfft(np.concatenate([X, np.zeros(len(X) * 3 - 3)]), n * 4) * 4
        peak = max(peak, float(np.max(np.abs(up))))
    return 20 * math.log10(max(peak, 1e-9))


def spectrum_bands(chans, sr):
    mono = chans.mean(axis=0)
    n = 4096
    if len(mono) < n:
        mono = np.pad(mono, (0, n - len(mono)))
    win = np.hanning(n)
    acc = np.zeros(n // 2 + 1)
    for s in range(0, len(mono) - n + 1, n // 2):
        acc += np.abs(np.fft.rfft(mono[s:s + n] * win)) ** 2
    f = np.fft.rfftfreq(n, 1 / sr)
    total = acc.sum() + 1e-18
    edges = [(0, 60, "sub"), (60, 250, "low"), (250, 1000, "lowmid"), (1000, 2500, "mid"), (2500, 6000, "presence"), (6000, sr / 2, "air")]
    bands = {name: float(acc[(f >= lo) & (f < hi)].sum() / total) for lo, hi, name in edges}
    centroid = float((f * acc).sum() / total)
    return bands, centroid


# ----------------------------------------------------------------------------- per file

def analyse(path):
    sr, ch = read(path)
    name = os.path.splitext(os.path.basename(path))[0]
    mono = ch.mean(axis=0)
    blocks = block_loudness(ch, sr)
    peak_env = np.max(np.abs(mono)) + 1e-12
    r = {"name": name, "seconds": len(mono) / sr, "channels": ch.shape[0]}
    r["integrated"] = integrated(blocks)
    r["max_momentary"] = float(blocks.max())
    r["true_peak"] = true_peak(ch)
    r["dc"] = float(abs(mono.mean()))
    above = np.nonzero(np.abs(mono) > 10 ** (-40 / 20))[0]
    r["lead_ms"] = float(above[0] / sr * 1000) if len(above) else 0.0
    loop = name.startswith(LOOPS)
    r["loop"] = loop
    d = np.abs(np.diff(mono))
    typical = float(np.median(d[d > 0])) if np.any(d > 0) else 1e-9
    if loop:
        seam = abs(mono[0] - mono[-1])
        slope = abs((mono[0] - mono[-1]) - (mono[-1] - mono[-2]))
        r["seam"] = float(max(seam, slope) / max(typical, 1e-9))
    else:
        r["start_jump"] = float(abs(mono[0]))
        r["end_jump"] = float(abs(mono[-1]))
    r["bands"], r["centroid"] = spectrum_bands(ch, sr)
    if ch.shape[0] == 2:
        l, rr = ch
        r["correlation"] = float(np.corrcoef(l, rr)[0, 1]) if np.std(l) > 0 and np.std(rr) > 0 else 1.0
    return r


def category(name):
    if name.startswith("ui_"):
        return "ui"
    if name.startswith("voice_"):
        return "voice"
    if name.startswith("streak_"):
        return "streak"
    if name.startswith(("star_", "fanfare_", "new_record")):
        return "reward"
    if name.startswith(("amb_", "motor_loop")):
        return "ambience"
    return "gameplay"


# ----------------------------------------------------------------------------- in-game levels

def _args(src, start):
    """Split the argument list that starts after an opening parenthesis at `start` (depth-aware)."""
    depth, cur, out = 0, "", []
    for ch in src[start:]:
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            if depth == 0:
                out.append(cur.strip())
                return out
            depth -= 1
        if ch == "," and depth == 0:
            out.append(cur.strip())
            cur = ""
            continue
        cur += ch
    return out


def _vol(expr, default):
    """Largest float literal in a volume expression (handles `a ? 0.6f : 0.3f`)."""
    if not expr:
        return default
    vals = [float(v) for v in re.findall(r"(\d+\.\d+|\d+)f", expr)]
    return max(vals) if vals else default


def call_sites():
    """Every Sfx("x", vol) / SfxLater("x", delay, vol) / Voice(kind, pan, vol) in the game code."""
    calls = []
    for path in glob.glob(os.path.join(ROOT, "Assets", "Scripts", "Game", "**", "*.cs"), recursive=True):
        src = open(path).read()
        rel = os.path.relpath(path, ROOT)
        for m in re.finditer(r"\b(Sfx|SfxLater|Voice)\(", src):
            if src[max(0, m.start() - 12):m.start()].rstrip().endswith("void"):
                continue
            a = _args(src, m.end())
            if not a:
                continue
            if m.group(1) == "Voice":
                calls.append(("voice_*", _vol(a[2] if len(a) > 2 else None, 0.55), rel))
                continue
            name = a[0]
            vol = _vol(a[2] if m.group(1) == "SfxLater" and len(a) > 2 else a[1] if m.group(1) == "Sfx" and len(a) > 1 else None, 1.0)
            lit = re.fullmatch(r'"(\w+)"', name)
            if lit:
                calls.append((lit.group(1), vol, rel))
            elif name.startswith('"'):
                prefix = re.match(r'"(\w+)"', name)
                if prefix:
                    calls.append((prefix.group(1) + "*", vol, rel))
    return calls


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    mix = argv[argv.index("--mix") + 1] if "--mix" in argv else None
    out_json = argv[argv.index("--json") + 1] if "--json" in argv else None
    files = sorted(glob.glob(os.path.join(AUDIO, "Sfx", "*.wav")) + glob.glob(os.path.join(AUDIO, "Music", "*.wav")))
    results = {}
    fails, warns = [], []
    for f in files:
        r = analyse(f)
        results[r["name"]] = r
        n = r["name"]
        if r["true_peak"] > -0.3:
            fails.append(f"{n}: true peak {r['true_peak']:+.1f} dBTP (inter-sample clipping)")
        elif r["true_peak"] > -1.0:
            warns.append(f"{n}: true peak {r['true_peak']:+.1f} dBTP (little headroom)")
        if r["dc"] > 0.003:
            warns.append(f"{n}: DC offset {r['dc']:.4f}")
        if not r["loop"]:
            if r["lead_ms"] > 15 and category(n) in ("ui", "gameplay"):
                warns.append(f"{n}: {r['lead_ms']:.0f} ms of silence before the sound starts (feels laggy)")
            if r["start_jump"] > 0.02:
                fails.append(f"{n}: starts on a non-zero sample ({r['start_jump']:.3f}): click")
            if r["end_jump"] > 0.02:
                fails.append(f"{n}: ends on a non-zero sample ({r['end_jump']:.3f}): click")
        elif r["seam"] > 12:
            fails.append(f"{n}: loop seam jump {r['seam']:.0f}x a typical sample step: click on every loop")
        if r["bands"]["sub"] > 0.25 and category(n) != "ambience":
            warns.append(f"{n}: {100 * r['bands']['sub']:.0f}% of energy below 60 Hz (rumble small speakers can't play)")
        if category(n) == "ui" and r["bands"]["presence"] + r["bands"]["air"] > 0.6:
            warns.append(f"{n}: UI sound with {100 * (r['bands']['presence'] + r['bands']['air']):.0f}% above 2.5 kHz (harsh when repeated)")
        if r.get("correlation", 1.0) < 0.2 and not n.startswith("amb_"):
            warns.append(f"{n}: stereo correlation {r['correlation']:.2f} (thin or phasey in mono)")

    # music stems: integrated loudness and how the layers stack
    stems = {k: v for k, v in results.items() if k.startswith(("going_up_", "lobby_"))}
    # effects as played: loudness + call volume, compared within each category
    sites = call_sites()
    played = {}
    for name, vol, rel in sites:
        targets = [k for k in results if (name.endswith("*") and k.startswith(name[:-1])) or k == name]
        for t in targets:
            lv = results[t]["max_momentary"] + 20 * math.log10(max(vol, 1e-4))
            played.setdefault(category(t), []).append((lv, t, vol, rel))
    by_cat = {}
    for cat, items in played.items():
        med = float(np.median([x[0] for x in items]))
        by_cat[cat] = med
        for lv, t, vol, rel in items:
            if abs(lv - med) > 9 and cat != "ambience":
                warns.append(f"{t} played at {vol:g} ({rel}): {lv - med:+.1f} LU against the {cat} median: may stick out / get lost")

    print(f"{'file':<22}{'sec':>6}{'LUFS':>8}{'maxM':>7}{'dBTP':>7}{'lead':>6}  bands sub/low/lmid/mid/pres/air   centroid")
    for n, r in results.items():
        b = r["bands"]
        print(f"{n:<22}{r['seconds']:6.2f}{r['integrated']:8.1f}{r['max_momentary']:7.1f}{r['true_peak']:7.1f}{r['lead_ms']:6.0f}  "
              f"{b['sub']:.2f}/{b['low']:.2f}/{b['lowmid']:.2f}/{b['mid']:.2f}/{b['presence']:.2f}/{b['air']:.2f}   {r['centroid']:6.0f} Hz")
    print("\nmusic stems (integrated LUFS): " + "  ".join(f"{k} {v['integrated']:.1f}" for k, v in stems.items()))
    print("as played, median max-momentary by category: " + "  ".join(f"{k} {v:.1f}" for k, v in sorted(by_cat.items())))

    if mix:
        sr, ch = read(mix)
        blocks = block_loudness(ch, sr)
        loud = integrated(blocks)
        tp = true_peak(ch)
        short = np.array([np.mean(10 ** (blocks[i:i + 30] / 10)) for i in range(0, max(1, len(blocks) - 30), 5)])
        short_db = 10 * np.log10(np.maximum(short, 1e-12))
        lra = float(np.percentile(short_db[short_db > loud - 20], 95) - np.percentile(short_db[short_db > loud - 20], 10)) if len(short_db) else 0
        bands, centroid = spectrum_bands(ch, sr)
        print(f"\nin-game mix {os.path.basename(mix)}: {ch.shape[1] / sr:.0f}s  integrated {loud:.1f} LUFS  true peak {tp:+.1f} dBTP  "
              f"loudness range {lra:.1f} LU  centroid {centroid:.0f} Hz  bands " + "/".join(f"{v:.2f}" for v in bands.values()))
        if tp > -0.5:
            fails.append(f"mix: true peak {tp:+.1f} dBTP")
        if loud < -26 or loud > -12:
            warns.append(f"mix: integrated {loud:.1f} LUFS is outside the usual -26..-12 for games")
        if lra > 15:
            warns.append(f"mix: loudness range {lra:.1f} LU (quiet parts may get lost)")

    print()
    for w in warns:
        print("WARN", w)
    for f in fails:
        print("FAIL", f)
    print(f"\n{len(results)} files, {len(fails)} fails, {len(warns)} warnings")
    if out_json:
        with open(out_json, "w") as fo:
            json.dump(results, fo, indent=1)
    sys.exit(1 if fails else 0)


main()
