#!/usr/bin/env python3
"""Assembles the feature trailer and the README media from a TrailerReel capture (see Tools/make_trailer.sh).

    python3 Tools/trailer/build.py <capture dir> <out dir>      (out dir: docs/media)

Inputs (written by the game): shots/<name>.mp4 (1080p30, captions baked in), stills/<name>.png, audio.wav (the
game's sound effects from the real-time pass, music muted) and audio_shots.txt (where each shot starts in it).

The edit is cut on the beat of the game's gameplay track ("Going Up", 104 BPM): every clip after the cold open
lasts a whole number of beats. The music bed is mixed here from the game's own stems (bed, melody, trouble, rush,
night) with the same layering the game does live: trouble strings and a tape warble when things go wrong, night
colour for the vampires and the Graveyard Shift, the rush layer for Rush Hour and the montage. The bed ducks under
the sound effects with a sidechain compressor, then the mix is loudness-normalised (EBU R128, two passes).

Everything is ffmpeg; no Python packages beyond the standard library.
"""
import json
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MUSIC = os.path.join(ROOT, "Assets", "Resources", "Audio", "Music")
FONTS = os.path.join(ROOT, "Assets", "Resources", "Fonts")

BPM = 104.0
BEAT = 60.0 / BPM
FPS = 30
RATE = 48000
SIZE_LIMIT_MB = 35.5  # MiB of video + audio; keeps the file comfortably under 40 MB

# ----------------------------------------------------------------------------------------------- the edit
# (shot, in-point in seconds, length in beats (or seconds for the cold open), transition into this clip)
# Transitions: ("fade", s) dissolve, ("fadeblack", s) dip to black, ("cut", 0) = a one-frame dissolve.
CUT = ("fade", 1.0 / FPS)
DIS = ("fade", 0.3)
EDL = [
    # cold open: the trailer moment, no music yet
    dict(shot="cold_ocean", at=0.6, secs=6.8, x=None),
    # title card on the downbeat
    dict(shot="title", at=0.0, beats=8, x=("fadeblack", 0.5)),
    # the core loop and the shuffle
    dict(shot="core", at=0.25, beats=12, x=DIS),
    dict(shot="cards_rise", at=0.15, beats=3, x=DIS),
    dict(shot="cards_sink", at=0.15, beats=3, x=CUT),
    dict(shot="cards_roll", at=0.15, beats=3, x=CUT),
    dict(shot="jam", at=0.2, beats=6, x=DIS),
    dict(shot="tips", at=0.2, beats=6, x=DIS),
    # the guests
    dict(shot="plant", at=0.2, beats=9, x=DIS),
    dict(shot="mirror", at=0.2, beats=7, x=DIS),
    dict(shot="vampire", at=0.2, beats=10, x=DIS),
    dict(shot="courier_jit", at=0.2, beats=5, x=DIS),
    dict(shot="courier_gone", at=0.0, beats=4, x=CUT),
    dict(shot="swimmer", at=0.2, beats=6, x=DIS),
    dict(shot="kid", at=0.2, beats=8, x=DIS),
    dict(shot="tycoon", at=0.2, beats=6, x=DIS),
    # pressure
    dict(shot="patience", at=0.2, beats=6, x=DIS),
    dict(shot="trouble", at=0.2, beats=6, x=DIS),
    # how you play it
    dict(shot="route", at=0.2, beats=8, x=DIS),
    dict(shot="pad", at=0.2, beats=8, x=DIS),
    dict(shot="rush", at=0.2, beats=6, x=DIS),
    # the week
    dict(shot="week", at=0.2, beats=10, x=DIS),
    dict(shot="clockout", at=1.4, beats=10, x=DIS),
    dict(shot="graveyard", at=0.2, beats=6, x=DIS),
    dict(shot="overtime", at=0.2, beats=6, x=DIS),
    # escalation montage: two beats each, hard cuts
    dict(shot="m_flip", at=0.7, beats=2, x=CUT),
    dict(shot="m_poof", at=0.85, beats=2, x=CUT),
    dict(shot="m_ocean", at=0.8, beats=2, x=CUT),
    dict(shot="m_triple", at=0.8, beats=2, x=CUT),
    dict(shot="m_express", at=0.8, beats=2, x=CUT),
    dict(shot="m_depart", at=0.7, beats=2, x=CUT),
    dict(shot="m_fired", at=0.4, beats=4, x=CUT),
    # end card
    dict(shot="end", at=0.0, secs=6.6, x=("fadeblack", 0.6)),
]

