"""The guests (and the operator) of The Shuffleton: chunky "peg people" with one silhouette prop each.

    blender -b -P ArtSource/build_characters.py -- [--only Vampire,Kid] [--preview DIR] [--no-export]

Authored in Unity space: feet at y=0, facing +z (Unity forward). Parts that animate in code are
separate objects: Head (pivot at the neck), Cape, Mirror, Balloons, Propeller, Leaves/Leaf0..N.
Exported to Assets/Resources/Models/Char_<Name>.fbx.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from omf_lib import *  # noqa: E402,F401,F403

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models")

SKIN = "F2C9A0"
NECK = 1.32


def peg_body(m, c, h=1.36, r=0.38, waist=0.35, shoulder=0.3):
    """Rounded peg: flat-ish base, slight waist, rounded shoulders."""
    prof = [(0.0, 0.0), (r * 0.82, 0.0), (r, 0.12), (r, h * 0.35), (waist + 0.02, h * 0.55), (r * 0.98, h * 0.78),
            (shoulder + 0.06, h * 0.93), (shoulder - 0.06, h), (0.0, h + 0.02)]
    m.add(lathe(prof, (0, 0, 0), seg=28), c)


def stub_arms(m, c, y=1.0, x=0.4, down=0.42, hand=SKIN, spread=0.08, forward=0.0):
    for s in (-1, 1):
        p0 = V(s * x, y, 0.0)
        p1 = V(s * (x + spread), y - down, forward)
        m.add(rod(p0, p1, 0.1, seg=12), c)
        m.add(sphere(p0, 0.1, seg=12, rings=8), c)
        m.add(sphere(p1, 0.105, seg=12, rings=8), col(hand, 0.3))


def face(m, y, z=0.34, spread=0.13, size=0.085, look=(0, 0), brows=None, mouth=True, cheeks=True):
    """Big friendly eyes on the +z side of a head centred at (0, y, 0)."""
    for s in (-1, 1):
        cx = s * spread
        m.add(sphere((cx, y + 0.02, z), size, (0.85, 1.15, 0.6), seg=14, rings=10), WHITE)
        m.add(sphere((cx + look[0] * 0.02, y + 0.0 + look[1] * 0.02, z + 0.045), size * 0.55, (1, 1.1, 0.6), seg=12, rings=8), col("1C1820", 0.7))
        m.add(sphere((cx + 0.018, y + 0.035, z + 0.07), size * 0.18, seg=8, rings=6), WHITE)
        if brows:
            ang = brows * s
            m.add(boxc((cx, y + 0.15, z - 0.01), (0.12, 0.025, 0.04), 0.008), col("2A1E2E", 0.3),
                  transform=rot_about((cx, y + 0.15, z), "Z", ang))
        if cheeks:
            m.add(sphere((s * 0.21, y - 0.09, z - 0.06), 0.055, (1, 0.6, 0.4), seg=10, rings=6), col("F29A8A", 0.2))
    if mouth:
        m.add(torus((0, y - 0.1, z - 0.02), 0.05, 0.012, "z", arc=0.5, seg=12, ring_seg=6), col("6E2A33", 0.3),
              transform=rot_about((0, y - 0.1, z - 0.02), "Z", 180))


def head(name, root, y=1.72, r=0.36, skin=SKIN):
    h = Model(name)
    h.add(sphere((0, y, 0), r, (1.0, 0.98, 0.96), seg=28, rings=18), col(skin, 0.3))
    return h


# ----------------------------------------------------------------------------- characters

def commuter(root):
    suit = "3D5A80"
    b = Model("Body")
    peg_body(b, col(suit, 0.3))
    b.add(prism([(-0.13, 1.33), (0.13, 1.33), (0.0, 0.95)], 0.3, 0.37), WHITE)
    b.add(prism([(-0.045, 1.28), (0.045, 1.28), (0.06, 0.92), (0.0, 0.85), (-0.06, 0.92)], 0.34, 0.39), col("C0262D", 0.4))
    stub_arms(b, col(suit, 0.3), forward=0.05)
    # briefcase hanging from the right hand
    b.add(box((0.36, 0.22, -0.2), (0.48, 0.58, 0.24), 0.03), col("6B3F22", 0.45))
    b.add(torus((0.42, 0.6, 0.02), 0.08, 0.018, "x", arc=0.5), col("2A1E2E", 0.4))
    b.add(box((0.47, 0.4, -0.05), (0.49, 0.44, 0.09)), BRASS)
    b.build(parent=root)
    h = head("Head", root)
    face(h, 1.74)
    h.add(sphere((0, 1.86, -0.03), 0.37, (1.02, 0.62, 1.02), seg=24, rings=12), col("3A2618", 0.4))
    h.add(sphere((0.12, 1.98, 0.18), 0.14, (1.4, 0.5, 1), seg=12, rings=8), col("3A2618", 0.4))
    h.build(origin=(0, NECK, 0), parent=root)


def houseplant(root):
    b = Model("Body")
    pot = "C8643C"
    b.add(lathe([(0.0, 0), (0.3, 0), (0.33, 0.06), (0.4, 0.62), (0.46, 0.66), (0.46, 0.78), (0.0, 0.78)], (0, 0, 0), seg=28), col(pot, 0.25))
    b.add(cyl((0, 0.78, 0), 0.41, 0.03, "y"), col("4A3020", 0.1))
    b.add(torus((0, 0.66, 0), 0.44, 0.03, "y"), col("A84E2C", 0.25))
    face(b, 0.4, z=0.37, spread=0.15, size=0.09)
    # a little bow of raffia, for character
    b.add(sphere((0.3, 0.66, 0.3), 0.06), col("E8D27A", 0.2))
    b.build(parent=root)
    # Head = invisible pivot used for bubbles; leaves animate individually
    head_pivot = empty("Head", (0, 1.65, 0), parent=root)
    leaves = empty("Leaves", (0, 0.78, 0), parent=root)
    for i in range(7):
        a = i * (360 / 7) + 10
        tilt = 22 + 12 * (i % 3)
        length = 0.75 + 0.15 * ((i * 37) % 3) / 2
        leaf = Model(f"Leaf{i}")
        d = V(math.sin(math.radians(a)) * math.sin(math.radians(tilt)), math.cos(math.radians(tilt)),
              math.cos(math.radians(a)) * math.sin(math.radians(tilt)))
        base = V(0, 0.78, 0)
        tip = base + d * length
        leaf.add(rod(base, tip, 0.022, seg=8), col("2C7A3B", 0.4))
        # monstera blade: flattened sphere with notches suggested by two smaller lobes
        side = d.cross(V(0, 1, 0)).normalized() if abs(d.y) < 0.99 else V(1, 0, 0)
        blade_c = tip + d * 0.12
        leaf.add(sphere(blade_c, 0.24, (1, 1, 1), seg=16, rings=10), col("3FA34D" if i % 2 else "35914A", 0.45),
                 transform=Matrix.Translation(blade_c) @ (V(0, 0, 1).rotation_difference(d).to_matrix().to_4x4())
                 @ Matrix.Diagonal(V(1.0, 0.18, 1.25, 1)) @ Matrix.Translation(-blade_c))
        leaf.build(origin=(0, 0.78, 0), parent=leaves, parent_origin=(0, 0.78, 0))


def mirror_mover(root):
    over = "2F5FA8"
    b = Model("Body")
    peg_body(b, col("E8D9B8", 0.3))
    b.add(lathe([(0.0, 0.0), (0.39, 0.0), (0.4, 0.12), (0.4, 0.75), (0.0, 0.76)], (0, 0, 0), seg=28), col(over, 0.25))
    b.add(box((-0.25, 0.7, 0.3), (0.25, 1.05, 0.39), 0.03), col(over, 0.25))
    for s in (-1, 1):
        b.add(box((s * 0.2 - 0.04, 0.95, -0.36), (s * 0.2 + 0.04, 1.32, 0.38)), col(over, 0.25))
        b.add(sphere((s * 0.2, 1.02, 0.39), 0.035), BRASS)
    stub_arms(b, col("E8D9B8", 0.3), spread=0.12)
    b.build(parent=root)
    h = head("Head", root, skin="D9A066")
    face(h, 1.74)
    h.add(sphere((0, 1.88, 0), 0.37, (1.02, 0.55, 1.02), seg=24, rings=12), col("E04E39", 0.35))
    h.add(box((-0.22, 1.86, 0.2), (0.22, 1.9, 0.55), 0.02), col("E04E39", 0.35))
    h.add(sphere((0, 1.62, 0.36), 0.05), col("C98A5A", 0.3))
    h.build(origin=(0, NECK, 0), parent=root)
    # the giant gilded mirror, standing beside them
    mm = Model("Mirror")
    cx, cy = 0.82, 0.98
    mm.add(torus((cx, cy, 0.0), 0.38, 0.065, "z", scale=(1.0, 2.05, 1.0), seg=36), met("D7A84A", 0.75))
    mm.add(sphere((cx, cy, -0.01), 0.38, (0.95, 2.0, 0.06), seg=24, rings=14), met("DDEBF2", 0.97))
    mm.add(sphere((cx, cy + 0.92, 0.0), 0.09), met("D7A84A", 0.75))
    mm.add(prism([(cx - 0.12, cy + 0.84), (cx + 0.12, cy + 0.84), (cx, cy + 1.0)], -0.03, 0.03), met("D7A84A", 0.75))
    mm.add(sphere((cx - 0.13, cy + 0.3, 0.05), 0.06, (1, 2.4, 0.3)), col("FFFFFF", 0.9))
    mm.add(sphere((cx + 0.1, cy - 0.2, 0.05), 0.035, (1, 1.8, 0.3)), col("FFFFFF", 0.9))
    mm.build(origin=(cx, 0, 0), parent=root)


def vampire(root):
    b = Model("Body")
    peg_body(b, col("1C1820", 0.45), h=1.42, r=0.36)
    b.add(prism([(-0.14, 1.38), (0.14, 1.38), (0.0, 0.85)], 0.28, 0.35), WHITE)
    b.add(boxc((0, 1.28, 0.36), (0.16, 0.06, 0.04), 0.01), col("B3122E", 0.5))
    stub_arms(b, col("1C1820", 0.45), hand="E8E4EE", spread=0.05)
    b.build(parent=root)
    h = head("Head", root, y=1.78, r=0.34, skin="E8E4EE")
    face(h, 1.8, z=0.32, brows=-18, mouth=False, cheeks=False, size=0.08)
    for s in (-1, 1):
        h.add(rod((s * 0.05, 1.67, 0.3), (s * 0.05, 1.6, 0.3), 0.022, radius2=0.002, seg=8), WHITE)
    h.add(torus((0, 1.68, 0.31), 0.06, 0.012, "z", arc=0.5, seg=12, ring_seg=6), col("6E2A33", 0.3), transform=rot_about((0, 1.68, 0.31), "Z", 180))
    h.add(sphere((0, 1.9, -0.04), 0.35, (1.02, 0.6, 1.02), seg=24, rings=12), col("16121A", 0.75))
    h.add(prism([(-0.09, 2.08), (0.09, 2.08), (0.0, 1.95)], 0.2, 0.33), col("16121A", 0.75))
    h.build(origin=(0, NECK + 0.06, 0), parent=root)
    cape = Model("Cape")
    # high collar fans and a long cape behind (red lining shows inside)
    for s in (-1, 1):
        cape.add(prism([(s * 0.12, 1.36), (s * 0.48, 1.36), (s * 0.55, 1.98), (s * 0.2, 1.72)], -0.12, -0.06), col("16121A", 0.55))
        cape.add(prism([(s * 0.14, 1.38), (s * 0.45, 1.38), (s * 0.5, 1.9), (s * 0.2, 1.7)], -0.06, -0.04), col("B3122E", 0.5))
    cape.add(prism([(-0.42, 1.38), (0.42, 1.38), (0.56, 0.05), (-0.56, 0.05)], -0.32, -0.26), col("16121A", 0.55))
    cape.add(prism([(-0.38, 1.36), (0.38, 1.36), (0.5, 0.08), (-0.5, 0.08)], -0.26, -0.24), col("B3122E", 0.5))
    cape.build(origin=(0, 1.38, -0.2), parent=root)


def courier(root):
    uni = "8A5A2B"
    b = Model("Body")
    peg_body(b, col(uni, 0.3))
    b.add(rod((-0.3, 1.25, 0.25), (0.35, 0.55, 0.3), 0.035), col("4E2E1C", 0.3))
    b.add(box((0.28, 0.4, -0.05), (0.46, 0.68, 0.25), 0.03), col("4E2E1C", 0.3))
    stub_arms(b, col(uni, 0.3), y=1.02, down=0.3, spread=-0.12, forward=0.32)
    # the urgent parcel held out front
    b.add(box((-0.33, 0.62, 0.38), (0.33, 1.02, 0.86), 0.03), col("C8955A", 0.2))
    b.add(box((-0.34, 0.8, 0.37), (0.34, 0.84, 0.87)), col("E8D27A", 0.3))
    b.add(box((-0.03, 1.01, 0.37), (0.03, 1.03, 0.87)), col("E8D27A", 0.3))
    b.add(box((-0.22, 0.66, 0.86), (0.12, 0.78, 0.88)), col("E5484D", 0.4))
    b.build(parent=root)
    h = head("Head", root, skin="A8714A")
    face(h, 1.74)
    h.add(sphere((0, 1.87, 0), 0.37, (1.02, 0.5, 1.02), seg=24, rings=12), col("6B4423", 0.35))
    h.add(box((-0.2, 1.84, 0.2), (0.2, 1.88, 0.52), 0.02), col("6B4423", 0.35))
    h.add(cyl((0, 1.98, 0), 0.05, 0.06, "y"), col("FFD23F", 0.4))
    h.build(origin=(0, NECK, 0), parent=root)


def swimmer(root):
    b = Model("Body")
    peg_body(b, col("F2C9A0", 0.35))
    for k in range(4):
        y0 = 0.12 + k * 0.22
        b.add(lathe([(0.392, y0), (0.395, y0 + 0.11), (0.0, y0 + 0.11)], (0, 0, 0), seg=28), col("E5484D" if k % 2 == 0 else "F7F2EA", 0.5))
    stub_arms(b, col("F2C9A0", 0.35), hand="F2C9A0")
    # towel over the left shoulder
    b.add(box((-0.42, 0.75, -0.3), (-0.22, 1.42, 0.3), 0.06), col("3FA9F5", 0.1))
    b.add(box((-0.43, 0.78, -0.31), (-0.21, 0.86, 0.31)), col("F7F2EA", 0.1))
    b.build(parent=root)
    h = head("Head", root, skin="F2C9A0")
    face(h, 1.72, look=(0, -1))
    h.add(sphere((0, 1.8, -0.02), 0.375, (1.02, 0.78, 1.02), seg=24, rings=14), col("F7F2EA", 0.6))
    h.add(torus((0, 1.86, 0), 0.33, 0.03, "y", seg=24), col("3FA9F5", 0.5))
    for s in (-1, 1):
        h.add(torus((s * 0.13, 1.93, 0.31), 0.075, 0.025, "z", seg=16), col("2C8BD6", 0.6))
        h.add(sphere((s * 0.13, 1.93, 0.31), 0.065, (1, 1, 0.4)), gls("9FE1FF", 0.6))
    h.add(box((-0.08, 1.92, 0.29), (0.08, 1.94, 0.33)), col("2C8BD6", 0.6))
    h.build(origin=(0, NECK, 0), parent=root)


def kid(root):
    b = Model("Body")
    peg_body(b, col("F07F3C", 0.3), h=1.0, r=0.3, waist=0.27, shoulder=0.24)
    for k in range(3):
        y0 = 0.3 + k * 0.2
        b.add(lathe([(0.305, y0), (0.307, y0 + 0.07), (0.0, y0 + 0.07)], (0, 0, 0), seg=24), col("FFD23F", 0.3))
    stub_arms(b, col("F07F3C", 0.3), y=0.78, x=0.31, down=0.32)
    # balloon strings gathered in the right hand
    b.build(parent=root)
    h = head("Head", root, y=1.32, r=0.34)
    face(h, 1.34, size=0.095, spread=0.12)
    h.add(sphere((0, 1.42, 0), 0.355, (1.02, 0.7, 1.02), seg=24, rings=14), col("3B7DD8", 0.35))
    h.add(cyl((0, 1.68, 0), 0.03, 0.12, "y"), col("FFD23F", 0.4))
    h.build(origin=(0, 0.98, 0), parent=root)
    prop = Model("Propeller")
    prop.add(boxc((0, 1.76, 0), (0.62, 0.025, 0.1), 0.01), col("E5484D", 0.4))
    prop.add(boxc((0, 1.76, 0), (0.1, 0.03, 0.62), 0.01), col("6CCB5F", 0.4))
    prop.build(origin=(0, 1.76, 0), parent=root)
    bal = Model("Balloons")
    hand = V(0.4, 0.46, 0.0)
    for i, (c, off) in enumerate((("E5484D", (-0.25, 1.75, 0.05)), ("FFD23F", (0.12, 1.95, -0.05)), ("3FA9F5", (0.42, 1.68, 0.08)))):
        p = V(off)
        bal.add(sphere(p, 0.24, (0.92, 1.1, 0.92), seg=18, rings=12), col(c, 0.75))
        bal.add(lathe([(0.0, 0), (0.05, 0.02), (0.0, 0.06)], p - V(0, 0.3, 0), seg=10), col(c, 0.75))
        bal.add(rod(hand, p - V(0, 0.27, 0), 0.006, seg=6), col("2A1E2E", 0.3))
        bal.add(sphere(p + V(-0.08, 0.1, 0.18), 0.05, (1, 1.4, 0.5)), col("FFFFFF", 0.9))
    bal.build(origin=tuple(hand), parent=root)


def tycoon(root):
    suit = "4B2E6E"
    b = Model("Body")
    peg_body(b, col(suit, 0.35), h=1.36, r=0.46, waist=0.47, shoulder=0.32)
    b.add(prism([(-0.13, 1.33), (0.13, 1.33), (0.0, 0.98)], 0.36, 0.44), WHITE)
    b.add(boxc((0, 1.3, 0.42), (0.2, 0.08, 0.04), 0.02), col("E5484D", 0.4))
    for s in (-1, 1):
        b.add(sphere((s * 0.05, 1.3, 0.43), 0.05), col("E5484D", 0.4))
    b.add(sweep([(-0.25, 0.85, 0.44), (-0.1, 0.78, 0.47), (0.1, 0.8, 0.47), (0.24, 0.86, 0.44)], 0.012), BRASS)
    b.add(cyl((0.26, 0.85, 0.43), 0.05, 0.02, "z"), BRASS)
    stub_arms(b, col(suit, 0.35), x=0.48, hand="F7F2EA", spread=0.06)
    b.add(rod((0.56, 0.0, 0.18), (0.56, 0.62, 0.12), 0.025), col("1C1820", 0.7))
    b.add(sphere((0.56, 0.64, 0.12), 0.05), BRASS)
    b.build(parent=root)
    h = head("Head", root, skin="F5D5BA")
    face(h, 1.76, brows=10, mouth=False)
    h.add(sphere((0, 1.63, 0.3), 0.06, (3.0, 0.7, 1)), col("F7F2EA", 0.3))
    h.add(sphere((-0.15, 1.62, 0.28), 0.07, (1.6, 0.6, 0.8)), col("F7F2EA", 0.3))
    h.add(sphere((0.15, 1.62, 0.28), 0.07, (1.6, 0.6, 0.8)), col("F7F2EA", 0.3))
    h.add(torus((0.13, 1.76, 0.36), 0.075, 0.012, "z", seg=16), BRASS)
    h.add(rod((0.2, 1.73, 0.35), (0.3, 1.4, 0.3), 0.004), BRASS)
    h.add(cyl((0, 2.25, 0), 0.25, 0.48, "y"), col("16121A", 0.6))
    h.add(cyl((0, 2.03, 0), 0.4, 0.04, "y", bevel=0.01), col("16121A", 0.6))
    h.add(cyl((0, 2.08, 0), 0.255, 0.08, "y"), col("E5484D", 0.4))
    h.build(origin=(0, NECK, 0), parent=root)


def bellhop(root):
    red = "C0262D"
    b = Model("Body")
    peg_body(b, col(red, 0.35))
    for k in range(4):
        b.add(sphere((0, 1.18 - k * 0.2, 0.375 - 0.0 * k), 0.035), BRASS)
    for s in (-1, 1):
        b.add(boxc((s * 0.3, 1.33, 0), (0.2, 0.06, 0.3), 0.02), BRASS)
        b.add(sweep([(s * 0.22, 1.3, 0.25), (s * 0.3, 1.1, 0.33), (s * 0.12, 0.98, 0.37)], 0.012), BRASS)
    b.add(lathe([(0.0, 0.0), (0.385, 0.0), (0.39, 0.1), (0.0, 0.1)], (0, 0, 0), seg=28), col("1C1820", 0.5))
    stub_arms(b, col(red, 0.35), hand="F7F2EA")
    b.build(parent=root)
    h = head("Head", root)
    face(h, 1.73)
    h.add(sphere((0, 1.8, -0.06), 0.36, (1.02, 0.6, 1.0), seg=24, rings=12), col("8A4B2A", 0.4))
    h.add(cyl((0.06, 2.08, 0), 0.2, 0.2, "y", bevel=0.02), col(red, 0.4))
    h.add(cyl((0.06, 2.0, 0), 0.205, 0.05, "y"), BRASS)
    h.add(sphere((0.06, 2.19, 0), 0.04), BRASS)
    h.add(torus((0, 1.7, 0.0), 0.36, 0.01, "x", arc=0.5, seg=16), col("1C1820", 0.4), transform=rot_about((0, 1.7, 0), "X", 90))
    h.build(origin=(0, NECK, 0), parent=root)


BUILDERS = {
    "Commuter": commuter, "Houseplant": houseplant, "Mirror": mirror_mover, "Vampire": vampire, "Courier": courier,
    "Swimmer": swimmer, "Kid": kid, "Tycoon": tycoon, "Bellhop": bellhop,
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(BUILDERS)
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    export = "--no-export" not in argv
    roots = []
    for name in only:
        reset_scene()
        root = empty(f"Char_{name}")
        BUILDERS[name](root)
        if export:
            export_fbx(os.path.join(OUT, f"Char_{name}.fbx"), [root])
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "blend", f"Char_{name}.blend"))
            print("exported", name)
        if preview:
            setup_preview()
            # front 3/4 view (Unity camera looks +z at their faces, so render from +z side)
            render_views(os.path.join(preview, f"Char_{name}.png"), [root], size=(360, 480), views=((8, 200),), lens=60, margin=1.25)


if __name__ == "__main__":
    os.makedirs(os.path.join(ROOT, "ArtSource", "blend"), exist_ok=True)
    main()
