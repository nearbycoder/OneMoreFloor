#!/usr/bin/env python3
"""Summarise real playtests from the game's local log and check them against the balance model.

    Tools/playtest_report.py [playtests.jsonl ...]

Default log: ~/.config/unity3d/Nearby/One More Floor/playtests.jsonl (written by Telemetry.cs, one line per
shift finished, restarted or quit). Collect the files from several testers and pass them all.

For each shift it prints attempts, how they ended, scores against the current star thresholds, what the
complaints were about, and suggested thresholds from the people who actually played:
    1 star  ~ the median of first and second attempts (half of players unlock the next shift in a try or two)
    2 stars ~ the 60th percentile of all finished runs
    3 stars ~ the 90th percentile of all finished runs
It also compares measured reaction times with what Bot.Human assumes, so the model can be recalibrated.
"""
import json
import math
import os
import re
import statistics
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DEFAULT = os.path.expanduser("~/.config/unity3d/Nearby/One More Floor/playtests.jsonl")


def thresholds():
    """Current star thresholds, read from ShiftCatalog.cs."""
    src = open(os.path.join(ROOT, "Assets/Scripts/Core/ShiftCatalog.cs")).read()
    out = {}
    for m in re.finditer(r'Id = "(\w+)".*?Stars = new\[\] \{ ([\d, ]+) \}', src, re.S):
        out[m.group(1)] = [int(x) for x in m.group(2).split(",")]
    return out


def fitts(d, w):
    return 0.1 + 0.15 * math.log2(d / w + 1.0)


def model(skill):
    """Bot.Human's timing assumptions (seconds) for a skill level 0..1."""
    lerp = lambda a, b: a + (b - a) * skill
    reaction, motor = lerp(1.25, 0.5), lerp(1.3, 0.8)
    to_guest = fitts(300, 55)
    to_button = 0.5 * (fitts(700, 70) + fitts(400, 100))
    return {"first action at a stop": reaction * 0.5 + motor * to_guest,
            "gap between boards": reaction * 0.35 + motor * to_guest,
            "gap before a send": reaction + motor * to_button}


def pct(values, q):
    v = sorted(values)
    if not v:
        return 0
    k = (len(v) - 1) * q
    lo, hi = math.floor(k), math.ceil(k)
    return v[lo] + (v[hi] - v[lo]) * (k - lo)


def round500(x):
    return int(round(x / 500.0) * 500)


def main():
    paths = sys.argv[1:] or [DEFAULT]
    rows = []
    for p in paths:
        if not os.path.exists(p):
            print(f"no log at {p}")
            continue
        with open(p) as f:
            rows += [json.loads(line) for line in f if line.strip()]
    if not rows:
        print("nothing to report yet: play a few shifts first")
        return 1
    stars = thresholds()
    order = list(stars)
    print(f"{len(rows)} shifts logged from {len(paths)} file(s)\n")
    for shift in sorted({r["shift"] for r in rows}, key=lambda s: order.index(s) if s in order else 99):
        rs = [r for r in rows if r["shift"] == shift]
        done = [r for r in rs if r["end"] == "finished"]
        cur = stars.get(shift, [0, 0, 0])
        print(f"== {shift}  ({len(rs)} attempts: {len(done)} finished, "
              f"{sum(r['end'] == 'restarted' for r in rs)} restarted, {sum(r['end'] == 'quit' for r in rs)} quit)")
        if not done:
            print()
            continue
        scores = [r["score"] for r in done]
        fired = sum(r["fired"] for r in done)
        print(f"   score median {statistics.median(scores):.0f}  best {max(scores)}   fired {fired}/{len(done)}"
              f"   stars now {cur}: " + " ".join(f"{k}*={sum(s >= cur[k - 1] for s in scores)}/{len(done)}" for k in (1, 2, 3)))
        causes = {k: sum(r.get(k, 0) for r in done) / len(done) for k in ("stormedOff", "fumed", "poofed", "packageLost", "sweptAway")}
        print("   complaints per run: " + "  ".join(f"{k} {v:.1f}" for k, v in causes.items() if v > 0))
        early = [r["score"] for r in done if r.get("plays", 1) <= 2] or scores
        one = round500(statistics.median(early))
        two = round500(max(pct(scores, 0.6), one + 1500))
        three = round500(max(pct(scores, 0.9), two + 1500))
        if len(done) >= 3:
            print(f"   suggested Stars = {{ {one}, {two}, {three} }}")
        else:
            print("   (fewer than 3 finished runs: no threshold suggestion yet)")
        if fired / len(done) > 0.3:
            print("   ! more than 30% of runs ended fired: consider slower spawns (SpawnStart/SpawnEnd) or more patience")
        print()

    firsts = [r["firstActionMean"] for r in rows if r.get("firstActionSamples", 0) >= 3]
    gaps = [r["actionGapMean"] for r in rows if r.get("actionGapSamples", 0) >= 3]
    print("== reaction times (real vs Bot.Human)")
    if firsts:
        print(f"   first action after the doors open: median {statistics.median(firsts):.2f}s over {len(firsts)} shifts")
    if gaps:
        print(f"   gap between consecutive actions:   median {statistics.median(gaps):.2f}s over {len(gaps)} shifts")
    for name, skill in (("new", 0.15), ("average", 0.5), ("practised", 0.85)):
        m = model(skill)
        print(f"   model {name:<9} " + "  ".join(f"{k} {v:.2f}s" for k, v in m.items()))
    dev = {}
    for r in rows:
        for k in ("mouseActions", "padActions", "keyActions"):
            dev[k] = dev.get(k, 0) + r.get(k, 0)
    total = sum(dev.values()) or 1
    print("\n== input: " + "  ".join(f"{k[:-7]} {100 * v / total:.0f}%" for k, v in dev.items()))
    zoom = [r.get("zoomMean", 0) for r in rows]
    print(f"== camera: mean zoom {statistics.mean(zoom):.2f} (0 = whole tower, 1 = close-up)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