# Music layers: stem -> list of (first clip, last clip, gain). Gains are linear, before the bus gain.
LAYERS = {
    "going_up_bed": [("title", "end", 0.9)],
    "going_up_melody": [("title", "tycoon", 0.85), ("patience", "trouble", 0.3), ("route", "overtime", 0.85),
                        ("m_flip", "m_fired", 0.6), ("end", "end", 0.85)],
    "going_up_night": [("vampire", "vampire", 0.7), ("graveyard", "m_fired", 0.75)],
    "going_up_trouble": [("patience", "trouble", 0.9), ("m_flip", "m_fired", 0.7)],
    "going_up_rush": [("rush", "rush", 0.8), ("m_flip", "m_fired", 0.85)],
}
STINGS = [("sting_start", "title", 0.0, 0.9), ("sting_rush", "rush", 0.0, 0.8), ("sting_finale", "end", 0.3, 0.8)]
WARBLE = ("patience", "trouble")  # the tape warble the game applies under pressure
MUSIC_DB = -3.5                   # music bus against the effects
SFX_DB = 0.0
TARGET_LUFS = -15.0


def run(cmd, capture=False):
    print("+", " ".join(cmd if len(" ".join(cmd)) < 400 else cmd[:6] + ["..."]), flush=True)
    r = subprocess.run(cmd, stdout=subprocess.PIPE if capture else None, stderr=subprocess.PIPE if capture else None, text=True)
    if r.returncode != 0:
        if capture:
            sys.stderr.write(r.stderr[-4000:])
        raise SystemExit(f"command failed ({r.returncode})")
    return r


def probe_duration(path):
    r = run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path], capture=True)
    return float(r.stdout.strip())


def read_manifest(path):
    shots = {}
    for line in open(path):
        parts = line.split()
        if not parts:
            continue
        shots[parts[0]] = {k: float(v) for k, v in (p.split("=") for p in parts[1:])}
    return shots


def timeline(clips, shot_len):
    """Start (S), slot length (D) and real length (L, including the overlap into the next clip) per clip."""
    t = 0.0
    out = []
    for i, c in enumerate(clips):
        d = c["secs"] if "secs" in c else c["beats"] * BEAT
        nxt = clips[i + 1]["x"] if i + 1 < len(clips) else None
        over = nxt[1] if nxt else 0.0
        length = d + over
        avail = shot_len[c["shot"]] - c["at"]
        if length > avail + 1e-3:
            print(f"  note: {c['shot']} wants {length:.2f}s from {c['at']:.2f}s but has {avail:.2f}s; holding the last frame")
        out.append(dict(c, S=t, D=d, L=length))
        t += d
    total = out[-1]["S"] + out[-1]["L"]
    return out, total


# ----------------------------------------------------------------------------------------------- video

