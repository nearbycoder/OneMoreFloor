#!/usr/bin/env python3
"""Tile captured frames into one contact sheet with ffmpeg:  python3 Tools/sheet.py <dir> <out.png> [cols]"""
import glob, subprocess, sys, math
d, out = sys.argv[1], sys.argv[2]
cols = int(sys.argv[3]) if len(sys.argv) > 3 else 4
frames = sorted(glob.glob(f"{d}/f*.png"))
rows = math.ceil(len(frames) / cols)
subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "1", "-pattern_type", "glob", "-i", f"{d}/f*.png",
                "-vf", f"scale=640:-1,tile={cols}x{rows}:padding=4:color=white", "-frames:v", "1", out], check=True)
print(out, len(frames), "frames")
