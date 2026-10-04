"""Icon sprites rendered from little 3D objects (and the character models) in Blender.

    blender -b -P ArtSource/build_icons.py -- [--only floor_lobby,kind_vampire]

floor_*  : what each floor is about (bell, briefcase, books, ...) used in bubbles and on panel plates
kind_*   : character portraits for the intro cards
badge_*  : rule badges on speech bubbles (sun, mirror, fangs, parcel, wave, balloons, top hat)
Rendered with a transparent background, then given a soft dark outline so they read on any colour.
Writes Assets/Resources/Icons/*.png.
"""
import math
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from omf_lib import *  # noqa: E402,F401,F403
import build_characters as chars  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Icons")


# ----------------------------------------------------------------------------- floor icons

def icon_lobby(m):
    m.add(lathe([(0.0, 0), (0.62, 0), (0.62, 0.06), (0.55, 0.12), (0.5, 0.3), (0.32, 0.55), (0.12, 0.62), (0.0, 0.64)], (0, 0, 0), seg=40), met("D7A84A", 0.85))
    m.add(cyl((0, 0.7, 0), 0.06, 0.12, "y"), met("D7A84A", 0.85))
    m.add(sphere((0, 0.8, 0), 0.08), met("D7A84A", 0.85))
    m.add(cyl((0, -0.05, 0), 0.75, 0.1, "y"), col("C8323F", 0.4))


def icon_office(m):
    m.add(box((-0.7, -0.45, -0.25), (0.7, 0.45, 0.25), 0.08), col("6B3F22", 0.5))
    m.add(box((-0.72, 0.05, -0.27), (0.72, 0.12, 0.27)), col("4E2E1C", 0.5))
    m.add(torus((0, 0.52, 0), 0.24, 0.06, "z", arc=0.5), col("2A1E2E", 0.4))
    for x in (-0.4, 0.4):
        m.add(box((x - 0.08, 0.0, -0.28), (x + 0.08, 0.16, -0.24)), met("D7A84A", 0.8))


def icon_library(m):
    colors = ["8E2433", "2F6F6A", "D7A84A", "3D5A80"]
    for i, c in enumerate(colors):
        y = -0.55 + i * 0.3
        w = 0.75 - i * 0.06
        m.add(box((-w, y, -0.38), (w, y + 0.26, 0.38), 0.04), col(c, 0.4))
        m.add(box((-w + 0.02, y + 0.04, -0.4), (w - 0.06, y + 0.22, 0.36)), col("F6EFE0", 0.3))
    m.add(xf(box((-0.55, 0.65, -0.3), (0.55, 0.72, 0.3), 0.02), rot_about((0, 0.68, 0), "Z", 8)), col("F6EFE0", 0.3))


def icon_laundromat(m):
    m.add(box((-0.6, -0.7, -0.4), (0.6, 0.7, 0.4), 0.1), col("F7F2EA", 0.4))
    m.add(box((-0.6, 0.45, -0.42), (0.6, 0.7, -0.38)), col("3FBFC0", 0.4))
    m.add(torus((0, -0.1, -0.42), 0.36, 0.07, "z", seg=32), met("C9CED6", 0.85))
    m.add(cyl((0, -0.1, -0.4), 0.33, 0.05, "z", seg=32), col("6EC6F0", 0.7))
    m.add(sphere((0.38, 0.57, -0.42), 0.06), col("FF5E5E", 0.5))


def icon_boiler(m):
    m.add(cyl((0, 0, 0), 0.62, 0.22, "z", seg=40), met("B5583A", 0.5))
    m.add(cyl((0, 0, -0.1), 0.52, 0.06, "z", seg=40), col("F6EFE0", 0.4))
    for k in range(9):
        a = math.radians(-30 + k * 30)
        m.add(boxc((0.4 * math.cos(a), 0.4 * math.sin(a), -0.15), (0.05, 0.05, 0.02)), col("2A1E2E", 0.3))
    m.add(xf(box((-0.03, 0, -0.17), (0.03, 0.42, -0.14)), rot_about((0, 0, -0.15), "Z", -35)), col("C0262D", 0.4))
    m.add(sphere((0, 0, -0.16), 0.06), met("D7A84A", 0.8))
    m.add(sphere((0, -0.82, 0), 0.22, (1, 1.4, 1)), glo("FF7A2A", 2.5))


def icon_greenhouse(m):
    m.add(lathe([(0.3, -0.7), (0.38, -0.25), (0.42, -0.2), (0.0, -0.2)], (0, 0, 0)), col("C8643C", 0.3))
    m.add(rod((0, -0.2, 0), (0, 0.35, 0), 0.04), col("2C7A3B", 0.4))
    for s in (-1, 1):
        m.add(sphere((s * 0.25, 0.35, 0), 0.24, (1.3, 0.45, 0.7)), col("5EBB4A", 0.5), transform=rot_about((0, 0.3, 0), "Z", -s * 25))
    m.add(sphere((0.55, 0.7, 0.2), 0.22), glo("FFD23F", 2.2))


