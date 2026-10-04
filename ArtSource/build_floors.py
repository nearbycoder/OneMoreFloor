"""The ten floor modules of The Shuffleton.

    blender -b -P ArtSource/build_floors.py -- [--only Lobby,Office] [--preview DIR] [--no-export]

Each floor is authored in Unity space with its floor top at y=0, spanning x in [-W, W] with the
elevator shaft opening at |x| < S, the open (cutaway) front at z = FRONT and the back wall at z = BACK.
Waiting passengers queue on the right, near the front (x 2.7..6.2, z -1.0), so props on the right
stay toward the back. Exported to Assets/Resources/Models/Floor_<Id>.fbx.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from omf_lib import *  # noqa: E402,F401,F403
import omf_lib as L  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models")

# Must match Layout in Assets/Scripts/Game/Util.cs
H = 2.8          # slot height
SLAB = 0.3
W = 7.6          # half width
S = 1.9          # shaft half width
FRONT = -2.1
BACK = 3.0
ROOM_H = H - SLAB  # 2.7
SHAFT_FRONT = -1.35

THEMES = {
    "Lobby":      dict(wall="F2D7B6", wains="8E2433", floor="F4ECDD", trim="D7A84A", accent="C8323F"),
    "Office":     dict(wall="F2E3AE", wains="A27F2E", floor="5E7F5A", trim="D9C089", accent="E0A526"),
    "Library":    dict(wall="2F5B4F", wains="6B4226", floor="8A5A36", trim="C9A25A", accent="95603A"),
    "Laundromat": dict(wall="C8F0EC", wains="3FBFC0", floor="F2F7F6", trim="9FDCD6", accent="3FBFC0"),
    "Boiler":     dict(wall="8C5A44", wains="4A3A36", floor="7D7570", trim="B5835A", accent="E2622A"),
    "Greenhouse": dict(wall="DDF2E2", wains="5E8F4A", floor="C9744A", trim="F4F4EC", accent="7CC242"),
    "Penthouse":  dict(wall="4B2E6E", wains="2E1C45", floor="6B3FA0", trim="D7A84A", accent="7B4FC0"),
    "Crypt":      dict(wall="5A6270", wains="3A404A", floor="4A4F58", trim="767E8A", accent="7CFFB2"),
    "Ocean":      dict(wall="9FD8F0", wains="1F86D0", floor="1F86D0", trim="E8DCC0", accent="1F86D0"),
    "Daycare":    dict(wall="FFE3EE", wains="F06EAA", floor="FFD6E6", trim="FFFFFF", accent="F06EAA"),
}
BRICK = "7A2F38"


def add(m, prim, mat, **kw):
    m.add(prim, mat, **kw)


# ----------------------------------------------------------------------------- shared shell

def shell(m, t, name, glow, walls=True):
    """Slabs, walls, the landing, the shaft portal and pendant lamps."""
    floor_mat = col(t["floor"], 0.25)
    # slab body (underside visible from the floor below = ceiling)
    for x0, x1 in ((-W, -S), (S, W)):
        add(m, box((x0, -SLAB, FRONT), (x1, -0.06, BACK)), col("EADFCB", 0.15))
        add(m, box((x0, -0.06, FRONT), (x1, 0.0, BACK)), floor_mat)
    add(m, box((-S, -SLAB, FRONT), (S, -0.06, SHAFT_FRONT)), col("EADFCB", 0.15))
    add(m, box((-S, -0.06, FRONT), (S, 0.0, SHAFT_FRONT)), floor_mat)
    # front edge: cream band with a brass strip, like a theatre balcony
    add(m, box((-W - 0.32, -SLAB - 0.02, FRONT - 0.08), (W + 0.32, -0.08, FRONT + 0.02), 0.03), col("F3E6C8", 0.3))
    add(m, box((-W - 0.32, -0.08, FRONT - 0.06), (W + 0.32, -0.02, FRONT + 0.02), 0.01), BRASS)
    if not walls:
        return
    wall = col(t["wall"], 0.2)
    wains = col(t["wains"], 0.35)
    trim = col(t["trim"], 0.45)
    for x0, x1 in ((-W, -S - 0.12), (S + 0.12, W)):
        # back wall: wainscot, chair rail, wallpaper, cornice
        add(m, box((x0, 0, BACK - 0.12), (x1, 0.95, BACK)), wains)
        add(m, box((x0, 0.95, BACK - 0.16), (x1, 1.03, BACK), 0.015), trim)
        add(m, box((x0, 1.03, BACK - 0.1), (x1, ROOM_H, BACK)), wall)
        add(m, box((x0, ROOM_H - 0.16, BACK - 0.2), (x1, ROOM_H, BACK), 0.02), trim)
        add(m, box((x0, 0, BACK - 0.16), (x1, 0.1, BACK)), trim)  # skirting
    # exterior brick walls with a window each
    for side in (-1, 1):
        xo, xi = side * (W + 0.3), side * W
        lo, hi = min(xo, xi), max(xo, xi)
        add(m, box((lo, -SLAB, FRONT), (hi, 0.75, BACK + 0.1)), col(BRICK, 0.1))
        add(m, box((lo, 2.15, FRONT), (hi, ROOM_H, BACK + 0.1)), col(BRICK, 0.1))
        add(m, box((lo, 0.75, FRONT), (hi, 2.15, 0.15)), col(BRICK, 0.1))
        add(m, box((lo, 0.75, 1.75), (hi, 2.15, BACK + 0.1)), col(BRICK, 0.1))
        # window: cream frame, glass, sill
        add(m, box((lo - 0.02, 0.7, 0.1), (hi + 0.02, 0.8, 1.8)), col("F3E6C8", 0.3))
        add(m, box((lo - 0.02, 2.1, 0.1), (hi + 0.02, 2.2, 1.8)), col("F3E6C8", 0.3))
        add(m, box((lo - 0.02, 0.75, 0.1), (hi + 0.02, 2.15, 0.2)), col("F3E6C8", 0.3))
        add(m, box((lo - 0.02, 0.75, 1.7), (hi + 0.02, 2.15, 1.8)), col("F3E6C8", 0.3))
        add(m, box((lo - 0.01, 1.42, 0.15), (hi + 0.01, 1.48, 1.75)), col("F3E6C8", 0.3))
        add(m, box((lo + 0.12, 0.8, 0.2), (hi - 0.12, 2.1, 1.7)), gls("BFE3F2", 0.35))
        # pilaster at the front corner
        add(m, box((lo - 0.06, -SLAB, FRONT - 0.1), (hi + 0.06, ROOM_H, FRONT + 0.22), 0.03), col(shade(BRICK, 0.85), 0.1))
    # walls flanking the shaft
    for side in (-1, 1):
        x0, x1 = sorted((side * S, side * (S + 0.14)))
        add(m, box((x0, 0, SHAFT_FRONT), (x1, ROOM_H, BACK)), col(shade(t["wains"], 0.9), 0.3))
    # portal: pilasters, header and a Deco fan around the shaft opening
    for side in (-1, 1):
        x0, x1 = sorted((side * (S - 0.02), side * (S + 0.32)))
        add(m, box((x0, 0, SHAFT_FRONT - 0.18), (x1, ROOM_H, SHAFT_FRONT + 0.05), 0.03), trim)
        add(m, box((x0 - 0.02, 0, SHAFT_FRONT - 0.22), (x1 + 0.02, 0.25, SHAFT_FRONT + 0.06), 0.02), BRASS)
        for k in range(3):
            xx = side * (S + 0.06 + k * 0.09)
            add(m, box((xx - 0.018, 0.3, SHAFT_FRONT - 0.2), (xx + 0.018, ROOM_H - 0.45, SHAFT_FRONT - 0.17)), BRASS)
        # call button plate on the right pilaster
        if side == 1:
            add(m, box((S + 0.4, 1.1, SHAFT_FRONT - 0.06), (S + 0.62, 1.5, SHAFT_FRONT - 0.02), 0.02), BRASS)
            add(m, cyl((S + 0.51, 1.37, SHAFT_FRONT - 0.07), 0.05, 0.04, "z"), glo("FFB547", 2.5))
            add(m, cyl((S + 0.51, 1.22, SHAFT_FRONT - 0.07), 0.05, 0.04, "z"), CREAM)
    add(m, box((-S - 0.3, ROOM_H - 0.42, SHAFT_FRONT - 0.2), (S + 0.3, ROOM_H, SHAFT_FRONT + 0.05), 0.03), trim)
    add(m, box((-S - 0.3, ROOM_H - 0.46, SHAFT_FRONT - 0.23), (S + 0.3, ROOM_H - 0.42, SHAFT_FRONT + 0.05)), BRASS)
    for k in range(9):
        a = math.radians(20 + k * 17.5)
        p0 = V(0, ROOM_H - 0.44, SHAFT_FRONT - 0.215)
        add(m, rod(p0 + V(math.cos(a), math.sin(a), 0) * 0.12, p0 + V(math.cos(a) * 1.35, math.sin(a) * 0.4, 0), 0.016), BRASS)
    # pendant lamps
    for x in (-4.9, 4.9):
        add(m, rod((x, ROOM_H, 0.8), (x, ROOM_H - 0.55, 0.8), 0.015), INK)
        add(m, lathe([(0.05, 0), (0.22, 0.12), (0.26, 0.2), (0.0, 0.2)], (x, ROOM_H - 0.75, 0.8)), BRASS)
        add(glow, sphere((x, ROOM_H - 0.78, 0.8), 0.1), glo("FFE2A8", 3.0))


# ----------------------------------------------------------------------------- reusable props

def potted_plant(m, p, s=1.0, pot="C8643C", leaf="3FA34D", rng=None):
    x, y, z = p
    add(m, lathe([(0.18 * s, 0), (0.22 * s, 0.32 * s), (0.25 * s, 0.36 * s), (0.25 * s, 0.4 * s), (0.0, 0.4 * s)], (x, y, z)), col(pot, 0.25))
    add(m, cyl((x, y + 0.39 * s, z), 0.21 * s, 0.02, "y"), col("4A3020", 0.1))
    rng = rng or random.Random(int(x * 100 + z * 10))
    for i in range(6):
        a = i * 60 + rng.uniform(-15, 15)
        tilt = rng.uniform(20, 45)
        L_ = rng.uniform(0.45, 0.7) * s
        d = V(math.sin(math.radians(a)) * math.sin(math.radians(tilt)), math.cos(math.radians(tilt)), math.cos(math.radians(a)) * math.sin(math.radians(tilt)))
        tip = V(x, y + 0.4 * s, z) + d * L_
        add(m, rod((x, y + 0.4 * s, z), tip, 0.015 * s), col(shade(leaf, 0.7), 0.3))
        add(m, sphere(tip, 0.16 * s, (1.0, 0.45, 1.0)), col(leaf if i % 2 else shade(leaf, 0.82), 0.45))


def chair(m, p, c="8E2433", facing=0):
    x, y, z = p
    rot = rot_about((x, y, z), "Y", facing)
    for dx in (-0.2, 0.2):
        for dz in (-0.2, 0.2):
            add(m, rod((x + dx, y, z + dz), (x + dx, y + 0.45, z + dz), 0.025), WOOD_DARK, transform=rot)
    add(m, box((x - 0.26, y + 0.42, z - 0.26), (x + 0.26, y + 0.52, z + 0.26), 0.04), col(c, 0.3), transform=rot)
    add(m, box((x - 0.26, y + 0.5, z + 0.18), (x + 0.26, y + 1.05, z + 0.27), 0.04), col(c, 0.3), transform=rot)


def armchair(m, p, c="B3122E", facing=0):
    x, y, z = p
    rot = rot_about((x, y, z), "Y", facing)
    add(m, box((x - 0.5, y + 0.1, z - 0.42), (x + 0.5, y + 0.48, z + 0.42), 0.1), col(c, 0.25), transform=rot)
    add(m, box((x - 0.5, y + 0.4, z + 0.18), (x + 0.5, y + 1.15, z + 0.45), 0.12), col(c, 0.25), transform=rot)
    for sx in (-1, 1):
        add(m, box((x + sx * 0.5 - 0.14, y + 0.1, z - 0.42), (x + sx * 0.5 + 0.14, y + 0.78, z + 0.45), 0.1), col(shade(c, 0.85), 0.25), transform=rot)
    for sx in (-0.38, 0.38):
        for sz in (-0.32, 0.32):
            add(m, cyl((x + sx, y + 0.05, z + sz), 0.04, 0.1, "y"), BRASS, transform=rot)


def table(m, p, w=1.2, d=0.7, h=0.75, top="7A4A2E", legs=None):
    x, y, z = p
    add(m, box((x - w / 2, y + h - 0.06, z - d / 2), (x + w / 2, y + h, z + d / 2), 0.02), col(top, 0.45))
    for sx in (-1, 1):
        for sz in (-1, 1):
            add(m, box((x + sx * (w / 2 - 0.08) - 0.035, y, z + sz * (d / 2 - 0.08) - 0.035),
                       (x + sx * (w / 2 - 0.08) + 0.035, y + h - 0.06, z + sz * (d / 2 - 0.08) + 0.035)), col(legs or shade(top, 0.75), 0.4))


def floor_lamp(m, glow, p, shade_col="F3D9A6"):
    x, y, z = p
    add(m, cyl((x, y + 0.02, z), 0.18, 0.04, "y"), BRASS)
    add(m, rod((x, y, z), (x, y + 1.45, z), 0.022), BRASS)
    add(m, lathe([(0.24, 0), (0.15, 0.32), (0.0, 0.32)], (x, y + 1.35, z)), col(shade_col, 0.2))
    add(glow, sphere((x, y + 1.4, z), 0.07), glo("FFE2A8", 2.5))


def wall_frame(m, p, w, h, c, art):
    x, y, z = p
    add(m, box((x - w / 2, y, z - 0.06), (x + w / 2, y + h, z), 0.02), BRASS)
    add(m, box((x - w / 2 + 0.06, y + 0.06, z - 0.08), (x + w / 2 - 0.06, y + h - 0.06, z - 0.05)), col(art, 0.4))


def wall_clock(m, glow, p, r=0.3):
    x, y, z = p
    add(m, cyl((x, y, z), r, 0.08, "z"), BRASS)
    add(m, cyl((x, y, z - 0.03), r * 0.86, 0.06, "z"), col("F6EFE0", 0.4))
    add(m, box((x - 0.012, y, z - 0.08), (x + 0.012, y + r * 0.6, z - 0.06)), INK)
    add(m, box((x, y - 0.012, z - 0.08), (x + r * 0.45, y + 0.012, z - 0.06)), INK)


# ----------------------------------------------------------------------------- floors

def lobby(m, glow, anim, t):
    # checkerboard marble
    for i in range(-10, 10):
        for j in range(7):
            x0, z0 = i * 0.76, FRONT + j * 0.76
            if abs(x0 + 0.38) < S + 0.2 and z0 > SHAFT_FRONT - 0.7:
                continue
            if (i + j) % 2 == 0:
                add(m, box((max(x0, -W), 0.0, z0), (min(x0 + 0.76, W), 0.012, min(z0 + 0.76, BACK - 0.12))), col("2A1E2E", 0.6))
    # red runner from the shaft to the left
    add(m, box((-W + 0.2, 0.012, -1.6), (-S - 0.3, 0.03, -0.7)), col("B3122E", 0.15))
    add(m, box((-W + 0.2, 0.03, -1.62), (-S - 0.3, 0.035, -1.56)), BRASS)
    # reception desk (curved front) with a bell, behind it a key rack
    add(m, box((-6.9, 0, 1.3), (-3.6, 1.05, 2.05), 0.06), WOOD)
    add(m, box((-7.0, 1.05, 1.22), (-3.5, 1.13, 2.12), 0.03), col("F4ECDD", 0.7))
    for k in range(5):
        add(m, box((-6.7 + k * 0.62, 0.15, 1.27), (-6.3 + k * 0.62, 0.9, 1.3)), BRASS)
    add(m, lathe([(0.12, 0), (0.12, 0.02), (0.1, 0.06), (0.05, 0.12), (0.0, 0.13)], (-4.2, 1.13, 1.6)), BRASS)
    add(m, sphere((-4.2, 1.27, 1.6), 0.02), BRASS)
    for r in range(3):
        for c in range(6):
            add(m, box((-6.6 + c * 0.42, 1.5 + r * 0.32, BACK - 0.16), (-6.32 + c * 0.42, 1.74 + r * 0.32, BACK - 0.12)), WOOD_DARK)
            add(m, sphere((-6.46 + c * 0.42, 1.62 + r * 0.32, BACK - 0.18), 0.03), BRASS)
    # palms
    potted_plant(m, (-2.9, 0, -1.25), 1.5, pot="D7A84A", leaf="3E8E41")
    potted_plant(m, (7.0, 0, 2.3), 1.6, pot="D7A84A", leaf="3E8E41")
    # luggage cart, back right
    add(m, box((4.0, 0.25, 1.9), (5.6, 0.32, 2.7)), BRASS)
    for x in (4.05, 5.55):
        add(m, rod((x, 0.3, 2.3), (x, 1.9, 2.3), 0.03), BRASS)
    add(m, rod((4.05, 1.9, 2.3), (5.55, 1.9, 2.3), 0.03), BRASS)
    for x in (4.2, 5.4):
        add(m, cyl((x, 0.12, 1.95), 0.12, 0.05, "x"), INK)
        add(m, cyl((x, 0.12, 2.65), 0.12, 0.05, "x"), INK)
    add(m, box((4.15, 0.32, 2.0), (4.95, 0.95, 2.6), 0.05), col("7B4A8E", 0.3))
    add(m, box((4.95, 0.32, 2.05), (5.5, 0.75, 2.55), 0.05), col("2F6F6A", 0.3))
    add(m, box((4.4, 0.95, 2.1), (5.1, 1.3, 2.5), 0.05), col("C8643C", 0.3))
    # chandelier
    add(m, rod((0, ROOM_H, 0.2), (0, ROOM_H - 0.3, 0.2), 0.02), BRASS)
    for x in (-4.9, 4.9):
        for k in range(6):
            a = k * 60
            px, pz = x + 0.42 * math.cos(math.radians(a)), 0.8 + 0.42 * math.sin(math.radians(a))
            add(m, rod((x, ROOM_H - 0.78, 0.8), (px, ROOM_H - 0.7, pz), 0.012), BRASS)
            add(glow, sphere((px, ROOM_H - 0.66, pz), 0.05), glo("FFE2A8", 3.0))
    # bench + framed portrait of the founder
    add(m, box((5.9, 0.42, 2.3), (7.3, 0.52, 2.8), 0.04), col("8E2433", 0.3))
    wall_frame(m, (-5.2, 1.35, BACK - 0.1), 1.0, 1.1, None, "3D5A80")
    add(m, sphere((-5.2, 1.95, BACK - 0.2), 0.18, (1, 1.2, 0.4)), col("F2C9A0", 0.3))
    add(m, sphere((-5.2, 1.6, BACK - 0.2), 0.3, (1, 0.8, 0.3)), col("2A1E2E", 0.3))


def office(m, glow, anim, t):
    rng = random.Random(2)
    for i, x in enumerate((-6.6, -4.4)):
        # cubicle: low partitions, desk, CRT, lamp, chair
        add(m, box((x - 1.0, 0, 2.55), (x + 1.0, 1.35, 2.65), 0.03), col("8C9A8A", 0.2))
        add(m, box((x + 1.0, 0, 1.0), (x + 1.1, 1.1, 2.65), 0.03), col("8C9A8A", 0.2))
        table(m, (x, 0, 1.6), 1.6, 0.8, 0.76, "C9A26B")
        add(m, box((x - 0.35, 0.76, 1.55), (x + 0.15, 1.18, 2.0), 0.05), col("D8CFB8", 0.3))
        add(m, box((x - 0.27, 0.84, 1.53), (x + 0.07, 1.1, 1.56)), glo("7CE0A8", 1.4))
        add(m, box((x - 0.4, 0.76, 1.25), (x + 0.2, 0.8, 1.45)), col("D8CFB8", 0.3))
        add(m, rod((x + 0.55, 0.76, 1.8), (x + 0.55, 1.25, 1.75), 0.015), INK)
        add(m, lathe([(0.0, 0), (0.14, 0.06), (0.08, 0.18), (0.0, 0.18)], (x + 0.55, 1.12, 1.68)), col("2F6F3A", 0.5))
        chair(m, (x - 0.1, 0, 0.95), "4C6A92", 180)
        # papers
        for k in range(3):
            add(m, box((x + 0.2 + k * 0.05, 0.76 + k * 0.012, 1.3), (x + 0.5 + k * 0.05, 0.772 + k * 0.012, 1.55)), WHITE)
    # filing cabinets + water cooler (right, back)
    for k in range(3):
        add(m, box((3.4 + k * 0.6, 0, 2.3), (3.95 + k * 0.6, 1.35, 2.85), 0.03), met("8C979A", 0.4))
        for d in range(3):
            add(m, box((3.6 + k * 0.6, 0.3 + d * 0.4, 2.27), (3.75 + k * 0.6, 0.34 + d * 0.4, 2.3)), CHROME)
    add(m, box((5.6, 0, 2.3), (6.1, 1.0, 2.8), 0.04), col("E8E4DA", 0.4))
    add(m, lathe([(0.2, 0), (0.22, 0.1), (0.22, 0.42), (0.1, 0.52), (0.06, 0.6), (0.0, 0.6)], (5.85, 1.0, 2.55)), gls("6EC6F0", 0.55))
    potted_plant(m, (6.9, 0, 2.4), 1.3)
    wall_clock(m, glow, (1.0 + 4.5, 2.0, BACK - 0.12))
    # motivational poster
    wall_frame(m, (3.6, 1.35, BACK - 0.1), 0.9, 0.7, None, "E0A526")


def library(m, glow, anim, t):
    rng = random.Random(3)
    book_cols = ["8E2433", "2F6F6A", "D7A84A", "3D5A80", "6B4E9B", "C8643C", "2F5B4F", "E8DCC0", "4E2E1C"]
    for x0, x1 in ((-7.4, -2.3), (2.4, 7.3)):
        # tall shelves against the back wall
        add(m, box((x0, 0, BACK - 0.2), (x1, ROOM_H - 0.05, BACK - 0.12)), WOOD_DARK)
        for xs in (x0, x1 - 0.08):
            add(m, box((xs, 0, BACK - 0.72), (xs + 0.08, ROOM_H - 0.05, BACK - 0.12)), WOOD)
        add(m, box((x0, ROOM_H - 0.2, BACK - 0.76), (x1, ROOM_H - 0.05, BACK - 0.12)), WOOD)
        for r in range(4):
            y = 0.12 + r * 0.52
            add(m, box((x0, y, BACK - 0.72), (x1, y + 0.05, BACK - 0.14)), WOOD)
            x = x0 + 0.08
            while x < x1 - 0.15:
                w = rng.uniform(0.06, 0.13)
                h = rng.uniform(0.3, 0.42)
                lean = rng.random() < 0.06
                add(m, box((x, y + 0.05, BACK - 0.62), (x + w, y + 0.05 + h, BACK - 0.22), 0.01), col(rng.choice(book_cols), 0.3),
                    transform=rot_about((x, y + 0.05, BACK - 0.4), "Z", 12) if lean else None)
                x += w + rng.uniform(0.0, 0.02) + (0.15 if lean else 0)
    # rolling ladder
    add(m, rod((-5.0, 0, BACK - 1.2), (-5.0, ROOM_H - 0.1, BACK - 0.75), 0.03), BRASS)
    add(m, rod((-4.4, 0, BACK - 1.2), (-4.4, ROOM_H - 0.1, BACK - 0.75), 0.03), BRASS)
    for k in range(8):
        f = (k + 0.5) / 8
        add(m, rod((-5.0, f * (ROOM_H - 0.1), BACK - 1.2 + f * 0.45), (-4.4, f * (ROOM_H - 0.1), BACK - 1.2 + f * 0.45), 0.02), WOOD)
    armchair(m, (-6.6, 0, 1.0), "8E2433", 200)
    floor_lamp(m, glow, (-7.2, 0, 1.7))
    # reading table with banker's lamp and an open book
    table(m, (-3.4, 0, 0.9), 1.2, 0.7, 0.75, "6B4226")
    add(m, box((-3.75, 0.75, 0.8), (-3.4, 0.79, 1.05)), WHITE)
    add(m, box((-3.4, 0.75, 0.8), (-3.05, 0.79, 1.05)), WHITE)
    add(m, rod((-3.0, 0.75, 1.1), (-3.0, 1.05, 1.1), 0.015), BRASS)
    add(m, cyl((-3.0, 1.08, 1.05), 0.09, 0.32, "x"), col("1F6B3A", 0.6))
    add(glow, cyl((-3.0, 1.03, 1.05), 0.06, 0.28, "x"), glo("FFE2A8", 2.0))
    # globe
    add(m, lathe([(0.2, 0), (0.05, 0.05), (0.04, 0.6), (0.0, 0.6)], (6.6, 0, 1.5)), WOOD)
    add(m, torus((6.6, 0.95, 1.5), 0.36, 0.02, "z"), BRASS)
    add(m, sphere((6.6, 0.95, 1.5), 0.32), col("2F6F9A", 0.5))
    add(m, sphere((6.52, 1.05, 1.42), 0.18, (1.2, 0.8, 0.6)), col("7CB342", 0.4))


def laundromat(m, glow, anim, t):
    # tiled checker floor
    for i in range(-10, 10):
        for j in range(7):
            x0, z0 = i * 0.76, FRONT + j * 0.76
            if abs(x0 + 0.38) < S + 0.2 and z0 > SHAFT_FRONT - 0.7:
                continue
            if (i + j) % 2 == 0:
                add(m, box((max(x0, -W), 0.0, z0), (min(x0 + 0.76, W), 0.012, min(z0 + 0.76, BACK - 0.12))), col("3FBFC0", 0.6))
    # row of front-loaders against the back wall (drums animate in Unity)
    xs = [-7.0, -5.9, -4.8, -3.7, 3.0, 4.1, 5.2, 6.3]
    for i, x in enumerate(xs):
        z = BACK - 0.95
        add(m, box((x - 0.5, 0, z - 0.42), (x + 0.5, 1.15, z + 0.42), 0.06), WHITE)
        add(m, box((x - 0.5, 1.0, z - 0.43), (x + 0.5, 1.15, z - 0.4)), col("3FBFC0", 0.4))
        add(m, sphere((x + 0.32, 1.08, z - 0.45), 0.035), glo("FF5E5E" if i % 3 else "7CFF8A", 2.0))
        add(m, torus((x, 0.55, z - 0.44), 0.3, 0.05, "z"), CHROME)
        drum = Model(f"Drum{i}")
        drum.add(cyl((x, 0.55, z - 0.42), 0.27, 0.04, "z"), gls("6EC6F0", 0.45))
        for k in range(3):
            a = k * 120
            drum.add(boxc((x + 0.14 * math.cos(math.radians(a)), 0.55 + 0.14 * math.sin(math.radians(a)), z - 0.39),
                          (0.16, 0.16, 0.02), 0.03), col(["F06EAA", "FFD23F", "7B4FC0"][k], 0.3))
        anim.append((drum, (x, 0.55, z - 0.42)))
    # folding table with towels, baskets, soap machine
    table(m, (-3.3, 0, 0.9), 1.3, 0.7, 0.85, "E8E4DA", legs="9AA5A8")
    for k, c in enumerate(("F06EAA", "FFD23F", "3D5A80")):
        add(m, box((-3.8 + k * 0.38, 0.85 + 0.0, 0.75), (-3.45 + k * 0.38, 0.95, 1.05), 0.03), col(c, 0.1))
    for x, c in ((-6.6, "C8A06B"), (6.8, "C8A06B")):
        add(m, lathe([(0.26, 0), (0.3, 0.45), (0.0, 0.45)], (x, 0, 1.2)), col(c, 0.2))
        add(m, sphere((x, 0.5, 1.2), 0.24, (1, 0.5, 1)), col("F2F2EA", 0.1))
    add(m, box((6.9, 0, 1.0), (7.45, 1.8, 1.7), 0.04), col("E04E39", 0.4))
    add(m, box((6.88, 1.0, 1.1), (6.92, 1.6, 1.6)), gls("BFE3F2", 0.4))


def boiler(m, glow, anim, t):
    # hazard-striped floor edge and concrete
    for k in range(34):
        x0 = -W + k * 0.46
        if abs(x0) < S + 0.25:
            continue
        add(m, box((x0, 0.0, FRONT + 0.05), (x0 + 0.23, 0.014, FRONT + 0.3)), col("FFD23F", 0.3))
        add(m, box((x0 + 0.23, 0.0, FRONT + 0.05), (x0 + 0.46, 0.014, FRONT + 0.3)), col("2A1E2E", 0.3))
    # the boiler: big horizontal tank with rivets, firebox glow, gauges
    add(m, cyl((-5.0, 1.2, 1.7), 0.95, 3.4, "x", bevel=0.08), met("B5583A", 0.35))
    for x in (-6.4, -5.0, -3.6):
        add(m, torus((x, 1.2, 1.7), 0.96, 0.04, "x"), BRASS_DARK)
    add(m, box((-6.6, 0, 1.0), (-3.4, 0.35, 2.4), 0.04), col("3A302C", 0.2))
    add(m, box((-5.5, 0.4, 0.72), (-4.5, 0.95, 0.8), 0.03), col("2A1E2E", 0.3))
    add(glow, box((-5.4, 0.48, 0.7), (-4.6, 0.88, 0.74)), glo("FF7A2A", 3.5))
    for i, x in enumerate((-6.0, -4.0)):
        add(m, cyl((x, 2.1, 1.05), 0.2, 0.08, "z"), BRASS)
        add(m, cyl((x, 2.1, 1.0), 0.17, 0.04, "z"), col("F6EFE0", 0.4))
        needle = Model(f"Needle{i}")
        needle.add(box((x - 0.012, 2.1, 0.96), (x + 0.012, 2.24, 0.98)), col("C0262D", 0.4))
        anim.append((needle, (x, 2.1, 0.97)))
    # pipes along the back wall and up into the ceiling
    for y, c in ((0.6, "B5835A"), (2.35, "8C979A")):
        add(m, sweep([(-7.5, y, BACK - 0.35), (-2.4, y, BACK - 0.35)], 0.09), met(c, 0.4))
        add(m, sweep([(2.4, y, BACK - 0.35), (7.5, y, BACK - 0.35)], 0.09), met(c, 0.4))
    add(m, sweep([(-3.0, 2.3, 1.7), (-3.0, 2.6, 1.7), (-3.0, ROOM_H, 1.7)], 0.12), met("8C979A", 0.4))
    # valve wheels
    for x in (3.4, 5.2):
        add(m, torus((x, 1.45, BACK - 0.3), 0.26, 0.035, "z"), col("C0262D", 0.4))
        for k in range(4):
            a = math.radians(k * 45)
            add(m, rod((x - 0.24 * math.cos(a), 1.45 - 0.24 * math.sin(a), BACK - 0.3), (x + 0.24 * math.cos(a), 1.45 + 0.24 * math.sin(a), BACK - 0.3), 0.02), col("C0262D", 0.4))
        add(m, rod((x, 1.45, BACK - 0.3), (x, 1.45, BACK - 0.12), 0.05), met("8C979A", 0.4))
    # coal pile and shovel, toolbox
    for k in range(14):
        rr = random.Random(k)
        add(m, sphere((6.2 + rr.uniform(-0.6, 0.6), 0.12 + rr.uniform(0, 0.25), 2.3 + rr.uniform(-0.3, 0.3)), rr.uniform(0.12, 0.2)), col("2A2428", 0.5))
    add(m, box((3.0, 0, 2.2), (3.9, 0.45, 2.7), 0.04), col("C0262D", 0.4))


def greenhouse(m, glow, anim, t):
    # terracotta tiles
    for i in range(-10, 10):
        for j in range(7):
            x0, z0 = i * 0.76, FRONT + j * 0.76
            if abs(x0 + 0.38) < S + 0.2 and z0 > SHAFT_FRONT - 0.7:
                continue
            if (i + j) % 2 == 0:
                add(m, box((max(x0, -W), 0.0, z0), (min(x0 + 0.76, W), 0.012, min(z0 + 0.76, BACK - 0.12))), col("B4623B", 0.3))
    # glass back wall in white frames (overrides wallpaper look)
    for x0, x1 in ((-W, -S - 0.12), (S + 0.12, W)):
        add(m, box((x0, 1.03, BACK - 0.13), (x1, ROOM_H - 0.16, BACK - 0.11)), gls("E8FFF4", 0.4))
        n = int((x1 - x0) / 0.9)
        for k in range(n + 1):
            x = x0 + k * (x1 - x0) / n
            add(m, box((x - 0.03, 1.03, BACK - 0.2), (x + 0.03, ROOM_H - 0.16, BACK - 0.1)), WHITE)
        add(m, box((x0, 1.8, BACK - 0.2), (x1, 1.86, BACK - 0.1)), WHITE)
    # glass roof ribs
    for x in range(-7, 8):
        if abs(x) < 2.5:
            continue
        add(m, box((x - 0.03, ROOM_H - 0.06, FRONT + 0.2), (x + 0.03, ROOM_H, BACK)), WHITE)
    # plant benches with pots
    rng = random.Random(5)
    for x0, x1 in ((-7.2, -2.6), (2.8, 7.2)):
        add(m, box((x0, 0.7, BACK - 1.1), (x1, 0.78, BACK - 0.35)), col("8A5A36", 0.3))
        for x in (x0 + 0.1, x1 - 0.15):
            add(m, box((x, 0, BACK - 1.05), (x + 0.06, 0.7, BACK - 0.4)), col("8A5A36", 0.3))
        x = x0 + 0.35
        while x < x1 - 0.3:
            potted_plant(m, (x, 0.78, BACK - 0.72), rng.uniform(0.55, 0.85), leaf=rng.choice(["3FA34D", "5EBB4A", "2E8B57"]), rng=rng)
            x += rng.uniform(0.55, 0.8)
    # big monstera and a watering can
    potted_plant(m, (-3.2, 0, 0.7), 1.7, leaf="2E8B57")
    potted_plant(m, (-6.9, 0, 0.6), 1.4, pot="F4F4EC", leaf="5EBB4A")
    add(m, lathe([(0.16, 0), (0.17, 0.28), (0.0, 0.28)], (-5.6, 0, -0.4)), met("5E9EA0", 0.5))
    add(m, rod((-5.45, 0.08, -0.4), (-5.1, 0.42, -0.4), 0.025), met("5E9EA0", 0.5))
    add(m, torus((-5.6, 0.36, -0.4), 0.12, 0.018, "x", arc=0.5), met("5E9EA0", 0.5))
    # hanging vines
    for x in (-6.0, -4.0, 4.0, 6.5):
        pts = [(x, ROOM_H, 1.6)]
        for k in range(1, 7):
            pts.append((x + 0.12 * math.sin(k * 1.3), ROOM_H - k * 0.18, 1.6 + 0.08 * math.cos(k)))
        add(m, sweep(pts, 0.025), col("2E8B57", 0.3))
        for k in range(1, 7):
            px, py, pz = pts[k]
            add(m, sphere((px + 0.08, py, pz - 0.05), 0.08, (1, 0.5, 1)), col("5EBB4A", 0.4))
    # a sun disc behind the glass (sunny floor marker)
    add(glow, sphere((5.6, 2.25, BACK + 0.4), 0.35, (1, 1, 0.2)), glo("FFE066", 3.0))


def penthouse(m, glow, anim, t):
    # gold wall panels
    for x0, x1 in ((-W + 0.3, -S - 0.4), (S + 0.4, W - 0.3)):
        n = 3
        for k in range(n):
            a = x0 + k * (x1 - x0) / n + 0.15
            b = x0 + (k + 1) * (x1 - x0) / n - 0.15
            add(m, box((a, 1.15, BACK - 0.13), (b, 2.35, BACK - 0.1)), col("5C3A85", 0.4))
            add(m, box((a - 0.03, 1.12, BACK - 0.12), (b + 0.03, 2.38, BACK - 0.105)), BRASS)
    # tall city-view window in the left wing
    add(m, box((-6.6, 0.4, BACK - 0.14), (-4.2, 2.5, BACK - 0.09)), glo("2B3A6E", 0.8))
    # distant towers and their lit windows through the glass
    rr = random.Random(40)
    for k in range(7):
        bx = -6.5 + k * 0.33
        bh = rr.uniform(0.6, 1.7)
        add(m, box((bx, 0.42, BACK - 0.145), (bx + 0.28, 0.42 + bh, BACK - 0.142)), col("1B2448", 0.2))
        for r in range(int(bh / 0.2)):
            if rr.random() < 0.5:
                wx = bx + rr.choice((0.05, 0.16))
                wy = 0.5 + r * 0.2
                add(glow, box((wx, wy, BACK - 0.15), (wx + 0.07, wy + 0.08, BACK - 0.146)), glo("FFD48A", 2.0))
    add(m, box((-6.7, 0.35, BACK - 0.2), (-4.1, 0.42, BACK - 0.08)), BRASS)
    add(m, box((-5.43, 0.4, BACK - 0.2), (-5.37, 2.5, BACK - 0.08)), BRASS)
    # grand piano
    add(m, hull([(-4.0, 0.7, 0.3), (-2.5, 0.7, 0.3), (-2.5, 0.7, 1.8), (-3.3, 0.7, 2.2), (-4.0, 0.7, 1.6),
                 (-4.0, 1.0, 0.3), (-2.5, 1.0, 0.3), (-2.5, 1.0, 1.8), (-3.3, 1.0, 2.2), (-4.0, 1.0, 1.6)], 0.04), col("141018", 0.85))
    add(m, xf(box((-4.0, 1.0, 0.35), (-2.5, 1.04, 1.9)), rot_about((-4.0, 1.0, 1.0), "Z", -28)), col("141018", 0.85))
    add(m, box((-3.95, 0.72, 0.15), (-2.55, 0.8, 0.32)), WHITE)
    for k in range(10):
        add(m, box((-3.9 + k * 0.14, 0.8, 0.18), (-3.85 + k * 0.14, 0.83, 0.28)), BLACK)
    for x, z in ((-3.9, 0.4), (-2.6, 0.4), (-3.3, 2.0)):
        add(m, cyl((x, 0.35, z), 0.06, 0.7, "y"), col("141018", 0.85))
    add(m, box((-3.6, 0, -0.35), (-2.9, 0.45, -0.05), 0.05), col("141018", 0.85))
    # velvet sofa (back right) and champagne tower on a side table
    add(m, box((3.4, 0.1, 2.2), (6.2, 0.5, 2.85), 0.12), col("B03A6E", 0.25))
    add(m, box((3.4, 0.4, 2.65), (6.2, 1.1, 2.9), 0.14), col("B03A6E", 0.25))
    for x in (3.4, 6.2):
        add(m, box((x - 0.15, 0.1, 2.2), (x + 0.15, 0.75, 2.9), 0.12), col("8E2E58", 0.25))
    table(m, (6.8, 0, 1.6), 0.7, 0.7, 0.8, "141018")
    for lvl, n in enumerate((4, 3, 2, 1)):
        for k in range(n):
            x = 6.8 - (n - 1) * 0.07 + k * 0.14
            y = 0.8 + lvl * 0.15
            add(m, lathe([(0.02, 0), (0.02, 0.06), (0.06, 0.08), (0.06, 0.14), (0.0, 0.14)], (x, y, 1.6)), gls("FFE9A8", 0.6))
    # crystal chandelier
    for x in (-4.9, 4.9):
        for k in range(8):
            a = math.radians(k * 45)
            add(glow, sphere((x + 0.5 * math.cos(a), ROOM_H - 0.85, 0.8 + 0.5 * math.sin(a)), 0.045), glo("FFF2D6", 3.0))
        add(m, torus((x, ROOM_H - 0.8, 0.8), 0.5, 0.02, "y"), BRASS)
    # marble bust on a plinth
    add(m, box((1.0 + 1.6, 0, 2.3), (1.0 + 2.2, 1.0, 2.8), 0.03), col("F4F0EA", 0.6))
    add(m, sphere((2.9, 1.25, 2.55), 0.2, (1, 1.15, 1)), col("F4F0EA", 0.6))
    add(m, sphere((2.9, 1.05, 2.55), 0.26, (1.2, 0.5, 0.8)), col("F4F0EA", 0.6))


def crypt(m, glow, anim, t):
    rng = random.Random(8)
    # stone block wall facing over the wallpaper
    for x0, x1 in ((-W, -S - 0.12), (S + 0.12, W)):
        y = 0
        row = 0
        while y < ROOM_H - 0.05:
            x = x0 + (0.3 if row % 2 else 0)
            hh = 0.42
            while x < x1:
                w = rng.uniform(0.55, 0.85)
                add(m, box((x + 0.02, y + 0.02, BACK - 0.2), (min(x + w, x1) - 0.02, min(y + hh, ROOM_H) - 0.02, BACK - 0.11), 0.03),
                    col(shade("5A6270", rng.uniform(0.85, 1.1)), 0.15))
                x += w
            y += hh
            row += 1
    # flagstones
    for i in range(-10, 10):
        for j in range(7):
            x0, z0 = i * 0.76, FRONT + j * 0.76
            if abs(x0 + 0.38) < S + 0.2 and z0 > SHAFT_FRONT - 0.7:
                continue
            add(m, box((max(x0, -W) + 0.03, 0.0, z0 + 0.03), (min(x0 + 0.76, W) - 0.03, 0.02, min(z0 + 0.76, BACK - 0.12) - 0.03)),
                col(shade("4A4F58", rng.uniform(0.85, 1.12)), 0.2))

    def coffin(x, z, ang, open_=False):
        prof = [(-0.32, 0), (0.32, 0), (0.42, 0.6), (0.25, 1.95), (-0.25, 1.95), (-0.42, 0.6)]
        pts = [(px, 0.0, pz) for px, pz in prof] + [(px, 0.45, pz) for px, pz in prof]
        r = Matrix.Translation(V(x, 0, z)) @ Matrix.Rotation(math.radians(ang), 4, "Y")
        add(m, hull(pts, 0.03), col("3E2A26", 0.4), transform=r)
        lid = [(px * 1.04, 0.45, pz) for px, pz in prof] + [(px * 1.04, 0.55, pz) for px, pz in prof]
        lt = r @ (rot_about((0.42, 0.45, 0.6), "Z", -25) if open_ else Matrix.Identity(4))
        add(m, hull(lid, 0.03), col("5A3A30", 0.45), transform=lt)
        add(m, box((-0.04, 0.55, 0.5), (0.04, 0.57, 1.3)), BRASS, transform=lt)
        add(m, box((-0.2, 0.55, 0.86), (0.2, 0.57, 0.94)), BRASS, transform=lt)

    coffin(-6.6, 0.6, 90)
    coffin(-4.4, 1.4, 80, open_=True)
    coffin(4.2, 2.3, 90)
    # candelabras with green flames
    for x, z in ((-7.1, 2.4), (-2.8, 2.5), (6.9, 1.6)):
        add(m, lathe([(0.15, 0), (0.05, 0.08), (0.03, 1.1), (0.0, 1.1)], (x, 0, z)), BRASS_DARK)
        for dx in (-0.22, 0, 0.22):
            add(m, rod((x, 1.0, z), (x + dx, 1.15, z), 0.015), BRASS_DARK)
            add(m, cyl((x + dx, 1.25, z), 0.035, 0.2, "y"), col("F2EAD8", 0.3))
            add(glow, sphere((x + dx, 1.42, z), 0.05, (1, 1.6, 1)), glo("7CFFB2", 4.0))
    # cobwebs in the corners
    for x, s in ((-W + 0.05, 1), (W - 0.05, -1)):
        for k in range(5):
            a = math.radians(k * 22.5)
            add(m, rod((x, ROOM_H - 0.05, BACK - 0.15), (x + s * 0.9 * math.cos(a), ROOM_H - 0.05 - 0.9 * math.sin(a), BACK - 0.15), 0.006), col("E8E8F0", 0.2))
        for r in (0.3, 0.55, 0.8):
            pts = [(x + s * r * math.cos(math.radians(k * 22.5)), ROOM_H - 0.05 - r * math.sin(math.radians(k * 22.5)), BACK - 0.15) for k in range(5)]
            add(m, sweep(pts, 0.005, caps=False), col("E8E8F0", 0.2))
    # skull on a shelf, stone arch
    add(m, box((5.4, 1.3, BACK - 0.45), (6.6, 1.38, BACK - 0.12)), col("767E8A", 0.2))
    add(m, sphere((6.0, 1.52, BACK - 0.3), 0.15, (1, 0.9, 1)), col("EDE6D6", 0.3))
    add(m, sphere((5.95, 1.53, BACK - 0.44), 0.035), BLACK)
    add(m, sphere((6.05, 1.53, BACK - 0.44), 0.035), BLACK)


def ocean(m, glow, anim, t):
    """No room at all: open sea to the horizon, a little pier at the elevator."""
    # sea bed + water volume (the slab is water)
    add(m, box((-W - 0.3, -SLAB, FRONT), (W + 0.3, -0.25, BACK + 3)), col("0E4F7A", 0.3))
    add(m, box((-W - 0.3, -0.25, FRONT), (W + 0.3, -0.02, BACK + 3)), gls("1F86D0", 0.82))
    for i in range(7):
        wave = Model(f"Wave{i}")
        z = FRONT + 0.35 + i * 0.78
        x0 = -W - 0.3 + (0.6 if i % 2 else 0)
        crest = [(x0, 0.0, z)]
        x = x0
        rr = random.Random(i)
        while x < W + 0.3:
            x = min(W + 0.3, x + rr.uniform(0.6, 1.1))
            crest.append((x, rr.uniform(-0.01, 0.06), z + rr.uniform(-0.06, 0.06)))
        wave.add(sweep(crest, 0.07 - i * 0.004), col("EAF8FF", 0.6))
        wave.add(sweep([(p[0], p[1] - 0.08, p[2] + 0.12) for p in crest], 0.11 - i * 0.006), gls(L.mix("5BC0F0", "2E8BD0", i / 7), 0.85))
        anim.append((wave, (0, 0.0, z)))
    # horizon backdrop: sky and a far sea line
    add(m, box((-W - 0.3, 0.3, BACK + 2.9), (W + 0.3, ROOM_H + 0.3, BACK + 3.0)), col("9FD8F0", 0.1))
    add(m, box((-W - 0.3, 0.2, BACK + 2.85), (W + 0.3, 0.55, BACK + 2.95)), col("1F86D0", 0.3))
    add(glow, sphere((-5.0, 2.1, BACK + 2.8), 0.45, (1, 1, 0.15)), glo("FFE9A0", 3.0))
    # clouds
    for x, y in ((-2.5, 2.2), (4.5, 2.4), (6.6, 1.9)):
        for k in range(3):
            add(m, sphere((x + k * 0.35, y + (0.1 if k == 1 else 0), BACK + 2.7), 0.28, (1, 0.7, 0.3)), col("FFFFFF", 0.1))
    # pier from the shaft to the right (swimmers wait here)
    add(m, box((-S - 0.2, 0.14, FRONT + 0.1), (6.8, 0.26, -0.2)), col("A8784F", 0.25))
    for x in range(-2, 7):
        add(m, box((x - 0.02, 0.262, FRONT + 0.1), (x + 0.02, 0.265, -0.2)), col("6B4A2E", 0.3))
        for z in (FRONT + 0.25, -0.35):
            add(m, cyl((x + 0.5, -0.15, z), 0.09, 0.8, "y"), col("6B4A2E", 0.3))
    for x in (6.6, 2.4):
        add(m, cyl((x, 0.5, -0.3), 0.08, 1.0, "y"), col("6B4A2E", 0.3))
    add(m, torus((6.6, 0.7, -0.42), 0.26, 0.07, "z"), col("E5484D", 0.4))
    # buoy, beach ball, rock with a gull
    add(m, lathe([(0.0, 0), (0.3, 0.05), (0.32, 0.4), (0.12, 0.7), (0.0, 0.75)], (-5.2, -0.1, 1.2)), col("E5484D", 0.4))
    add(m, cyl((-5.2, 0.28, 1.2), 0.325, 0.12, "y"), WHITE)
    add(glow, sphere((-5.2, 0.8, 1.2), 0.06), glo("FFDD55", 3.0))
    add(m, sphere((-3.2, 0.3, 0.2), 0.22), col("FFD23F", 0.5))
    add(m, sphere((3.4, 0.0, 1.9), 0.6, (1.4, 0.7, 1.0)), col("6F6A66", 0.2))
    add(m, sphere((3.5, 0.55, 1.85), 0.1, (1.6, 0.8, 0.8)), WHITE)
    add(m, sphere((3.62, 0.6, 1.85), 0.06), WHITE)


def daycare(m, glow, anim, t):
    rng = random.Random(10)
    pastel = ["FFB5C8", "B5E3FF", "FFF0A8", "C8F5C0", "E1C8FF"]
    for i in range(-10, 10):
        for j in range(7):
            x0, z0 = i * 0.76, FRONT + j * 0.76
            if abs(x0 + 0.38) < S + 0.2 and z0 > SHAFT_FRONT - 0.7:
                continue
            add(m, box((max(x0, -W) + 0.01, 0.0, z0 + 0.01), (min(x0 + 0.76, W) - 0.01, 0.015, min(z0 + 0.76, BACK - 0.12) - 0.01), 0.005),
                col(pastel[(i * 3 + j) % 5], 0.2))
    # polka dots on the wall
    for k in range(40):
        x = rng.uniform(-W + 0.3, W - 0.3)
        if abs(x) < S + 0.5:
            continue
        add(m, cyl((x, rng.uniform(1.2, 2.4), BACK - 0.105), 0.07, 0.01, "z"), col(rng.choice(["F06EAA", "FFD23F", "3FA9F5"]), 0.3))
    # ball pit
    add(m, box((-7.2, 0, 0.6), (-4.4, 0.55, 2.6), 0.08), col("3FA9F5", 0.4))
    add(m, box((-7.05, 0.2, 0.75), (-4.55, 0.5, 2.45)), col("FFE3EE", 0.2))
    for k in range(70):
        add(m, sphere((rng.uniform(-6.95, -4.65), rng.uniform(0.45, 0.62), rng.uniform(0.85, 2.35)), 0.11, seg=10, rings=6),
            col(rng.choice(["E5484D", "FFD23F", "3FA9F5", "6CCB5F", "F06EAA"]), 0.6))
    # rocking horse
    add(m, torus((-3.1, 0.35, 0.3), 0.6, 0.05, "z", arc=0.5, scale=(1, 0.4, 1)), col("C8643C", 0.4), transform=rot_about((-3.1, 0.35, 0.3), "Z", 180))
    add(m, sphere((-3.1, 0.75, 0.3), 0.32, (1.4, 0.8, 0.7)), col("F2F2EA", 0.3))
    add(m, sphere((-2.62, 1.1, 0.3), 0.18, (1.2, 0.9, 0.7)), col("F2F2EA", 0.3))
    add(m, rod((-2.8, 0.85, 0.3), (-2.65, 1.05, 0.3), 0.1), col("F2F2EA", 0.3))
    add(m, sphere((-3.1, 1.0, 0.3), 0.16, (1.2, 0.3, 0.8)), col("E5484D", 0.4))
    # giant blocks
    letters = [("E5484D", 3.0, 2.4), ("FFD23F", 3.7, 2.4), ("3FA9F5", 3.35, 2.1)]
    for c, x, z in letters[:2]:
        add(m, box((x - 0.3, 0, z - 0.3), (x + 0.3, 0.6, z + 0.3), 0.05), col(c, 0.4))
    add(m, box((3.05, 0.6, 2.1), (3.65, 1.2, 2.7), 0.05), col("3FA9F5", 0.4))
    # little slide
    add(m, box((5.4, 0, 2.2), (6.0, 1.3, 2.8), 0.04), col("6CCB5F", 0.4))
    add(m, xf(box((6.0, 1.2, 2.25), (7.6, 1.28, 2.75), 0.03), rot_about((6.0, 1.24, 2.5), "Z", -38)), col("FFD23F", 0.5))
    # crayon drawings
    for k, x in enumerate((-6.6, -5.6, 3.4, 4.6, 6.4)):
        add(m, box((x - 0.35, 1.35, BACK - 0.12), (x + 0.35, 1.85, BACK - 0.1)), WHITE)
        add(m, sphere((x - 0.1, 1.65, BACK - 0.125), 0.1, (1, 1, 0.05)), col("FFD23F", 0.3))
        add(m, box((x - 0.25, 1.42, BACK - 0.125), (x + 0.25, 1.5, BACK - 0.12)), col(rng.choice(["6CCB5F", "3FA9F5", "E5484D"]), 0.3))


BUILDERS = {
    "Lobby": lobby, "Office": office, "Library": library, "Laundromat": laundromat, "Boiler": boiler,
    "Greenhouse": greenhouse, "Penthouse": penthouse, "Crypt": crypt, "Ocean": ocean, "Daycare": daycare,
}


def build_floor(name):
    t = THEMES[name]
    root = empty(f"Floor_{name}")
    m = Model("Shell")
    glow = Model("Glow")
    anim = []
    shell(m, t, name, glow, walls=(name != "Ocean"))
    BUILDERS[name](m, glow, anim, t)
    m.build(parent=root)
    if not glow.empty():
        glow.build(parent=root)
    for model, pivot in anim:
        model.build(origin=pivot, parent=root)
    return root


def parse():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = {"only": None, "preview": None, "export": True}
    i = 0
    while i < len(argv):
        if argv[i] == "--only":
            opts["only"] = argv[i + 1].split(",")
            i += 1
        elif argv[i] == "--preview":
            opts["preview"] = argv[i + 1]
            i += 1
        elif argv[i] == "--no-export":
            opts["export"] = False
        i += 1
    return opts


def main():
    opts = parse()
    names = opts["only"] or list(BUILDERS.keys())
    for name in names:
        reset_scene()
        root = build_floor(name)
        if opts["export"]:
            export_fbx(os.path.join(OUT, f"Floor_{name}.fbx"), [root])
            print("exported", name)
        if opts["preview"]:
            setup_preview()
            render_views(os.path.join(opts["preview"], f"Floor_{name}.png"), [root], size=(900, 420), views=((10, 9),), lens=50, margin=0.62)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "blend", f"Floor_{name}.blend")) if opts["export"] else None


if __name__ == "__main__":
    os.makedirs(os.path.join(ROOT, "ArtSource", "blend"), exist_ok=True)
    main()