def build_video(cap, clips, total, out_path):
    args = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"]
    for c in clips:
        args += ["-ss", f"{c['at']:.3f}", "-t", f"{c['L'] + 0.5:.3f}", "-i", os.path.join(cap, "shots", c["shot"] + ".mp4")]
    f = []
    for i, c in enumerate(clips):
        # tpad holds the last frame if a shot is a touch shorter than its slot
        f.append(f"[{i}:v]fps={FPS},format=yuv420p,setsar=1,tpad=stop_mode=clone:stop_duration=2,"
                 f"trim=duration={c['L']:.4f},setpts=PTS-STARTPTS,settb=AVTB[v{i}]")
    prev = "v0"
    for i in range(1, len(clips)):
        kind, dur = clips[i]["x"]
        f.append(f"[{prev}][v{i}]xfade=transition={kind}:duration={dur:.4f}:offset={clips[i]['S']:.4f}[x{i}]")
        prev = f"x{i}"
    f.append(f"[{prev}]fade=t=in:st=0:d=0.6,fade=t=out:st={total - 0.9:.3f}:d=0.9,format=yuv420p[vout]")
    graph = os.path.join(os.path.dirname(out_path), "video_graph.txt")
    open(graph, "w").write(";\n".join(f))
    run(args + ["-/filter_complex", graph, "-map", "[vout]", "-c:v", "libx264", "-preset", "fast", "-crf", "10", "-pix_fmt", "yuv420p", out_path])


# ----------------------------------------------------------------------------------------------- audio

def window_expr(windows):
    """Piecewise gain over time: a sum of trapezoids (gain, start, end, fade in, fade out)."""
    terms = []
    for g, a, b, fi, fo in windows:
        terms.append(f"{g:.3f}*min(clip((t-{a:.3f})/{fi:.3f},0,1),clip(({b:.3f}-t)/{fo:.3f},0,1))")
    return "+".join(terms) if terms else "0"


