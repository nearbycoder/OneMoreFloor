"""The guests (and the operator) of The Shuffleton: chunky storybook people with one silhouette prop each.

    blender -b -P ArtSource/build_characters.py -- [--only Vampire,Kid] [--preview DIR] [--no-export]

Authored in Unity space: feet at y=0, facing +z (Unity forward). Every humanoid shares one simple rig of
separate, pivoted parts that PassengerView animates in code (no skinning):
    Body                      torso, neck, clothing; arms that hold something with both hands live here
    LegL / LegR               pivot at the hip (walk cycle, foot taps, hop tucks)
    ArmL / ArmR               pivot at the shoulder (arm swing, cheers, fists)
    Head                      pivot at the neck
plus the kind-specific animated parts: Cape, Mirror, Balloons, Propeller, Leaves/Leaf0..N.
Skin uses col_F2C9A0_s30 and the commuter suit col_3D5A80, the kid shirt col_F07F3C so the game can
recolour them per guest. Exported to Assets/Resources/Models/Char_<Name>.fbx.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from omf_lib import *  # noqa: E402,F401,F403

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models")

SKIN = col("F2C9A0", 0.3)
INK_EYE = col("1C1820", 0.7)
SHOE_BLACK = col("1C1820", 0.75)
SHOE_BROWN = col("5A3420", 0.6)
WHITE_GLOVE = col("F7F2EA", 0.35)


class Rig:
    """Key heights of one body type (Unity units, feet at 0)."""

    def __init__(self, hip=0.6, leg_x=0.13, leg_r=0.1, shoulder=1.2, shoulder_x=0.3, arm_r=0.085,
                 neck=1.3, head_y=1.67, head_r=0.33, depth=0.8, torso=None, hand_r=0.08):
        self.hip, self.leg_x, self.leg_r = hip, leg_x, leg_r
        self.shoulder, self.shoulder_x, self.arm_r = shoulder, shoulder_x, arm_r
        self.neck, self.head_y, self.head_r, self.depth = neck, head_y, head_r, depth
        self.hand_r = hand_r
        self.torso = torso or [(0.0, hip - 0.1), (0.24, hip - 0.1), (0.27, hip - 0.04), (0.27, hip + 0.06),
                               (0.25, hip + 0.22), (0.27, hip + 0.36), (0.3, shoulder - 0.12), (0.29, shoulder - 0.02),
                               (0.21, shoulder + 0.06), (0.1, neck + 0.01), (0.0, neck + 0.02)]

    def surface_z(self, y, extra=0.0):
        """Front of the torso at height y (for buttons, ties, lapels)."""
        prof = self.torso
        for (r0, y0), (r1, y1) in zip(prof, prof[1:]):
            if y0 <= y <= y1 and y1 > y0:
                return (r0 + (r1 - r0) * (y - y0) / (y1 - y0)) * self.depth + extra
        return 0.2 + extra


ADULT = Rig()


# ----------------------------------------------------------------------------- body parts

def torso(m, rig, mat, profile=None, depth=None):
    prof = profile or rig.torso
    m.add(lathe(prof, (0, 0, 0), seg=32), mat, transform=Matrix.Diagonal(V(1, 1, depth or rig.depth, 1)))


def neck(m, rig, mat=SKIN):
    m.add(rod((0, rig.neck - 0.08, 0), (0, rig.neck + 0.12, 0), rig.arm_r * 0.95, seg=14), mat)


def leg(root, rig, side, trouser, shoe_mat, name=None, bare_below=None, skin=SKIN, shoe="shoe", cuff=None, sock=None, spat=None):
    """One leg pivoting at the hip; `bare_below` = height where trousers end and skin shows (shorts).
    `sock` = material of knee socks, `spat` = material of spats over the shoe."""
    x = side * rig.leg_x
    hip, knee, ankle = (x, rig.hip, 0.0), (x, rig.hip * 0.5, 0.03), (x, 0.13, 0.0)
    m = Model(name or ("LegL" if side > 0 else "LegR"))
    r = rig.leg_r
    if bare_below is None:
        m.add(limb(hip, knee, ankle, r, r * 0.93, r * 0.85), trouser)
        if cuff:
            m.add(torus((x, 0.17, 0.0), r * 0.86, 0.022, "y", seg=18, ring_seg=6), cuff)
    else:
        m.add(limb(hip, knee, ankle, r * 0.82, r * 0.78, r * 0.7), skin)
        m.add(lathe([(r * 1.12, bare_below), (r * 1.18, rig.hip - 0.02), (0.0, rig.hip + 0.04)], (x, 0, 0), seg=18), trouser)
    if sock:
        m.add(lathe([(r * 0.88, 0.1), (r * 0.9, rig.hip * 0.42), (r * 0.98, rig.hip * 0.46), (0.0, rig.hip * 0.46)], (x, 0, 0.02), seg=18), sock)
    if spat:
        m.add(box((x - 0.104, 0.07, -0.104), (x + 0.104, 0.2, 0.1), 0.05, seg=2), spat)
    if shoe == "shoe":
        m.add(box((x - 0.1, 0.0, -0.1), (x + 0.1, 0.15, 0.2), 0.065, seg=3), shoe_mat)
        m.add(box((x - 0.102, 0.0, -0.102), (x + 0.102, 0.035, 0.202), 0.015), col("2A1E2E", 0.3))
    elif shoe == "sneaker":
        m.add(box((x - 0.09, 0.0, -0.09), (x + 0.09, 0.14, 0.18), 0.06, seg=3), shoe_mat)
        m.add(box((x - 0.094, 0.0, -0.094), (x + 0.094, 0.045, 0.186), 0.02), WHITE)
    elif shoe == "flipper":
        m.add(hull([(x - 0.08, 0.0, -0.1), (x + 0.08, 0.0, -0.1), (x - 0.15, 0.0, 0.42), (x + 0.15, 0.0, 0.42),
                    (x - 0.08, 0.07, -0.08), (x + 0.08, 0.07, -0.08), (x - 0.14, 0.03, 0.42), (x + 0.14, 0.03, 0.42)], 0.02), shoe_mat)
        m.add(box((x - 0.09, 0.0, -0.1), (x + 0.09, 0.15, 0.1), 0.05, seg=3), shoe_mat)
    m.build(origin=hip, parent=root)
    return m


def arm_geo(m, rig, sleeve, hand, sh, el, wr, cuff=None, fist=False):
    """Upper arm, forearm and a mitten hand (with a thumb) added into model `m`."""
    r = rig.arm_r
    m.add(limb(sh, el, wr, r * 1.05, r * 0.92, r * 0.8), sleeve)
    d = (V(wr) - V(el)).normalized()
    hc = V(wr) + d * rig.hand_r * 0.9
    m.add(sphere(hc, rig.hand_r, (0.86, 1.08 if not fist else 0.9, 0.95), seg=16, rings=10), hand)
    side = 1 if sh[0] > 0 else -1
    m.add(sphere(hc + V(-side * 0.035, 0.015, 0.055), rig.hand_r * 0.42, seg=10, rings=8), hand)
    if cuff:
        m.add(sphere(V(wr) - d * 0.01, r * 0.98, (1, 0.45, 1), seg=16, rings=8), cuff)
    return hc


def arm(root, rig, side, sleeve, hand, swing=(0.0, 0.0), cuff=None, extra=None):
    """A free arm pivoting at the shoulder, hanging slightly out. extra(m, hand_center) adds held props."""
    sx = side * rig.shoulder_x
    sh = (sx, rig.shoulder - 0.04, 0.0)
    el = (sx + side * 0.07, rig.shoulder - 0.3, 0.03 + swing[0])
    wr = (sx + side * 0.09, rig.shoulder - 0.53, 0.07 + swing[1])
    m = Model("ArmL" if side > 0 else "ArmR")
    hc = arm_geo(m, rig, sleeve, hand, sh, el, wr, cuff)
    if extra:
        extra(m, hc)
    m.add(sphere(sh, rig.arm_r * 1.25, seg=16, rings=10), sleeve)
    m.build(origin=sh, parent=root)
    return m


def head_base(root, rig, skin=SKIN, ears=True, nose=True, ear_point=False):
    h = Model("Head")
    y, r = rig.head_y, rig.head_r
    h.add(sphere((0, y, 0), r, (1.0, 0.97, 0.95), seg=32, rings=20), skin)
    # soft jaw / cheeks so the head isn't a perfect ball
    h.add(sphere((0, y - r * 0.38, r * 0.18), r * 0.72, (1.05, 0.75, 0.9), seg=24, rings=14), skin)
    if nose:
        h.add(sphere((0, y - r * 0.12, r * 0.93), r * 0.16, (1.0, 0.85, 0.85), seg=14, rings=10), skin)
    if ears:
        for s in (-1, 1):
            if ear_point:
                h.add(hull([(s * r * 0.9, y + 0.04, 0.02), (s * r * 0.9, y - 0.1, 0.0), (s * r * 0.9, y - 0.02, -0.08),
                            (s * (r + 0.13), y + 0.16, -0.06), (s * (r * 0.95), y - 0.02, -0.02)], 0.01), skin)
            else:
                h.add(sphere((s * r * 0.96, y - 0.03, -0.01), r * 0.22, (0.45, 1.0, 0.75), seg=14, rings=10), skin)
    return h


def eyes(h, rig, look=(0.0, 0.0), size=1.0, spread=0.125, lids=0.0, lid_mat=SKIN, y_off=0.02, brows=None, brow_mat=None):
    """Big storybook eyes; lids (0..1) droop the upper lids for sleepy/sly looks; brows tilt in degrees."""
    y, r = rig.head_y + y_off, rig.head_r
    s0 = 0.078 * size * r / 0.33
    for s in (-1, 1):
        cx = s * spread * r / 0.33
        cz = math.sqrt(max(0.0, r * r - cx * cx - (y_off) ** 2)) * 0.93
        h.add(sphere((cx, y, cz), s0, (0.88, 1.12, 0.55), seg=16, rings=12), WHITE)
        h.add(sphere((cx + look[0] * s0 * 0.25, y - 0.004 + look[1] * s0 * 0.25, cz + s0 * 0.42), s0 * 0.58, (1, 1.1, 0.6), seg=14, rings=10), INK_EYE)
        h.add(sphere((cx + s0 * 0.25, y + s0 * 0.38, cz + s0 * 0.72), s0 * 0.2, seg=8, rings=6), WHITE)
        if lids > 0:
            h.add(cut_sphere((cx, y, cz), s0 * 1.12, (0.9, 1.14, 0.62), plane_co=(cx, y + s0 * (1.0 - 1.7 * lids), cz), plane_no=(0, 1, 0), seg=16, rings=12), lid_mat)
        if brows is not None:
            by = y + s0 * 1.75
            ang = brows * s
            h.add(boxc((cx, by, cz + 0.005), (s0 * 1.7, s0 * 0.36, s0 * 0.5), s0 * 0.15), brow_mat or col("2A1E2E", 0.3),
                  transform=rot_about((cx, by, cz), "Z", ang))


def smile(h, rig, width=0.065, y_off=-0.2, mat=None, frown=False):
    y = rig.head_y + rig.head_r * y_off * 1.0 - 0.02
    z = rig.head_r * 0.9
    m = mat or col("6E2A33", 0.3)
    h.add(torus((0, y, z), width, 0.013, "z", arc=0.5, seg=14, ring_seg=6), m,
          transform=rot_about((0, y, z), "Z", 0 if frown else 180))


def cheeks(h, rig, mat=None):
    r = rig.head_r
    for s in (-1, 1):
        h.add(sphere((s * r * 0.58, rig.head_y - r * 0.28, r * 0.72), r * 0.13, (1, 0.65, 0.4), seg=10, rings=6), mat or col("F29A8A", 0.2))


def hair_cap(h, rig, mat, front=0.24, back=-0.42, puff=0.035, cover=1.0, scale=(1.03, 1.0, 1.03)):
    """Hair as a shell over the skull: `front` is the hairline height above head centre at the brow,
    `back` how far down the nape it reaches (negative = below centre)."""
    y, r = rig.head_y, rig.head_r
    rr = r + puff
    # plane through (front at +z) and (back at -z)
    zf, zb = r, -r
    yf, yb = y + front * r / 0.33, y + back * r / 0.33
    n = V(0, zf - zb, -(yf - yb)).normalized()
    h.add(cut_sphere((0, y + 0.01, -0.01), rr, scale, plane_co=(0, yf, zf), plane_no=n, seg=32, rings=20), mat)


# ----------------------------------------------------------------------------- characters

def commuter(root):
    rig = ADULT
    suit = col("3D5A80", 0.3)
    shirt = WHITE
    b = Model("Body")
    torso(b, rig, suit)
    neck(b, rig)
    # shirt V, tie, lapels, pocket square, buttons
    zc = rig.surface_z(1.06)
    b.add(prism([(-0.12, 1.25), (0.12, 1.25), (0.0, 0.9)], zc - 0.06, zc + 0.012), shirt)
    b.add(prism([(-0.035, 1.21), (0.035, 1.21), (0.05, 0.94), (0.0, 0.88), (-0.05, 0.94)], zc - 0.04, zc + 0.03), col("C0262D", 0.4))
    b.add(boxc((0, 1.21, zc + 0.02), (0.07, 0.05, 0.04), 0.015), col("A01E26", 0.4))
    for s in (-1, 1):
        b.add(prism([(s * 0.12, 1.25), (s * 0.2, 1.18), (s * 0.06, 0.88), (s * 0.02, 0.9)], zc - 0.03, zc + 0.03), col("2E4766", 0.3))
    b.add(boxc((0.17, 1.03, rig.surface_z(1.03) + 0.005), (0.08, 0.04, 0.02)), WHITE)
    for y in (0.84, 0.72):
        b.add(sphere((0, y, rig.surface_z(y) + 0.005), 0.022, seg=8, rings=6), col("1C1820", 0.6))
    b.build(parent=root)
    trouser = col("3D5A80", 0.3)
    for s in (-1, 1):
        leg(root, rig, s, trouser, SHOE_BLACK)

    def briefcase(m, hc):
        m.add(box((hc.x - 0.07, hc.y - 0.5, hc.z - 0.24), (hc.x + 0.07, hc.y - 0.1, hc.z + 0.24), 0.035), col("6B3F22", 0.45))
        m.add(torus((hc.x, hc.y - 0.06, hc.z), 0.07, 0.016, "x", arc=0.5), col("2A1E2E", 0.4))
        m.add(box((hc.x - 0.075, hc.y - 0.2, hc.z - 0.06), (hc.x + 0.075, hc.y - 0.16, hc.z + 0.06)), BRASS)
    arm(root, rig, 1, suit, SKIN, cuff=shirt)
    arm(root, rig, -1, suit, SKIN, cuff=shirt, extra=briefcase)
    h = head_base(root, rig)
    eyes(h, rig, brows=-4, brow_mat=col("3A2618", 0.4))
    smile(h, rig)
    cheeks(h, rig)
    hair = col("3A2618", 0.45)
    hair_cap(h, rig, hair, front=0.2, back=-0.36)
    # side-parted quiff
    h.add(sphere((-0.1, rig.head_y + 0.27, 0.12), 0.17, (1.35, 0.55, 1.0), seg=16, rings=10), hair)
    h.add(sphere((0.13, rig.head_y + 0.24, 0.1), 0.14, (1.2, 0.5, 1.0), seg=16, rings=10), hair)
    # round spectacles
    for s in (-1, 1):
        cx = s * 0.125
        h.add(torus((cx, rig.head_y + 0.02, 0.33), 0.07, 0.011, "z", seg=20, ring_seg=6), col("2A1E2E", 0.6))
    h.add(boxc((0, rig.head_y + 0.03, 0.335), (0.07, 0.014, 0.014)), col("2A1E2E", 0.6))
    h.build(origin=(0, rig.neck, 0), parent=root)


def bellhop(root):
    rig = ADULT
    red = col("C0262D", 0.35)
    b = Model("Body")
    torso(b, rig, red)
    neck(b, rig)
    # short mess jacket hem, brass buttons in two rows, gold piping, epaulettes
    for y in (1.08, 0.94, 0.8):
        for s in (-1, 1):
            b.add(sphere((s * 0.08, y, rig.surface_z(y) + 0.005), 0.026, seg=10, rings=8), BRASS)
    b.add(lathe([(0.272, 0.66), (0.278, 0.7), (0.0, 0.7)], (0, 0, 0), seg=32), BRASS, transform=Matrix.Diagonal(V(1, 1, rig.depth, 1)))
    b.add(lathe([(0.0, 0.5), (0.25, 0.5), (0.272, 0.56), (0.272, 0.66), (0.0, 0.66)], (0, 0, 0), seg=32), col("1C1820", 0.5),
          transform=Matrix.Diagonal(V(1.01, 1, rig.depth * 1.01, 1)))
    b.add(lathe([(0.17, 1.26), (0.12, 1.36), (0.0, 1.36)], (0, 0, 0), seg=24), red)
    b.add(torus((0, 1.36, 0), 0.12, 0.012, "y", seg=24), BRASS)
    for s in (-1, 1):
        b.add(boxc((s * 0.27, 1.2, 0), (0.16, 0.05, 0.22), 0.02), BRASS)
        for k in range(4):
            b.add(sphere((s * (0.21 + k * 0.035), 1.16, 0.0), 0.016, seg=6, rings=4), BRASS)
    b.build(parent=root)
    trouser = col("1C1820", 0.5)
    for s in (-1, 1):
        lg = leg(root, rig, s, trouser, SHOE_BLACK)
    arm(root, rig, 1, red, WHITE_GLOVE, cuff=BRASS)
    arm(root, rig, -1, red, WHITE_GLOVE, cuff=BRASS)
    h = head_base(root, rig)
    eyes(h, rig, brows=-2, brow_mat=col("6E3A20", 0.4))
    smile(h, rig, width=0.075)
    cheeks(h, rig)
    hair = col("8A4B2A", 0.4)
    hair_cap(h, rig, hair, front=0.16, back=-0.36)
    h.add(sphere((0.1, rig.head_y + 0.22, 0.2), 0.12, (1.5, 0.45, 0.9), seg=14, rings=8), hair)
    # pillbox hat, tipped at a jaunty angle, with chin strap
    hat_c = (0.06, rig.head_y + 0.33, 0.0)
    tilt = rot_about(hat_c, "Z", -12)
    h.add(cyl(hat_c, 0.2, 0.2, "y", bevel=0.025), red, transform=tilt)
    h.add(cyl((hat_c[0], hat_c[1] - 0.075, 0), 0.205, 0.05, "y"), BRASS, transform=tilt)
    h.add(sphere((hat_c[0], hat_c[1] + 0.11, 0), 0.04), BRASS, transform=tilt)
    h.build(origin=(0, rig.neck, 0), parent=root)


def vampire(root):
    rig = Rig(hip=0.66, shoulder=1.3, neck=1.4, head_y=1.76, head_r=0.31, shoulder_x=0.29, leg_r=0.09, arm_r=0.075, depth=0.74)
    coat = col("1C1820", 0.45)
    pale = col("E8E4EE", 0.3)
    b = Model("Body")
    torso(b, rig, coat)
    neck(b, rig, pale)
    zc = rig.surface_z(1.15)
    b.add(prism([(-0.13, 1.36), (0.13, 1.36), (0.0, 0.95)], zc - 0.06, zc + 0.012), WHITE)
    # blood-red cravat and a ruby pin
    b.add(prism([(-0.07, 1.34), (0.07, 1.34), (0.05, 1.18), (0.0, 1.12), (-0.05, 1.18)], zc - 0.03, zc + 0.04), col("B3122E", 0.5))
    b.add(sphere((0, 1.24, zc + 0.045), 0.025, seg=10, rings=8), met("E5484D", 0.85))
    # tail coat skirts behind
    for s in (-1, 1):
        b.add(prism([(s * 0.04, 0.78), (s * 0.27, 0.78), (s * 0.25, 0.28), (s * 0.08, 0.36)], -0.26, -0.18), coat)
    b.build(parent=root)
    trouser = col("16121A", 0.55)
    for s in (-1, 1):
        leg(root, rig, s, trouser, col("0E0B10", 0.9))
    arm(root, rig, 1, coat, pale, cuff=WHITE)
    arm(root, rig, -1, coat, pale, cuff=WHITE)
    h = head_base(root, rig, skin=pale, ear_point=True)
    eyes(h, rig, size=0.95, lids=0.35, lid_mat=pale, brows=-22, brow_mat=col("16121A", 0.6))
    # thin sly smile with fangs
    y = rig.head_y - 0.1
    z = rig.head_r * 0.9
    h.add(torus((0, y, z), 0.07, 0.011, "z", arc=0.5, seg=14, ring_seg=6), col("6E2A33", 0.3), transform=rot_about((0, y, z), "Z", 180))
    for s in (-1, 1):
        h.add(rod((s * 0.045, y - 0.03, z + 0.01), (s * 0.045, y - 0.085, z + 0.01), 0.018, radius2=0.002, seg=8), WHITE)
    hair = col("16121A", 0.8)
    hair_cap(h, rig, hair, front=0.2, back=-0.3, puff=0.025)
    # widow's peak
    h.add(prism([(-0.11, rig.head_y + 0.24), (0.11, rig.head_y + 0.24), (0.0, rig.head_y + 0.1)], 0.2, 0.3), hair)
    h.build(origin=(0, rig.neck, 0), parent=root)
    cape = Model("Cape")
    cy = rig.shoulder + 0.08
    # high stand-up collar (red inside) and a long cape that flares at the hem
    for s in (-1, 1):
        cape.add(prism([(s * 0.12, cy), (s * 0.46, cy), (s * 0.54, cy + 0.6), (s * 0.2, cy + 0.36)], -0.14, -0.08), col("16121A", 0.55))
        cape.add(prism([(s * 0.14, cy + 0.02), (s * 0.43, cy + 0.02), (s * 0.49, cy + 0.53), (s * 0.2, cy + 0.34)], -0.08, -0.06), col("B3122E", 0.5))
    cape.add(prism([(-0.4, cy), (0.4, cy), (0.6, 0.06), (-0.6, 0.06)], -0.34, -0.27), col("16121A", 0.55))
    cape.add(prism([(-0.36, cy - 0.02), (0.36, cy - 0.02), (0.54, 0.09), (-0.54, 0.09)], -0.27, -0.25), col("B3122E", 0.5))
    for s in (-1, 1):
        cape.add(sphere((s * 0.2, cy - 0.04, 0.12), 0.04, seg=10, rings=8), met("D7A84A", 0.8))
    cape.add(sweep([(-0.2, cy - 0.04, 0.12), (0.0, cy - 0.1, 0.17), (0.2, cy - 0.04, 0.12)], 0.01), met("D7A84A", 0.8))
    cape.build(origin=(0, cy, -0.2), parent=root)


def courier(root):
    rig = ADULT
    uni = col("8A5A2B", 0.3)
    b = Model("Body")
    torso(b, rig, uni)
    neck(b, rig)
    zc = rig.surface_z(1.1)
    # collar, pocket flaps, satchel strap and bag on the hip
    for s in (-1, 1):
        b.add(prism([(s * 0.04, 1.27), (s * 0.17, 1.24), (s * 0.13, 1.14), (s * 0.03, 1.2)], zc - 0.03, zc + 0.03), col("A87440", 0.3))
    b.add(sweep([(-0.24, 1.22, 0.16), (-0.05, 1.0, 0.25), (0.15, 0.78, 0.23), (0.27, 0.62, 0.12)], 0.026), col("4E2E1C", 0.35))
    b.add(box((0.2, 0.42, -0.14), (0.36, 0.7, 0.16), 0.04), col("5A3420", 0.35))
    b.add(box((0.205, 0.58, -0.145), (0.365, 0.71, 0.165), 0.03), col("4E2E1C", 0.35))
    b.add(sphere((0.36, 0.6, 0.0), 0.02), BRASS)
    # both arms reach forward to carry the parcel
    for s in (-1, 1):
        sh = (s * rig.shoulder_x, rig.shoulder - 0.04, 0.0)
        arm_geo(b, rig, uni, SKIN, sh, (s * 0.34, 0.95, 0.2), (s * 0.29, 0.88, 0.4), cuff=col("A87440", 0.3))
        b.add(sphere(sh, rig.arm_r * 1.25, seg=16, rings=10), uni)
    # the urgent parcel
    p0, p1 = V(-0.25, 0.78, 0.32), V(0.25, 1.12, 0.7)
    b.add(box(p0, p1, 0.03), col("C8955A", 0.2))
    b.add(box((p0.x - 0.005, 0.93, p0.z - 0.005), (p1.x + 0.005, 0.97, p1.z + 0.005)), col("E8D27A", 0.3))
    b.add(box((-0.02, p0.y - 0.005, p0.z - 0.005), (0.02, p1.y + 0.005, p1.z + 0.005)), col("E8D27A", 0.3))
    b.add(box((-0.2, 0.82, p1.z), (0.08, 0.9, p1.z + 0.015)), col("E5484D", 0.4))
    b.add(box((0.1, 0.99, p1.z), (0.22, 1.08, p1.z + 0.015)), WHITE)
    b.build(parent=root)
    shorts = col("6B4423", 0.3)
    for s in (-1, 1):
        leg(root, rig, s, shorts, SHOE_BROWN, bare_below=0.36, sock=col("F3E6C8", 0.3))
    h = head_base(root, rig)
    eyes(h, rig, brows=8, brow_mat=col("2A1E2E", 0.4), look=(0, 0.6))
    smile(h, rig, width=0.06)
    cheeks(h, rig)
    hair_cap(h, rig, col("2A1E2E", 0.4), front=0.12, back=-0.38)
    # peaked cap with a badge
    cap_c = (0, rig.head_y + 0.2, -0.01)
    h.add(cut_sphere(cap_c, rig.head_r + 0.045, (1.0, 0.75, 1.0), plane_co=(0, rig.head_y + 0.16, 0), plane_no=(0, 1, 0), seg=28, rings=16), col("6B4423", 0.35))
    h.add(cyl((0, rig.head_y + 0.17, 0), rig.head_r + 0.05, 0.05, "y"), col("4E2E1C", 0.35))
    h.add(hull([(-0.2, rig.head_y + 0.16, 0.22), (0.2, rig.head_y + 0.16, 0.22), (-0.16, rig.head_y + 0.12, 0.5), (0.16, rig.head_y + 0.12, 0.5),
                (-0.2, rig.head_y + 0.18, 0.22), (0.2, rig.head_y + 0.18, 0.22), (-0.16, rig.head_y + 0.14, 0.5), (0.16, rig.head_y + 0.14, 0.5)], 0.01), col("4E2E1C", 0.35))
    h.add(cyl((0, rig.head_y + 0.27, rig.head_r + 0.02), 0.05, 0.03, "z"), col("FFD23F", 0.5))
    h.build(origin=(0, rig.neck, 0), parent=root)


def swimmer(root):
    rig = ADULT
    tan = col("F2C9A0", 0.35)
    red, cream = col("E5484D", 0.5), col("F7F2EA", 0.5)
    b = Model("Body")
    torso(b, rig, tan)
    neck(b, rig, tan)
    # old-timey striped bathing suit with straps
    stripes = 6
    y0, y1 = rig.hip - 0.12, 1.12
    for k in range(stripes):
        ya, yb = y0 + (y1 - y0) * k / stripes, y0 + (y1 - y0) * (k + 1) / stripes
        prof = [(r * 1.035, y) for r, y in rig.torso if ya - 0.08 <= y <= yb + 0.08]
        r_at = lambda yy: rig.surface_z(yy) / rig.depth * 1.035
        b.add(lathe([(r_at(ya), ya), (r_at((ya + yb) / 2), (ya + yb) / 2), (r_at(yb), yb), (0.0, yb)], (0, 0, 0), seg=32),
              red if k % 2 == 0 else cream, transform=Matrix.Diagonal(V(1, 1, rig.depth, 1)))
    for s in (-1, 1):
        b.add(sweep([(s * 0.13, 1.1, rig.surface_z(1.1) + 0.01), (s * 0.16, 1.22, 0.12), (s * 0.15, 1.24, -0.08), (s * 0.12, 1.1, -0.2)], 0.025), red)
    # towel over the left shoulder
    towel = col("3FA9F5", 0.1)
    tx0, tx1 = -0.3, -0.1
    zf, zb = rig.surface_z(1.0) + 0.02, -rig.surface_z(1.0) - 0.02
    b.add(box((tx0, 0.62, zf - 0.03), (tx1, 1.18, zf + 0.03), 0.025), towel)
    b.add(box((tx0, 0.78, zb - 0.03), (tx1, 1.18, zb + 0.03), 0.025), towel)
    b.add(cut_sphere(((tx0 + tx1) / 2, 1.17, 0), 1.0, ((tx1 - tx0) / 2, 0.14, zf + 0.03), plane_co=(0, 1.17, 0), plane_no=(0, 1, 0), seg=20, rings=12), towel)
    for y in (0.68, 0.74):
        b.add(box((tx0 - 0.003, y, zf - 0.033), (tx1 + 0.003, y + 0.025, zf + 0.033)), cream)
    b.build(parent=root)
    flipper = col("2C8BD6", 0.6)
    for s in (-1, 1):
        leg(root, rig, s, red, flipper, bare_below=rig.hip - 0.14, skin=tan, shoe="flipper")
    arm(root, rig, 1, tan, tan)
    arm(root, rig, -1, tan, tan)
    h = head_base(root, rig, skin=tan)
    eyes(h, rig, look=(0, -0.6), brows=6, brow_mat=col("C88A52", 0.4))
    smile(h, rig, width=0.06)
    cheeks(h, rig)
    # rubber swim cap with a chin strap, goggles pushed up
    hair_cap(h, rig, col("F7F2EA", 0.65), front=0.18, back=-0.3, puff=0.03)
    for k in range(3):
        a = -40 + k * 40
        x, z = math.sin(math.radians(a)) * 0.2, math.cos(math.radians(a)) * 0.2
        h.add(sphere((x, rig.head_y + 0.3, z), 0.06, (1, 0.7, 1)), col("FFB0C8", 0.5))
    gy = rig.head_y + 0.2
    h.add(torus((0, gy - 0.02, 0), rig.head_r + 0.04, 0.025, "y", seg=28), col("2C8BD6", 0.6), transform=rot_about((0, gy, 0), "X", -12))
    for s in (-1, 1):
        cx = s * 0.12
        h.add(torus((cx, gy + 0.03, 0.29), 0.07, 0.024, "z", seg=18), col("2C8BD6", 0.6), transform=rot_about((cx, gy, 0.29), "X", -20))
        h.add(sphere((cx, gy + 0.03, 0.29), 0.06, (1, 1, 0.4)), gls("9FE1FF", 0.6), transform=rot_about((cx, gy, 0.29), "X", -20))
    h.build(origin=(0, rig.neck, 0), parent=root)


KID = Rig(hip=0.4, leg_x=0.1, leg_r=0.085, shoulder=0.86, shoulder_x=0.24, arm_r=0.07, neck=0.94, head_y=1.27, head_r=0.31,
          depth=0.85, hand_r=0.068,
          torso=[(0.0, 0.3), (0.2, 0.3), (0.23, 0.36), (0.23, 0.46), (0.24, 0.6), (0.24, 0.74), (0.22, 0.84), (0.14, 0.92), (0.07, 0.95), (0.0, 0.96)])


def kid(root):
    rig = KID
    shirt = col("F07F3C", 0.3)
    b = Model("Body")
    torso(b, rig, shirt)
    neck(b, rig)
    for k in range(3):
        y0 = 0.46 + k * 0.13
        r = rig.surface_z(y0 + 0.03) / rig.depth * 1.03
        b.add(lathe([(r, y0), (r, y0 + 0.05), (0.0, y0 + 0.05)], (0, 0, 0), seg=28), col("FFD23F", 0.3), transform=Matrix.Diagonal(V(1, 1, rig.depth, 1)))
    # shorts with suspenders
    b.add(lathe([(0.0, 0.29), (0.215, 0.29), (0.245, 0.36), (0.245, 0.47), (0.0, 0.47)], (0, 0, 0), seg=28), col("3B5BA8", 0.3),
          transform=Matrix.Diagonal(V(1, 1, rig.depth, 1)))
    for s in (-1, 1):
        b.add(sweep([(s * 0.12, 0.46, rig.surface_z(0.46) + 0.02), (s * 0.12, 0.86, rig.surface_z(0.8) + 0.02), (s * 0.1, 0.9, 0.0), (s * 0.12, 0.6, -0.2)], 0.02), col("3B5BA8", 0.3))
        b.add(sphere((s * 0.12, 0.47, rig.surface_z(0.47) + 0.03), 0.022), BRASS)
    # right hand raised, holding the balloon strings
    sh = (0.24, rig.shoulder - 0.03, 0.0)
    arm_geo(b, rig, shirt, SKIN, sh, (0.36, 0.78, 0.04), (0.38, 0.98, 0.06), fist=True)
    b.add(sphere(sh, rig.arm_r * 1.25, seg=16, rings=10), shirt)
    b.build(parent=root)
    for s in (-1, 1):
        leg(root, rig, s, col("3B5BA8", 0.3), col("E5484D", 0.45), bare_below=0.3, shoe="sneaker")
    arm(root, rig, -1, shirt, SKIN)
    h = head_base(root, rig)
    eyes(h, rig, size=1.2, spread=0.12, look=(0.2, 0.4))
    # big open grin
    y, z = rig.head_y - 0.1, rig.head_r * 0.9
    h.add(sphere((0, y, z - 0.01), 0.055, (1.3, 0.75, 0.5), seg=14, rings=8), col("6E2A33", 0.3))
    h.add(boxc((0, y + 0.03, z + 0.008), (0.07, 0.02, 0.02)), WHITE)
    cheeks(h, rig)
    # freckles
    for s in (-1, 1):
        for k in range(3):
            h.add(sphere((s * (0.15 + 0.03 * k), rig.head_y - 0.05 + 0.02 * (k % 2), 0.27), 0.009, seg=6, rings=4), col("B8784A", 0.2))
    hair = col("D9822B", 0.4)
    hair_cap(h, rig, hair, front=0.12, back=-0.36)
    for k in range(4):
        h.add(sphere((-0.1 + k * 0.07, rig.head_y + 0.16, 0.27), 0.055, (1, 1.2, 0.8), seg=10, rings=8), hair)
    # propeller beanie
    beanie = cut_sphere((0, rig.head_y + 0.08, 0), rig.head_r + 0.05, (1.0, 0.85, 1.0), plane_co=(0, rig.head_y + 0.14, 0), plane_no=(0, 1, 0), seg=28, rings=16)
    h.add(beanie, col("3B7DD8", 0.35))
    for k in range(4):
        a = k * 90 + 45
        h.add(cut_sphere((0, rig.head_y + 0.08, 0), rig.head_r + 0.052, (1.0, 0.85, 1.0), plane_co=(0, rig.head_y + 0.14, 0), plane_no=(0, 1, 0), seg=20, rings=12, fill=False),
              col("FFD23F", 0.35), transform=Matrix.Rotation(math.radians(a), 4, "Y") @ Matrix.Diagonal(V(0.12, 1.0, 1.0, 1)))
    h.add(cyl((0, rig.head_y + 0.14, 0), rig.head_r + 0.055, 0.04, "y"), col("FFD23F", 0.35))
    h.add(cyl((0, rig.head_y + 0.39, 0), 0.025, 0.1, "y"), col("FFD23F", 0.4))
    h.build(origin=(0, rig.neck, 0), parent=root)
    prop = Model("Propeller")
    py = rig.head_y + 0.45
    for k in range(2):
        a = k * 180
        prop.add(hull([(0, py - 0.01, -0.03), (0, py + 0.01, 0.03), (0.3, py - 0.015, -0.06), (0.3, py + 0.015, 0.06)], 0.01),
                 col("E5484D" if k == 0 else "6CCB5F", 0.4), transform=rot_about((0, py, 0), "Y", a))
    prop.add(sphere((0, py, 0), 0.035), col("FFD23F", 0.4))
    prop.build(origin=(0, py, 0), parent=root)
    bal = Model("Balloons")
    hand = V(0.39, 1.06, 0.07)
    for c, off in (("E5484D", (-0.2, 1.85, 0.05)), ("FFD23F", (0.18, 2.02, -0.05)), ("3FA9F5", (0.5, 1.78, 0.08))):
        p = V(off)
        bal.add(sphere(p, 0.21, (0.92, 1.12, 0.92), seg=20, rings=14), col(c, 0.75))
        bal.add(lathe([(0.0, 0), (0.045, 0.02), (0.0, 0.055)], p - V(0, 0.27, 0), seg=10), col(c, 0.75))
        bal.add(sweep([tuple(hand), tuple(hand.lerp(p - V(0, 0.25, 0), 0.5) + V(0.03, 0, 0)), tuple(p - V(0, 0.24, 0))], 0.005, seg=5), col("2A1E2E", 0.3))
        bal.add(sphere(p + V(-0.07, 0.09, 0.16), 0.045, (1, 1.4, 0.5)), col("FFFFFF", 0.9))
    bal.build(origin=tuple(hand), parent=root)


TYCOON = Rig(hip=0.6, leg_x=0.15, leg_r=0.1, shoulder=1.2, shoulder_x=0.36, arm_r=0.09, neck=1.3, head_y=1.68, head_r=0.33, depth=0.9,
             torso=[(0.0, 0.5), (0.3, 0.5), (0.36, 0.58), (0.44, 0.74), (0.47, 0.88), (0.44, 1.02), (0.37, 1.14), (0.3, 1.2), (0.2, 1.27), (0.1, 1.31), (0.0, 1.32)])


def tycoon(root):
    rig = TYCOON
    suit = col("4B2E6E", 0.35)
    b = Model("Body")
    torso(b, rig, suit)
    neck(b, rig)
    zc = rig.surface_z(1.1)
    b.add(prism([(-0.14, 1.27), (0.14, 1.27), (0.0, 0.86)], zc - 0.08, zc + 0.02), WHITE)
    # waistcoat over the belly, watch chain, carnation, bow tie
    b.add(lathe([(0.0, 0.62), (0.42, 0.62), (0.475, 0.8), (0.48, 0.9), (0.45, 1.0), (0.0, 1.0)], (0, 0, 0), seg=32), col("E8D27A", 0.35),
          transform=Matrix.Diagonal(V(0.62, 1, rig.depth * 1.02, 1)))
    for y in (0.7, 0.8, 0.9):
        b.add(sphere((0, y, rig.surface_z(y) + 0.02), 0.022, seg=8, rings=6), BRASS)
    b.add(sweep([(-0.22, 0.86, rig.surface_z(0.86) - 0.03), (-0.1, 0.78, rig.surface_z(0.78) + 0.01), (0.1, 0.8, rig.surface_z(0.8) + 0.01), (0.23, 0.87, rig.surface_z(0.87) - 0.03)], 0.012), BRASS)
    b.add(cyl((0.24, 0.86, rig.surface_z(0.86) - 0.01), 0.045, 0.02, "z"), BRASS)
    b.add(boxc((0, 1.25, rig.surface_z(1.25) + 0.03), (0.07, 0.07, 0.05), 0.02), col("E5484D", 0.4))
    for s in (-1, 1):
        b.add(hull([(0, 1.25, rig.surface_z(1.25) + 0.03), (s * 0.14, 1.31, rig.surface_z(1.25) + 0.02), (s * 0.14, 1.19, rig.surface_z(1.25) + 0.02),
                    (s * 0.02, 1.25, rig.surface_z(1.25) + 0.06)], 0.01), col("E5484D", 0.4))
    b.add(sphere((-0.24, 1.12, rig.surface_z(1.12) + 0.02), 0.05, seg=10, rings=8), col("E5484D", 0.4))
    b.add(sphere((-0.24, 1.12, rig.surface_z(1.12) + 0.05), 0.022, seg=8, rings=6), col("FFD23F", 0.4))
    b.build(parent=root)
    for s in (-1, 1):
        leg(root, rig, s, col("2A1E2E", 0.35), SHOE_BLACK, spat=col("F3E6C8", 0.3))

    def cane(m, hc):
        m.add(rod((hc.x + 0.02, hc.y + 0.02, hc.z + 0.03), (hc.x + 0.06, 0.02, hc.z + 0.12), 0.024), col("1C1820", 0.75))
        m.add(sphere((hc.x + 0.02, hc.y + 0.05, hc.z + 0.03), 0.055), BRASS)
        m.add(cyl((hc.x + 0.06, 0.04, hc.z + 0.12), 0.03, 0.06, "y"), BRASS)
    arm(root, rig, 1, suit, WHITE_GLOVE, cuff=WHITE)
    arm(root, rig, -1, suit, WHITE_GLOVE, cuff=WHITE, extra=cane)
    h = head_base(root, rig, skin=col("F5D5BA", 0.3))
    eyes(h, rig, size=0.9, lids=0.25, lid_mat=col("F5D5BA", 0.3), brows=12, brow_mat=col("F7F2EA", 0.3))
    cheeks(h, rig, col("F29080", 0.2))
    # walrus moustache and mutton chops
    my = rig.head_y - 0.13
    for s in (-1, 1):
        h.add(sphere((s * 0.08, my, 0.3), 0.08, (1.4, 0.65, 0.8), seg=14, rings=10), col("F7F2EA", 0.3), transform=rot_about((s * 0.08, my, 0.3), "Z", -s * 14))
        h.add(sphere((s * 0.3, rig.head_y - 0.08, 0.06), 0.09, (0.6, 1.3, 0.9), seg=14, rings=10), col("F7F2EA", 0.3))
    # monocle on a chain
    h.add(torus((0.12, rig.head_y + 0.02, 0.325), 0.075, 0.013, "z", seg=20), BRASS)
    h.add(sphere((0.12, rig.head_y + 0.02, 0.32), 0.065, (1, 1, 0.2)), gls("DDEBF2", 0.35))
    h.add(sweep([(0.19, rig.head_y - 0.02, 0.31), (0.25, rig.head_y - 0.2, 0.25), (0.28, rig.head_y - 0.36, 0.2)], 0.005, seg=5), BRASS)
    # top hat
    hy = rig.head_y + 0.27
    h.add(cyl((0, hy + 0.27, 0), 0.24, 0.5, "y", radius2=0.26, bevel=0.015), col("16121A", 0.65))
    h.add(cyl((0, hy, 0), 0.42, 0.04, "y", bevel=0.015), col("16121A", 0.65))
    h.add(cyl((0, hy + 0.09, 0), 0.245, 0.09, "y"), col("E5484D", 0.4))
    h.build(origin=(0, rig.neck, 0), parent=root)


def mirror_mover(root):
    rig = ADULT
    over = col("2F5FA8", 0.25)
    shirt = col("E8D9B8", 0.3)
    b = Model("Body")
    torso(b, rig, shirt)
    neck(b, rig, col("D9A066", 0.3))
    # overalls: trousers part of the torso, bib with pocket and brass buckles, straps
    b.add(lathe([(0.0, rig.hip - 0.1), (0.25, rig.hip - 0.1), (0.278, rig.hip - 0.04), (0.278, rig.hip + 0.06), (0.258, rig.hip + 0.24), (0.0, rig.hip + 0.24)], (0, 0, 0), seg=32),
          over, transform=Matrix.Diagonal(V(1, 1, rig.depth * 1.02, 1)))
    zb = rig.surface_z(0.98)
    b.add(box((-0.17, 0.8, zb - 0.06), (0.17, 1.08, zb + 0.03), 0.02), over)
    b.add(box((-0.08, 0.86, zb + 0.02), (0.08, 0.98, zb + 0.04), 0.01), col("244B87", 0.25))
    for s in (-1, 1):
        b.add(sweep([(s * 0.14, 1.06, zb + 0.01), (s * 0.16, 1.2, 0.14), (s * 0.15, 1.24, -0.08), (s * 0.1, 0.9, -0.22)], 0.025), over)
        b.add(boxc((s * 0.14, 1.05, zb + 0.04), (0.05, 0.05, 0.02), 0.01), BRASS)
    # both arms hold the mirror at the side
    hand_mat = col("D9A066", 0.3)
    sh_r = (rig.shoulder_x, rig.shoulder - 0.04, 0.0)
    arm_geo(b, rig, shirt, hand_mat, sh_r, (0.42, 1.12, 0.12), (0.4, 1.3, 0.1))
    b.add(sphere(sh_r, rig.arm_r * 1.25, seg=16, rings=10), shirt)
    sh_l = (-rig.shoulder_x, rig.shoulder - 0.04, 0.0)
    arm_geo(b, rig, shirt, hand_mat, sh_l, (-0.18, 0.88, 0.28), (0.18, 0.72, 0.3))
    b.add(sphere(sh_l, rig.arm_r * 1.25, seg=16, rings=10), shirt)
    b.build(parent=root)
    for s in (-1, 1):
        leg(root, rig, s, over, col("6B3F22", 0.4), cuff=col("244B87", 0.25))
    h = head_base(root, rig, skin=hand_mat)
    eyes(h, rig, look=(0.6, 0), brows=-6, brow_mat=col("2A1E2E", 0.4))
    smile(h, rig, width=0.05)
    cheeks(h, rig)
    # sideburns and a work cap
    for s in (-1, 1):
        h.add(sphere((s * 0.3, rig.head_y + 0.02, 0.04), 0.06, (0.5, 1.3, 0.8), seg=10, rings=8), col("2A1E2E", 0.4))
    cap = col("E04E39", 0.35)
    h.add(cut_sphere((0, rig.head_y + 0.06, 0), rig.head_r + 0.04, (1.02, 0.9, 1.02), plane_co=(0, rig.head_y + 0.1, 0), plane_no=(0, 1, 0), seg=28, rings=16), cap)
    h.add(cyl((0, rig.head_y + 0.11, 0), rig.head_r + 0.045, 0.04, "y"), col("B83A2A", 0.35))
    h.add(hull([(-0.19, rig.head_y + 0.12, 0.22), (0.19, rig.head_y + 0.12, 0.22), (-0.16, rig.head_y + 0.07, 0.5), (0.16, rig.head_y + 0.07, 0.5),
                (-0.19, rig.head_y + 0.145, 0.22), (0.19, rig.head_y + 0.145, 0.22), (-0.16, rig.head_y + 0.095, 0.5), (0.16, rig.head_y + 0.095, 0.5)], 0.012), cap)
    h.add(sphere((0, rig.head_y + 0.42, 0), 0.03), cap)
    h.build(origin=(0, rig.neck, 0), parent=root)
    # the giant gilded mirror, standing beside them
    mm = Model("Mirror")
    cx, cy = 0.82, 0.98
    gold = met("D7A84A", 0.75)
    mm.add(torus((cx, cy, 0.0), 0.38, 0.065, "z", scale=(1.0, 2.05, 1.0), seg=40), gold)
    mm.add(sphere((cx, cy, -0.01), 0.38, (0.95, 2.0, 0.06), seg=28, rings=16), met("DDEBF2", 0.97))
    mm.add(sphere((cx, cy + 0.92, 0.0), 0.09), gold)
    mm.add(prism([(cx - 0.14, cy + 0.84), (cx + 0.14, cy + 0.84), (cx, cy + 1.02)], -0.03, 0.03), gold)
    for s in (-1, 1):
        mm.add(sphere((cx + s * 0.3, cy + 0.62, 0.0), 0.05), gold)
    mm.add(sphere((cx, cy - 0.83, 0.0), 0.07), gold)
    mm.add(sphere((cx - 0.13, cy + 0.3, 0.05), 0.06, (1, 2.4, 0.3)), col("FFFFFF", 0.9))
    mm.add(sphere((cx + 0.1, cy - 0.2, 0.05), 0.035, (1, 1.8, 0.3)), col("FFFFFF", 0.9))
    mm.build(origin=(cx, 0, 0), parent=root)


def houseplant(root):
    pot = col("C8643C", 0.25)
    lift = 0.14
    b = Model("Body")
    prof = [(0.0, 0), (0.27, 0), (0.3, 0.05), (0.37, 0.56), (0.43, 0.6), (0.44, 0.72), (0.0, 0.72)]
    b.add(lathe([(r, y + lift) for r, y in prof], (0, 0, 0), seg=36), pot)
    b.add(cyl((0, 0.72 + lift, 0), 0.4, 0.03, "y"), col("4A3020", 0.1))
    # art deco bands near the foot of the pot and a darker rim
    for y0, hgt in ((0.09, 0.035), (0.15, 0.018)):
        r0 = 0.3 + (0.37 - 0.3) * (y0 - 0.05) / 0.51
        b.add(lathe([(r0 + 0.008, y0 + lift), (r0 + 0.012, y0 + hgt + lift), (0.0, y0 + hgt + lift)], (0, 0, 0), seg=36), col("F3E6C8", 0.3))
    b.add(torus((0, 0.6 + lift, 0), 0.43, 0.03, "y", seg=36), col("A84E2C", 0.25))
    # face on the pot
    h = Rig(head_y=0.47 + lift, head_r=0.36)
    eyes(b, h, size=1.1, spread=0.15, y_off=0.0)
    smile(b, h, width=0.06, y_off=-0.24)
    cheeks(b, h)
    # a raffia bow
    for s in (-1, 1):
        b.add(sphere((0.28 + s * 0.05, 0.64 + lift, 0.3), 0.05, (1.2, 0.7, 0.6)), col("E8D27A", 0.2))
    b.add(sphere((0.28, 0.64 + lift, 0.32), 0.03), col("E8D27A", 0.2))
    b.build(parent=root)
    # little feet under the pot
    for s in (-1, 1):
        m = Model("LegL" if s > 0 else "LegR")
        x = s * 0.13
        m.add(rod((x, lift + 0.04, 0), (x, 0.06, 0), 0.04, seg=10), col("2C7A3B", 0.4))
        m.add(box((x - 0.07, 0.0, -0.06), (x + 0.07, 0.08, 0.12), 0.035, seg=2), col("E5484D", 0.45))
        m.build(origin=(x, lift + 0.04, 0), parent=root)
    empty("Head", (0, 1.65, 0), parent=root)
    leaves = empty("Leaves", (0, 0.76 + lift, 0), parent=root)
    base_y = 0.76 + lift
    for i in range(7):
        a = i * (360 / 7) + 10
        tilt = 20 + 14 * (i % 3)
        length = 0.72 + 0.1 * ((i * 37) % 3) / 2
        d = V(math.sin(math.radians(a)) * math.sin(math.radians(tilt)), math.cos(math.radians(tilt)),
              math.cos(math.radians(a)) * math.sin(math.radians(tilt)))
        base = V(0, base_y, 0)
        tip = base + d * length
        leaf = Model(f"Leaf{i}")
        leaf.add(sweep([tuple(base), tuple(base + d * length * 0.5 + V(0, 0.04, 0)), tuple(tip)], 0.022, seg=6), col("2C7A3B", 0.4))
        outward = V(d.x, 0.0, d.z).normalized() if abs(d.y) < 0.99 else V(0, 0, 1)
        leaf.add(leaf_blade(tip - outward * 0.03, outward + V(0, 0.55, 0), 0.56, 0.27, bend=0.3, notches=3, roll=62),
                 col("3FA34D" if i % 2 else "35914A", 0.45))
        leaf.build(origin=(0, base_y, 0), parent=leaves, parent_origin=(0, base_y, 0))


BUILDERS = {
    "Commuter": commuter, "Houseplant": houseplant, "Mirror": mirror_mover, "Vampire": vampire, "Courier": courier,
    "Swimmer": swimmer, "Kid": kid, "Tycoon": tycoon, "Bellhop": bellhop,
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(BUILDERS)
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    export = "--no-export" not in argv
    for name in only:
        reset_scene()
        root = empty(f"Char_{name}")
        BUILDERS[name](root)
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in root.children_recursive if o.type == "MESH")
        print(f"built {name}: {tris} tris")
        if export:
            export_fbx(os.path.join(OUT, f"Char_{name}.fbx"), [root])
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "blend", f"Char_{name}.blend"))
            print("exported", name)
        if preview:
            setup_preview()
            render_views(os.path.join(preview, f"Char_{name}.png"), [root], size=(420, 560), views=((8, 200), (4, 140)), lens=60, margin=1.2)


if __name__ == "__main__":
    os.makedirs(os.path.join(ROOT, "ArtSource", "blend"), exist_ok=True)
    main()
