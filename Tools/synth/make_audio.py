"""Regenerates every sound in the game. Run with Blender's bundled Python (it has numpy):
    blender -b --python-exit-code 1 -P Tools/synth/make_audio.py -- [sfx] [music]
Writes Assets/Resources/Audio/{Sfx,Music}/*.wav and prints a level report."""
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

import make_music  # noqa: E402
import make_sfx  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
what = set(argv) or {"sfx", "music"}
t0 = time.time()
if "sfx" in what:
    for line in make_sfx.main(os.path.join(ROOT, "Assets", "Resources", "Audio", "Sfx")):
        print("[sfx]", line)
if "music" in what:
    for line in make_music.main(os.path.join(ROOT, "Assets", "Resources", "Audio", "Music")):
        print("[music]", line)
print(f"done in {time.time() - t0:.1f}s")