def build_audio(cap, clips, total, out_wav):
    shots = read_manifest(os.path.join(cap, "audio_shots.txt"))
    by_name = {c["shot"]: c for c in clips}
    t0 = by_name["title"]["S"]
    end_clip = by_name["end"]

    inputs = ["-i", os.path.join(cap, "audio.wav")]
    f = []
    # --- effects bus: each clip's stretch of the real-time recording, placed on the timeline
    n = len(clips)
    f.append(f"[0:a]aresample={RATE},aformat=sample_fmts=fltp:channel_layouts=stereo,asplit={n}" + "".join(f"[s{i}]" for i in range(n)))
    for i, c in enumerate(clips):
        a = shots[c["shot"]]["start"] + c["at"]
        fi = max(0.02, c["x"][1]) if c["x"] else 0.02
        nxt = clips[i + 1]["x"] if i + 1 < n else None
        fo = max(0.02, nxt[1]) if nxt else 0.6
        ms = int(round(c["S"] * 1000))
        f.append(f"[s{i}]atrim=start={a:.4f}:duration={c['L']:.4f},asetpts=PTS-STARTPTS,"
                 f"afade=t=in:d={fi:.3f},afade=t=out:st={max(0.0, c['L'] - fo):.3f}:d={fo:.3f},adelay={ms}|{ms}[c{i}]")
    f.append("".join(f"[c{i}]" for i in range(n)) + f"amix=inputs={n}:normalize=0:dropout_transition=0,"
             f"apad=whole_dur={total:.3f},atrim=duration={total:.3f},volume={SFX_DB}dB[sfx]")

    # --- music bus: stems looped from the title card on, each with its own gain automation
    stems = list(LAYERS.keys())
    k = 1
    ms0 = int(round(t0 * 1000))
    stem_labels = []
    for stem in stems:
        inputs += ["-i", os.path.join(MUSIC, stem + ".wav")]
        wins = []
        for first, last, g in LAYERS[stem]:
            a = by_name[first]["S"]
            b = by_name[last]["S"] + by_name[last]["D"]
            if last == "end":
                b = total
            wins.append((g, a, b, 0.25 if first == "title" else 0.6, 1.2 if last == "end" else 0.6))
        expr = window_expr(wins)
        f.append(f"[{k}:a]aresample={RATE},aformat=sample_fmts=fltp:channel_layouts=stereo,aloop=loop=-1:size={int(round(36.923061 * RATE))},"
                 f"atrim=duration={total - t0 + 1:.3f},adelay={ms0}|{ms0},volume='{expr}':eval=frame[m{k}]")
        stem_labels.append(f"[m{k}]")
        k += 1
    sting_labels = []
    for name, clip, offset, g in STINGS:
        inputs += ["-i", os.path.join(MUSIC, name + ".wav")]
        ms = int(round((by_name[clip]["S"] + offset) * 1000))
        f.append(f"[{k}:a]aresample={RATE},aformat=sample_fmts=fltp:channel_layouts=stereo,volume={g},adelay={ms}|{ms}[st{k}]")
        sting_labels.append(f"[st{k}]")
        k += 1
    nm = len(stem_labels) + len(sting_labels)
    f.append("".join(stem_labels + sting_labels) + f"amix=inputs={nm}:normalize=0:dropout_transition=0,"
             f"apad=whole_dur={total:.3f},atrim=duration={total:.3f},volume={MUSIC_DB}dB[mus0]")
    # the tape warble under pressure (dry/wet crossfade so it eases in and out)
    wa = by_name[WARBLE[0]]["S"]
    wb = by_name[WARBLE[1]]["S"] + by_name[WARBLE[1]]["D"]
    wet = window_expr([(1.0, wa, wb, 0.5, 0.5)])
    f.append(f"[mus0]asplit=2[dry][wetin]")
    f.append(f"[wetin]vibrato=f=0.55:d=0.35,volume='{wet}':eval=frame[wet]")
    f.append(f"[dry]volume='1-({wet})':eval=frame[dryv]")
    f.append(f"[dryv][wet]amix=inputs=2:normalize=0[mus1]")
    # duck the bed under the effects, then the end-card fade
    f.append("[sfx]asplit=3[sfxmix][sfxkey][sfxqc]")
    f.append("[mus1][sfxkey]sidechaincompress=threshold=0.04:ratio=4:attack=8:release=320:makeup=1:knee=4,asplit=2[musd][musqc]")
    f.append(f"[musd][sfxmix]amix=inputs=2:normalize=0,afade=t=out:st={total - 1.6:.3f}:d=1.6[mix]")
    graph = os.path.join(os.path.dirname(out_wav), "audio_graph.txt")
    open(graph, "w").write(";\n".join(f))
    raw = out_wav.replace(".wav", "_raw.wav")
    qc_sfx, qc_mus = out_wav.replace(".wav", "_sfx.wav"), out_wav.replace(".wav", "_music.wav")
    run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"] + inputs + ["-/filter_complex", graph, "-map", "[mix]",
         "-c:a", "pcm_f32le", "-ar", str(RATE), raw, "-map", "[sfxqc]", "-c:a", "pcm_f32le", qc_sfx, "-map", "[musqc]", "-c:a", "pcm_f32le", qc_mus])
    for label, path in (("effects bus", qc_sfx), ("music bus (ducked)", qc_mus)):
        r = run(["ffmpeg", "-hide_banner", "-nostats", "-i", path, "-af", "ebur128", "-f", "null", "-"], capture=True)
        i = re.findall(r"I:\s+(-?[\d.]+) LUFS", r.stderr)
        print(f"  {label}: {i[-1] if i else '?'} LUFS integrated (before normalisation)")
    # two-pass loudness normalisation
    r = run(["ffmpeg", "-hide_banner", "-nostats", "-i", raw, "-af", f"loudnorm=I={TARGET_LUFS}:TP=-1.5:LRA=11:print_format=json", "-f", "null", "-"], capture=True)
    m = json.loads(r.stderr[r.stderr.rindex("{"):r.stderr.rindex("}") + 1])
    run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", raw, "-af",
         f"loudnorm=I={TARGET_LUFS}:TP=-1.5:LRA=11:measured_I={m['input_i']}:measured_TP={m['input_tp']}:measured_LRA={m['input_lra']}:"
         f"measured_thresh={m['input_thresh']}:offset={m['target_offset']}:linear=true,aresample={RATE}",
         "-c:a", "pcm_s16le", out_wav])
    print(f"  music starts at {t0:.2f}s; input loudness {m['input_i']} LUFS -> {TARGET_LUFS}")


