"""The car, the shaft, the roof, the foundation, street furniture and small FX meshes.

    blender -b -P ArtSource/build_props.py -- [--only Car,Roof] [--preview DIR] [--no-export]

Unity space. Sizes must match Layout in Assets/Scripts/Game/Util.cs.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from omf_lib import *  # noqa: E402,F401,F403

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models")

SLOT = 2.8
W, H, D = 3.5, 2.4, 2.5          # car outer size
HW = 7.6                          # floor half width
S = 1.9                           # shaft half width
FRONT, BACK = -2.1, 3.0
BRICK = "7A2F38"


def car(root):
    m = Model("Body")
    hw, hd = W / 2, D / 2
    carpet = col("8E2433", 0.12)
    wood = col("7A4A2E", 0.4)
    panel = col("9A6440", 0.45)
    # floor with a patterned carpet and brass sill
    m.add(box((-hw, -0.14, -hd), (hw, 0.0, hd)), col("3A2A24", 0.3))
    m.add(box((-hw + 0.1, 0.0, -hd + 0.1), (hw - 0.1, 0.02, hd - 0.1)), carpet)
    m.add(box((-hw + 0.35, 0.02, -hd + 0.35), (hw - 0.35, 0.025, hd - 0.35)), col("D7A84A", 0.3))
    m.add(box((-hw + 0.42, 0.025, -hd + 0.42), (hw - 0.42, 0.03, hd - 0.42)), carpet)
    m.add(box((-hw - 0.06, -0.18, -hd - 0.08), (hw + 0.06, 0.03, -hd + 0.06), 0.02), BRASS)
    # back wall: wood panels with brass inlays, two sconces
    m.add(box((-hw, 0, hd - 0.1), (hw, H, hd)), wood)
    for k in range(3):
        x0 = -hw + 0.18 + k * (W - 0.36) / 3
        x1 = x0 + (W - 0.36) / 3 - 0.14
        m.add(box((x0, 0.25, hd - 0.13), (x1, 0.95, hd - 0.1), 0.02), panel)
        m.add(box((x0, 1.1, hd - 0.13), (x1, H - 0.25, hd - 0.1), 0.02), panel)
        m.add(box((x0 - 0.02, 1.0, hd - 0.14), (x1 + 0.02, 1.04, hd - 0.1)), BRASS)
    for x in (-0.95, 0.95):
        m.add(box((x - 0.08, 1.55, hd - 0.16), (x + 0.08, 1.75, hd - 0.1), 0.02), BRASS)
        m.add(lathe([(0.0, 0), (0.12, 0.0), (0.08, 0.16), (0.0, 0.16)], (x, 1.72, hd - 0.24)), gls("FFF2D6", 0.7))
    # side walls with handrails
    for s in (-1, 1):
        x0, x1 = sorted((s * hw, s * (hw - 0.1)))
        m.add(box((x0, 0, -hd), (x1, H, hd)), wood)
        xr = s * (hw - 0.18)
        m.add(rod((xr, 0.95, -hd + 0.25), (xr, 0.95, hd - 0.2), 0.035), BRASS)
        for z in (-hd + 0.3, hd - 0.3):
            m.add(rod((xr, 0.95, z), (s * (hw - 0.1), 0.95, z), 0.02), BRASS)
    # operator panel on the left wall, by the bellhop's corner
    m.add(box((-hw + 0.1, 1.0, 0.2), (-hw + 0.14, 1.6, 0.6), 0.02), BRASS)
    for r in range(3):
        for c in range(2):
            m.add(cyl((-hw + 0.15, 1.15 + r * 0.16, 0.32 + c * 0.16), 0.045, 0.03, "x"), col("F3E6C8", 0.4))
    # ceiling with a round light
    m.add(box((-hw, H - 0.08, -hd), (hw, H, hd)), col("F3E6C8", 0.3))
    m.add(cyl((0, H - 0.1, 0.1), 0.42, 0.04, "y"), BRASS)
    m.add(cyl((0, H - 0.13, 0.1), 0.36, 0.04, "y"), glo("FFF0CC", 2.5))
    # brass crown on the roof, front header with a little floor indicator dial
    m.add(box((-hw - 0.08, H, -hd - 0.08), (hw + 0.08, H + 0.12, hd + 0.08), 0.03), BRASS)
    m.add(box((-hw + 0.2, H + 0.12, -hd + 0.2), (hw - 0.2, H + 0.2, hd - 0.2), 0.03), BRASS_DARK)
    m.add(cyl((0, H + 0.32, 0.1), 0.18, 0.24, "y"), BRASS_DARK)
    m.add(torus((0, H + 0.48, 0.1), 0.12, 0.03, "x"), BRASS_DARK)
    m.add(box((-0.7, H - 0.02, -hd - 0.12), (0.7, H + 0.36, -hd - 0.06), 0.03), BRASS)
    m.add(cyl((0, H + 0.12, -hd - 0.13), 0.22, 0.03, "z", seg=24), col("F3E6C8", 0.5))
    for k in range(7):
        a = math.radians(15 + k * 25)
        m.add(rod((0, H + 0.12, -hd - 0.15), (0.2 * math.cos(a), H + 0.12 + 0.2 * math.sin(a), -hd - 0.15), 0.006), INK)
    # side posts framing the gates
    for s in (-1, 1):
        x0, x1 = sorted((s * hw, s * (hw - 0.14)))
        m.add(box((x0, 0, -hd - 0.08), (x1, H, -hd + 0.06), 0.02), BRASS)
    m.build(parent=root)
    # accordion gates, pivot at the outer edge; Unity scales x to fold them
    for name, s in (("DoorL", -1), ("DoorR", 1)):
        g = Model(name)
        pivot_x = s * (hw - 0.14)
        span = hw - 0.14
        bars = 7
        for i in range(bars + 1):
            x = pivot_x - s * span * i / bars
            g.add(box((x - 0.022, 0.06, -hd - 0.04), (x + 0.022, H - 0.06, -hd + 0.0)), BRASS)
        for i in range(bars):
            xa = pivot_x - s * span * i / bars
            xb = pivot_x - s * span * (i + 1) / bars
            for k in range(6):
                y0 = 0.12 + k * 0.36
                g.add(rod((xa, y0, -hd - 0.02), (xb, y0 + 0.36, -hd - 0.02), 0.012, seg=6), BRASS)
                g.add(rod((xa, y0 + 0.36, -hd - 0.02), (xb, y0, -hd - 0.02), 0.012, seg=6), BRASS)
        g.add(box((min(pivot_x, pivot_x - s * span), H - 0.1, -hd - 0.05), (max(pivot_x, pivot_x - s * span), H - 0.04, -hd + 0.01)), BRASS)
        g.add(box((min(pivot_x, pivot_x - s * span), 0.04, -hd - 0.05), (max(pivot_x, pivot_x - s * span), 0.1, -hd + 0.01)), BRASS)
        g.add(sphere((pivot_x - s * span + s * 0.04, 1.0, -hd - 0.08), 0.05), BRASS)
        g.build(origin=(pivot_x, 0, -hd), parent=root)


def shaft_segment(root):
    """One slot of shaft: dark panelled back wall with brass rails and a band at each floor line."""
    m = Model("Segment")
    s = S - 0.06
    m.add(box((-s, -0.3, 1.5), (s, SLOT - 0.3, 1.62)), col("3B2A2E", 0.35))
    for k in range(2):
        x0 = -s + 0.15 + k * (2 * s - 0.3) / 2
        x1 = x0 + (2 * s - 0.3) / 2 - 0.15
        m.add(box((x0, 0.2, 1.46), (x1, SLOT - 0.6, 1.5), 0.02), col("4A3438", 0.35))
    m.add(box((-s, -0.3, 1.42), (s, -0.18, 1.62)), BRASS)
    for x in (-s, s):
        for z in (-1.42, 1.38):
            m.add(box((x - 0.06, -0.3, z), (x + 0.06, SLOT - 0.3, z + 0.12)), BRASS)
    # guide rail with rivets
    for x in (-s + 0.25, s - 0.25):
        m.add(box((x - 0.05, -0.3, 1.36), (x + 0.05, SLOT - 0.3, 1.46)), met("6E6A70", 0.5))
        for y in (0.2, 1.2, 2.2):
            m.add(sphere((x, y, 1.35), 0.025), CHROME)
    m.build(parent=root)


def shaft_top(root):
    """Machine room in the middle of the roof: the neon sign lives on its face, the pulley on top."""
    m = Model("Housing")
    m.add(box((-4.9, 0, -0.45), (4.9, 2.2, 2.2), 0.05), col("4A1E26", 0.15))
    m.add(box((-5.05, 2.2, -0.6), (5.05, 2.4, 2.35), 0.04), col("F3E6C8", 0.3))
    m.add(box((-4.95, -0.05, -0.62), (4.95, 0.12, 2.3), 0.03), BRASS)
    # dark sign panel with a brass frame and marquee bulbs
    m.add(box((-4.6, 0.3, -0.5), (4.6, 1.95, -0.45)), col("1E1622", 0.3))
    for y in (0.27, 1.98):
        m.add(box((-4.65, y - 0.04, -0.53), (4.65, y + 0.04, -0.45)), BRASS)
    for x in (-4.65, 4.65):
        m.add(box((x - 0.04, 0.27, -0.53), (x + 0.04, 1.98, -0.45)), BRASS)
    for k in range(24):
        x = -4.4 + k * 8.8 / 23
        m.add(sphere((x, 0.27, -0.56), 0.045), glo("FFD48A", 3.0))
        m.add(sphere((x, 1.98, -0.56), 0.045), glo("FFD48A", 3.0))
    # pulley mounts on the roof of the room
    for x in (-0.25, 0.25):
        m.add(box((x - 0.07, 2.4, 0.4), (x + 0.07, 3.2, 1.0)), met("3A3A44", 0.4))
    m.add(cyl((2.4, 2.75, 0.8), 0.32, 0.9, "x"), met("4A6E8A", 0.5))
    m.add(box((1.9, 2.4, 0.4), (2.9, 2.5, 1.2)), met("3A3A44", 0.4))
    m.build(parent=root)
    wheel = Model("Pulley")
    cy = 3.2
    wheel.add(torus((0, cy, 0.7), 0.82, 0.08, "x", seg=32), BRASS)
    wheel.add(cyl((0, cy, 0.7), 0.16, 0.24, "x"), INK)
    for k in range(8):
        wheel.add(boxc((0, cy, 0.7), (0.06, 1.6, 0.07)), BRASS, transform=rot_about((0, cy, 0.7), "X", k * 22.5))
    wheel.build(origin=(0, cy, 0.7), parent=root)


def roof(root):
    m = Model("Roof")
    L = HW + 0.3
    brick = col(BRICK, 0.1)
    cream = col("F3E6C8", 0.3)
    m.add(box((-L, 0, FRONT - 0.1), (L, 0.4, BACK + 0.1)), brick)
    # stepped Deco cornice
    m.add(box((-L - 0.3, 0.4, FRONT - 0.35), (L + 0.3, 0.62, BACK + 0.25), 0.03), cream)
    m.add(box((-L - 0.15, 0.62, FRONT - 0.2), (L + 0.15, 0.75, BACK + 0.15), 0.02), BRASS)
    for x in range(-7, 8):
        m.add(box((x - 0.12, 0.4, FRONT - 0.4), (x + 0.12, 0.62, FRONT - 0.33)), BRASS)
    # parapet with crenellated Deco blocks at the corners
    m.add(box((-L, 0.75, FRONT - 0.1), (L, 1.15, FRONT + 0.2)), brick)
    for s in (-1, 1):
        for k in range(3):
            x0 = s * (L - 0.1 - k * 0.42)
            m.add(box((min(x0, x0 - s * 0.36), 0.75, FRONT - 0.15), (max(x0, x0 - s * 0.36), 1.45 + 0.35 * (2 - k), FRONT + 0.25), 0.03),
                  brick if k % 2 == 0 else cream)
    # water tower: barrel with hoops on stilts, conical cap
    tx, tz = 5.7, 1.8
    for ox, oz in ((-0.7, -0.7), (0.7, -0.7), (-0.7, 0.7), (0.7, 0.7)):
        m.add(rod((tx + ox, 0.4, tz + oz), (tx + ox * 0.85, 2.3, tz + oz * 0.85), 0.06), col("3A302C", 0.3))
    m.add(rod((tx - 0.7, 1.3, tz - 0.7), (tx + 0.7, 1.3, tz + 0.7), 0.03), col("3A302C", 0.3))
    m.add(rod((tx + 0.7, 1.3, tz - 0.7), (tx - 0.7, 1.3, tz + 0.7), 0.03), col("3A302C", 0.3))
    m.add(lathe([(0.0, 0), (0.92, 0), (0.98, 0.2), (1.02, 1.4), (0.98, 1.65), (0.0, 1.65)], (tx, 2.3, tz), seg=24), col("9A6440", 0.3))
    for y in (2.55, 3.1, 3.6):
        m.add(torus((tx, y, tz), 1.0, 0.03, "y", seg=28), met("3A3A44", 0.4))
    m.add(lathe([(1.08, 0), (0.0, 0.85)], (tx, 3.95, tz), seg=24), col("5E3B28", 0.3))
    m.add(sphere((tx, 4.82, tz), 0.08), BRASS)
    # roof door hut and antenna
    m.add(box((-6.9, 0.4, 1.4), (-5.6, 2.3, 2.8), 0.04), col("D9CBB0", 0.2))
    m.add(box((-7.0, 2.3, 1.3), (-5.5, 2.45, 2.9), 0.03), BRASS)
    m.add(box((-6.6, 0.4, 1.36), (-5.9, 1.9, 1.4)), col("3D5A80", 0.3))
    m.add(rod((-6.2, 2.45, 2.1), (-6.2, 4.4, 2.1), 0.03), met("8C979A", 0.5))
    for y in (3.2, 3.7, 4.1):
        m.add(rod((-6.6, y, 2.1), (-5.8, y, 2.1), 0.015), met("8C979A", 0.5))
    m.add(sphere((-6.2, 4.42, 2.1), 0.07), glo("FF4040", 3.0))
    m.build(parent=root)


def base(root):
    """Foundation under slot 0 plus the pavement, lamps, planters and a hydrant."""
    m = Model("Base")
    L = HW + 0.6
    stone = col("B9A88E", 0.15)
    m.add(box((-L, -2.0, FRONT - 0.3), (L, -0.3, BACK + 0.3)), col("5A222B", 0.1))
    m.add(box((-L - 0.15, -0.5, FRONT - 0.42), (L + 0.15, -0.3, BACK + 0.4), 0.02), col("F3E6C8", 0.3))
    m.add(box((-L - 0.2, -2.05, FRONT - 0.45), (L + 0.2, -1.6, BACK + 0.45), 0.03), stone)
    # rusticated blocks
    for k in range(16):
        x = -L + 0.2 + k * (2 * L - 0.4) / 16
        m.add(box((x, -1.55, FRONT - 0.36), (x + (2 * L - 0.4) / 16 - 0.08, -0.6, FRONT - 0.3), 0.03), col("6A2A33", 0.1))
    # plaque
    m.add(box((-1.6, -1.35, FRONT - 0.42), (1.6, -0.75, FRONT - 0.36), 0.02), BRASS)
    m.add(box((-1.5, -1.28, FRONT - 0.44), (1.5, -0.82, FRONT - 0.41)), col("2A1E2E", 0.4))
    # pavement and curb
    m.add(box((-70, -2.45, -12), (70, -2.05, 10)), stone)
    for k in range(-30, 30):
        m.add(box((k * 2.2, -2.06, -11.9), (k * 2.2 + 0.03, -2.05, 9.9)), col("A39478", 0.1))
    m.add(box((-70, -2.6, -12.4), (70, -2.25, -11.9)), col("D9CBB0", 0.2))
    m.add(box((-70, -2.75, -40), (70, -2.6, -12.4)), col("3B3540", 0.3))
    for k in range(-14, 15):
        m.add(box((k * 5 - 1.2, -2.6, -20.1), (k * 5 + 1.2, -2.59, -19.8)), col("E8DCC0", 0.3))
    # street lamps
    for x in (-12.0, 11.0, -25, 24):
        m.add(cyl((x, -2.0, -10.8), 0.2, 0.2, "y"), col("1C1820", 0.4))
        m.add(rod((x, -2.0, -10.8), (x, 2.2, -10.8), 0.07), col("1C1820", 0.4))
        m.add(rod((x, 2.2, -10.8), (x + 0.6, 2.6, -10.8), 0.04), col("1C1820", 0.4))
        m.add(lathe([(0.0, 0), (0.22, 0.05), (0.28, 0.35), (0.12, 0.5), (0.0, 0.52)], (x + 0.6, 2.2, -10.8)), gls("FFE7B0", 0.8))
        m.add(sphere((x + 0.6, 2.42, -10.8), 0.1), glo("FFE2A8", 3.0))
    # planters with lollipop trees
    for x in (-10.5, 9.5, -18, 17):
        m.add(box((x - 0.6, -2.05, -9.6), (x + 0.6, -1.5, -8.4), 0.04), col("B9A88E", 0.15))
        m.add(rod((x, -1.5, -9.0), (x, 0.4, -9.0), 0.08), col("6B4A2E", 0.3))
        m.add(sphere((x, 1.0, -9.0), 0.95, (1, 1.05, 1), seg=16, rings=10), col("4E9A3F", 0.3))
        m.add(sphere((x + 0.4, 1.3, -9.4), 0.55, seg=14, rings=8), col("5EBB4A", 0.3))
    # hydrant, mailbox, bench
    m.add(lathe([(0.0, 0), (0.2, 0), (0.18, 0.55), (0.22, 0.6), (0.15, 0.75), (0.0, 0.8)], (7.0, -2.05, -10.6)), col("E5484D", 0.4))
    m.add(cyl((7.0, -1.75, -10.6), 0.07, 0.6, "x"), col("E5484D", 0.4))
    m.add(box((-8.2, -2.05, -10.9), (-7.6, -0.9, -10.4), 0.08), col("3D5A80", 0.4))
    m.add(box((3.5, -1.55, -11.1), (5.5, -1.45, -10.6), 0.03), WOOD)
    m.add(box((3.5, -1.45, -10.7), (5.5, -1.0, -10.6), 0.03), WOOD)
    for x in (3.7, 5.3):
        m.add(box((x - 0.04, -2.05, -11.0), (x + 0.04, -1.55, -10.7)), col("1C1820", 0.4))
    m.build(parent=root)


def cloud(root):
    m = Model("Cloud")
    rng = random.Random(4)
    for k in range(7):
        x = -2.4 + k * 0.8 + rng.uniform(-0.2, 0.2)
        r = 0.9 + 0.7 * math.sin(k / 6 * math.pi) + rng.uniform(-0.1, 0.2)
        m.add(sphere((x, r * 0.55, rng.uniform(-0.3, 0.3)), r, (1, 0.8, 0.8), seg=16, rings=10), col("FFFFFF", 0.05))
    m.add(box((-3.0, -0.2, -0.6), (3.0, 0.3, 0.6), 0.2), col("FFFFFF", 0.05))
    m.build(parent=root)


def tower(root, variant):
    """Distant Deco skyscraper silhouettes for the skyline (window glow is a separate mesh)."""
    rng = random.Random(variant)
    body = Model("Tower")
    win = Model("Windows")
    h = rng.uniform(16, 34)
    w = rng.uniform(5, 8)
    d = 5
    c = rng.choice(["2B2F4A", "33304F", "2A3A52"])
    body.add(box((-w / 2, 0, -d / 2), (w / 2, h, d / 2)), col(c, 0.1))
    top = h
    for k in range(rng.randint(1, 3)):
        w2 = w * (0.75 - k * 0.18)
        hh = rng.uniform(2, 4)
        body.add(box((-w2 / 2, top, -d / 2 * 0.8), (w2 / 2, top + hh, d / 2 * 0.8)), col(c, 0.1))
        top += hh
    if rng.random() < 0.6:
        body.add(rod((0, top, 0), (0, top + rng.uniform(3, 7), 0), 0.15, radius2=0.02), col(c, 0.1))
    for r in range(1, int(h / 2.2)):
        for k in range(int(w / 1.3)):
            if rng.random() < 0.35:
                x = -w / 2 + 0.6 + k * 1.3
                win.add(box((x, r * 2.2, -d / 2 - 0.02), (x + 0.55, r * 2.2 + 0.8, -d / 2)), glo("FFD48A", 2.0))
    body.build(parent=root)
    if not win.empty():
        win.build(parent=root)


def fx_meshes(root):
    coin = Model("Coin")
    # one material only: particle systems need single-submesh meshes
    coin.add(cyl((0, 0, 0), 0.22, 0.05, "z", seg=24, bevel=0.01), met("FFC83D", 0.8))
    coin.add(cyl((0, 0, 0), 0.15, 0.07, "z", seg=24), met("FFC83D", 0.8))
    coin.build(parent=root)
    bat = Model("Bat")
    for s in (-1, 1):
        bat.add(prism([(0, 0.05), (s * 0.45, 0.2), (s * 0.35, -0.02), (s * 0.22, 0.04), (s * 0.1, -0.06)], -0.02, 0.02), col("1C1820", 0.4))
    bat.add(sphere((0, 0.02, 0), 0.09), col("1C1820", 0.4))
    bat.build(origin=(0, 0, 0), parent=root)
    gull = Model("Gull")
    gull.add(sphere((0, 0, 0), 0.18, (1.6, 0.8, 0.8)), WHITE)
    for s in (-1, 1):
        gull.add(prism([(0, 0.02), (s * 0.55, 0.15), (s * 0.5, 0.08)], -0.1, 0.08), WHITE)
    gull.add(rod((0.28, 0.02, 0), (0.4, 0.0, 0), 0.03, radius2=0.005), WHITE)
    gull.build(parent=root)


BUILDERS = {"Car": car, "ShaftSegment": shaft_segment, "ShaftTop": shaft_top, "Roof": roof, "Base": base, "Cloud": cloud,
            "FxMeshes": fx_meshes}
for i in range(5):
    BUILDERS[f"Tower{i}"] = (lambda i: (lambda r: tower(r, i)))(i)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(BUILDERS)
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    export = "--no-export" not in argv
    for name in only:
        reset_scene()
        root = empty(name)
        BUILDERS[name](root)
        if export:
            export_fbx(os.path.join(OUT, f"{name}.fbx"), [root])
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "blend", f"{name}.blend"))
            print("exported", name)
        if preview:
            setup_preview()
            render_views(os.path.join(preview, f"{name}.png"), [root], size=(640, 480), views=((14, 14),), lens=50, margin=1.0)


if __name__ == "__main__":
    os.makedirs(os.path.join(ROOT, "ArtSource", "blend"), exist_ok=True)
    main()