def icon_penthouse(m):
    pts = [(-0.7, -0.4), (0.7, -0.4), (0.75, 0.35), (0.35, 0.05), (0.0, 0.5), (-0.35, 0.05), (-0.75, 0.35)]
    m.add(prism([(-0.7, -0.4), (0.7, -0.4), (0.7, 0.0), (-0.7, 0.0)], -0.2, 0.2, 0.04), met("D7A84A", 0.85))
    for x, h in ((-0.6, 0.45), (0.0, 0.6), (0.6, 0.45)):
        m.add(hull([(x - 0.18, 0, -0.18), (x + 0.18, 0, -0.18), (x - 0.18, 0, 0.18), (x + 0.18, 0, 0.18), (x, h, 0)]), met("D7A84A", 0.85))
        m.add(sphere((x, h + 0.04, 0), 0.08), col("7B4FC0", 0.8))
    m.add(sphere((0, -0.2, -0.22), 0.1), col("C8323F", 0.8))


def icon_crypt(m):
    prof = [(-0.32, -0.75), (0.32, -0.75), (0.45, 0.25), (0.25, 0.8), (-0.25, 0.8), (-0.45, 0.25)]
    m.add(hull([(x, y, z) for x, y in prof for z in (-0.18, 0.18)], 0.03), col("5A3A30", 0.45))
    m.add(box((-0.04, -0.45, -0.2), (0.04, 0.45, -0.17)), met("D7A84A", 0.8))
    m.add(box((-0.25, 0.15, -0.2), (0.25, 0.23, -0.17)), met("D7A84A", 0.8))
    m.add(sphere((0.55, 0.65, 0), 0.12, (1, 1.5, 1)), glo("7CFFB2", 3.0))


def icon_ocean(m):
    pts = []
    for k in range(25):
        a = k / 24 * math.pi * 1.5
        pts.append((-0.7 + k * 0.06, -0.1 + 0.32 * math.sin(a) * (k / 24), 0))
    m.add(sweep(pts, 0.12), col("1F86D0", 0.6))
    m.add(sweep([(p[0], p[1] - 0.28, p[2]) for p in pts], 0.12), col("2E9BE0", 0.6))
    m.add(sweep([(p[0], p[1] + 0.13, p[2] - 0.02) for p in pts[10:]], 0.05), col("F7FBFF", 0.5))
    m.add(sphere((0.55, 0.6, 0), 0.2), glo("FFE066", 2.0))


def icon_daycare(m):
    for c, p in (("E5484D", (-0.32, -0.32)), ("3FA9F5", (0.32, -0.32)), ("FFD23F", (0.0, 0.3))):
        m.add(boxc((p[0], p[1], 0), (0.56, 0.56, 0.56), 0.06), col(c, 0.4))
    m.add(boxc((0.0, 0.3, -0.29), (0.25, 0.25, 0.02)), col("F7F2EA", 0.4))


# ----------------------------------------------------------------------------- badges

def badge_sun(m):
    m.add(sphere((0, 0, 0), 0.42), glo("FFD23F", 1.4))
    for k in range(10):
        a = math.radians(k * 36)
        m.add(xf(boxc((0.68 * math.cos(a), 0.68 * math.sin(a), 0), (0.2, 0.08, 0.08), 0.02), rot_about((0.68 * math.cos(a), 0.68 * math.sin(a), 0), "Z", k * 36)), col("FFB000", 0.4))


def badge_mirror(m):
    m.add(torus((0, 0, 0), 0.42, 0.08, "z", scale=(1, 1.5, 1), seg=32), met("D7A84A", 0.85))
    m.add(sphere((0, 0, 0.01), 0.42, (0.95, 1.45, 0.06)), met("DDEBF2", 0.97))


def badge_fangs(m):
    m.add(torus((0, 0.1, 0), 0.55, 0.13, "z", arc=0.5, seg=24), col("B3122E", 0.5), transform=rot_about((0, 0.1, 0), "Z", 180))
    for s in (-1, 1):
        m.add(rod((s * 0.25, -0.2, -0.05), (s * 0.25, -0.65, -0.05), 0.12, radius2=0.01, seg=12), WHITE)


def badge_parcel(m):
    m.add(boxc((0, 0, 0), (1.1, 0.8, 0.8), 0.06), col("C8955A", 0.3))
    m.add(boxc((0, 0, 0), (1.12, 0.12, 0.82)), col("E8D27A", 0.3))
    m.add(boxc((0.22, -0.18, -0.41), (0.4, 0.2, 0.02)), col("E5484D", 0.4))


def badge_balloon(m):
    m.add(sphere((0, 0.15, 0), 0.5, (0.9, 1.1, 0.9)), col("E5484D", 0.75))
    m.add(rod((0, -0.42, 0), (0.05, -0.9, 0), 0.02), INK)
    m.add(sphere((-0.15, 0.35, -0.38), 0.1, (1, 1.4, 0.5)), WHITE)