# ----------------------------------------------------------------------------------------------- encode

def encode(video, audio, total, out_mp4, work):
    kbps = int((SIZE_LIMIT_MB * 8 * 1024 * 1024 / total - 192000) / 1000)
    kbps = min(kbps, 6000)
    log = os.path.join(work, "x264pass")
    common = ["-c:v", "libx264", "-preset", "slow", "-tune", "animation", "-b:v", f"{kbps}k", "-maxrate", f"{int(kbps * 1.8)}k",
              "-bufsize", f"{kbps * 3}k", "-pix_fmt", "yuv420p", "-r", str(FPS), "-g", str(FPS * 4), "-passlogfile", log]
    run(["nice", "-n", "10", "ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", video] + common + ["-pass", "1", "-an", "-f", "null", "-"])
    run(["nice", "-n", "10", "ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", video, "-i", audio] + common +
        ["-pass", "2", "-c:a", "aac", "-b:a", "192k", "-ar", str(RATE), "-map", "0:v", "-map", "1:a", "-shortest",
         "-movflags", "+faststart", "-metadata", "title=One More Floor - feature trailer", out_mp4])
    print(f"  video {kbps} kb/s -> {os.path.getsize(out_mp4) / 1048576:.1f} MB")


# ----------------------------------------------------------------------------------------------- README media

def play_button(path, cx=1280, cy=720):
    """A brass-ringed play button on a transparent 1280x720 canvas (drawn at 2x and scaled down; centre in 2x pixels)."""
    r = 150
    ring = f"between(hypot(X-{cx},Y-{cy}),{r - 12},{r})"
    fill = f"lt(hypot(X-{cx},Y-{cy}),{r - 12})"
    tri = f"gte(X,{cx - 44})*lte(abs(Y-{cy}),({cx + 76}-X)*68/120)"
    geq = (f"r='if({tri},255,if({ring},242,if({fill},24,0)))':g='if({tri},240,if({ring},198,if({fill},14,0)))':"
           f"b='if({tri},208,if({ring},107,if({fill},30,0)))':a='if({tri},255,if({ring},255,if({fill},165,0)))'")
    run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "color=c=black@0:s=2560x1440,format=rgba",
         "-vf", f"geq={geq},scale=1280:720:flags=lanczos", "-frames:v", "1", path])


def poster(trailer, at, out_jpg, work, length):
    # the title card has the logo on the left and the tower on the right: put the button on the tower
    bx, by = 878, 330
    btn = os.path.join(work, "play.png")
    play_button(btn, bx * 2, by * 2)
    font = os.path.join(FONTS, "Bungee-Regular.ttf")
    run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-ss", f"{at:.2f}", "-i", trailer, "-i", btn, "-filter_complex",
         f"[0:v]scale=1280:720:flags=lanczos,format=rgba[bg];[bg][1:v]overlay=0:0,"
         f"drawbox=x={bx - 150}:y={by + 92}:w=300:h=46:color=0x180E1E@0.82:t=fill,"
         f"drawbox=x={bx - 150}:y={by + 92}:w=300:h=46:color=0xF2C66B@0.9:t=2,"
         f"drawtext=fontfile='{font}':text='WATCH THE TRAILER':fontsize=24:fontcolor=0xF2C66B:x={bx}-tw/2:y={by + 104},format=yuv420p",
         "-frames:v", "1", "-q:v", "3", out_jpg])


def teaser(cap, out_gif, work):
    """An 8-second loop for the top of the README: the ocean, a flip, bats, a triple drop."""
    parts = [("cold_ocean", 2.15, 2.9), ("m_flip", 0.75, 1.7), ("m_poof", 0.9, 1.6), ("m_triple", 0.85, 1.6)]
    args = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"]
    for shot, at, dur in parts:
        args += ["-ss", f"{at}", "-t", f"{dur + 0.4}", "-i", os.path.join(cap, "shots", shot + ".mp4")]
    f, t = [], 0.0
    for i, (_, _, dur) in enumerate(parts):
        f.append(f"[{i}:v]fps=14,scale=720:-2:flags=lanczos,setsar=1,trim=duration={dur + 0.3},setpts=PTS-STARTPTS,settb=AVTB[p{i}]")
    prev = "p0"
    t = parts[0][2]
    for i in range(1, len(parts)):
        f.append(f"[{prev}][p{i}]xfade=transition=fade:duration=0.3:offset={t:.3f}[q{i}]")
        prev = f"q{i}"
        t += parts[i][2]
    # loop seam: crossfade the end back into the opening frames
    f.append(f"[{prev}]split=2[body][head]")
    f.append(f"[head]trim=duration=0.3,setpts=PTS-STARTPTS[h]")
    f.append(f"[body]trim=start=0.3,setpts=PTS-STARTPTS[b]")
    f.append(f"[b][h]xfade=transition=fade:duration=0.3:offset={t - 0.3:.3f},split[s0][s1]")
    f.append("[s0]palettegen=max_colors=144:stats_mode=diff[pal]")
    f.append("[s1][pal]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle")
    run(args + ["-filter_complex", ";".join(f), "-loop", "0", out_gif])
    print(f"  teaser {os.path.getsize(out_gif) / 1048576:.1f} MB")


SCREENSHOTS = ["title", "core_shuffle", "ocean_moment", "triple_drop", "vampire_poof", "houseplant_sun", "graveyard_flip",
               "route_preview", "gamepad_closeup", "roster"]


def screenshots(cap, out_dir):
    if os.path.isdir(out_dir):
        shutil.rmtree(out_dir)
    os.makedirs(out_dir)
    for name in SCREENSHOTS:
        src = os.path.join(cap, "stills", name + ".png")
        if not os.path.exists(src):
            print(f"  missing still {name}")
            continue
        dst = os.path.join(out_dir, name.replace("_", "-") + ".jpg")
        run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", src, "-q:v", "2", dst])
        print(f"  {os.path.basename(dst)} {os.path.getsize(dst) / 1024:.0f} KB")


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    cap, out = sys.argv[1], sys.argv[2]
    what = set(sys.argv[3:]) or {"trailer", "media"}
    if what == {"screenshots"}:
        # README stills only: no shot manifest or audio needed
        screenshots(cap, os.path.join(out, "screenshots"))
        return
    work = os.path.join(cap, "build")
    os.makedirs(work, exist_ok=True)
    os.makedirs(out, exist_ok=True)
    shot_len = {k: v["seconds"] for k, v in read_manifest(os.path.join(cap, "video_shots.txt")).items()}
    clips = [c for c in EDL if c["shot"] in shot_len]
    missing = [c["shot"] for c in EDL if c["shot"] not in shot_len]
    if missing:
        print("  shots missing from the capture (left out):", ", ".join(missing))
    clips, total = timeline(clips, shot_len)
    with open(os.path.join(work, "timeline.txt"), "w") as fh:
        for c in clips:
            fh.write(f"{c['S']:7.2f}  {c['D']:5.2f}  {c['shot']}\n")
    print(f"  {len(clips)} clips, {total:.1f}s")
    trailer = os.path.join(out, "trailer.mp4")
    if "trailer" in what:
        video = os.path.join(work, "video.mkv")
        audio = os.path.join(work, "audio.wav")
        build_video(cap, clips, total, video)
        build_audio(cap, clips, total, audio)
        encode(video, audio, total, trailer, work)
    if "media" in what:
        title = next(c for c in clips if c["shot"] == "title")
        poster(trailer, title["S"] + 3.6, os.path.join(out, "trailer-poster.jpg"), work, probe_duration(trailer))
        teaser(cap, os.path.join(out, "teaser.gif"), work)
        screenshots(cap, os.path.join(out, "screenshots"))


if __name__ == "__main__":
    main()