def badge_tophat(m):
    m.add(cyl((0, 0.15, 0), 0.4, 0.8, "y"), col("16121A", 0.6))
    m.add(cyl((0, -0.25, 0), 0.65, 0.08, "y"), col("16121A", 0.6))
    m.add(cyl((0, -0.12, 0), 0.41, 0.14, "y"), col("E5484D", 0.4))


ICONS = {
    "floor_lobby": icon_lobby, "floor_office": icon_office, "floor_library": icon_library, "floor_laundromat": icon_laundromat,
    "floor_boiler": icon_boiler, "floor_greenhouse": icon_greenhouse, "floor_penthouse": icon_penthouse, "floor_crypt": icon_crypt,
    "floor_ocean": icon_ocean, "floor_daycare": icon_daycare,
    "badge_sun": badge_sun, "badge_mirror": badge_mirror, "badge_fangs": badge_fangs, "badge_parcel": badge_parcel,
    "badge_wave": icon_ocean, "badge_balloon": badge_balloon, "badge_tophat": badge_tophat,
}

KINDS = {"kind_commuter": "Commuter", "kind_houseplant": "Houseplant", "kind_mirror": "Mirror", "kind_vampire": "Vampire",
         "kind_courier": "Courier", "kind_swimmer": "Swimmer", "kind_kid": "Kid", "kind_tycoon": "Tycoon", "kind_bellhop": "Bellhop"}


def setup_icon_scene(size):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = 32
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = size
    world = bpy.data.worlds.new("icons")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.9, 0.92, 1.0, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    scene.world = world
    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "SUN"))
    key.data.energy = 3.5
    key.rotation_euler = (math.radians(50), math.radians(25), math.radians(-25))
    scene.collection.objects.link(key)
    rim = bpy.data.objects.new("rim", bpy.data.lights.new("rim", "SUN"))
    rim.data.energy = 1.5
    rim.rotation_euler = (math.radians(-40), math.radians(-30), math.radians(160))
    scene.collection.objects.link(rim)


def render_icon(objects, path, size=256, front_z=-1, tilt=12, yaw=-18, fill=0.86, bust=False):
    """front_z = -1: look at the object's -z face (Unity camera side). +1 for characters (they face +z).
    bust: frame a character's head and shoulders (hats included) instead of the whole body."""
    scene = bpy.context.scene
    mins, maxs = bounds(objects)
    center = (mins + maxs) / 2
    ext = max(maxs - mins)
    head = next((o for r in objects for o in r.children_recursive if o.name == "Head" and o.type == "MESH"), None)
    if bust and head:
        hmins, hmaxs = bounds([head])
        top, bottom = hmaxs.z + 0.12, hmins.z - 0.4
        center = Vector((0.0, (hmins.y + hmaxs.y) / 2, (top + bottom) / 2))
        ext = max(top - bottom, (hmaxs.x - hmins.x) + 0.45)
    cam_data = bpy.data.cameras.new("icon_cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ext / fill
    cam = bpy.data.objects.new("icon_cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    # Unity -z == Blender +y; Unity +z == Blender -y
    el, az = math.radians(tilt), math.radians(yaw)
    by = 1 if front_z < 0 else -1
    d = Vector((math.sin(az) * math.cos(el) * by, by * math.cos(az) * math.cos(el), math.sin(el)))
    cam.location = center + d * (ext * 4)
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    outline(path)


def outline(path, radius=3, color=(0.12, 0.08, 0.14)):
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    a = px[:, :, 3]
    grown = a.copy()
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            if dx * dx + dy * dy > radius * radius:
                continue
            grown = np.maximum(grown, np.roll(np.roll(a, dy, 0), dx, 1))
    out = np.zeros_like(px)
    out[:, :, 0:3] = np.array(color)
    out[:, :, 3] = grown
    # composite original over the outline
    fa = a[:, :, None]
    out[:, :, 0:3] = px[:, :, 0:3] * fa + out[:, :, 0:3] * (1 - fa)
    out[:, :, 3] = np.maximum(a, grown)
    img.pixels[:] = out.ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None
    os.makedirs(OUT, exist_ok=True)
    for name, fn in ICONS.items():
        if only and name not in only:
            continue
        reset_scene()
        setup_icon_scene(256)
        root = empty(name)
        m = Model(name)
        fn(m)
        m.build(parent=root)
        render_icon([root], os.path.join(OUT, name + ".png"), tilt=10, yaw=-15)
        print("icon", name)
    for name, kind in KINDS.items():
        if only and name not in only:
            continue
        reset_scene()
        setup_icon_scene(320)
        root = empty(name)
        chars.BUILDERS[kind](root)
        render_icon([root], os.path.join(OUT, name + ".png"), size=320, front_z=1, tilt=6, yaw=-20, fill=0.92, bust=True)
        print("icon", name)


if __name__ == "__main__":
    main()
