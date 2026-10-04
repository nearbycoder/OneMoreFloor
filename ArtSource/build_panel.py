"""The operator's panel, rendered as UI art: brass plate, bakelite buttons (normal + lit), dial face.

    blender -b -P ArtSource/build_panel.py

Built directly in Blender space (the plate faces -Y toward an orthographic camera) and rendered with
Eevee onto transparent backgrounds. Writes Assets/Resources/Icons/panel_*.png.
"""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Icons")


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple((v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4) for v in c) + (1.0,)


def mat(name, color, metal=0.0, rough=0.4, emit=None, strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = srgb(color)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = srgb(emit)
        b.inputs["Emission Strength"].default_value = strength
    return m


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def scene_setup(w, h, ortho, center=(0, 0, 0)):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.eevee.taa_render_samples = 48
    sc.render.film_transparent = True
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.3, 0.28, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    sc.world = world
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    cam.location = (center[0], -20, center[2])
    cam.rotation_euler = (math.radians(90), 0, 0)
    sc.collection.objects.link(cam)
    sc.camera = cam
    # big soft lights + bright cards for the brass to reflect
    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
    key.data.energy = 2500
    key.data.size = 12
    key.location = (-6, -10, 9)
    key.rotation_euler = (math.radians(50), 0, math.radians(-30))
    sc.collection.objects.link(key)
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "AREA"))
    fill.data.energy = 900
    fill.data.size = 10
    fill.location = (8, -9, -4)
    fill.rotation_euler = (math.radians(110), 0, math.radians(40))
    sc.collection.objects.link(fill)
    for i, (x, z, s) in enumerate(((-9, 8, 1.0), (9, -6, 0.5), (0, 14, 0.7))):
        bpy.ops.mesh.primitive_plane_add(size=10, location=(x, -6, z))
        p = bpy.context.object
        p.rotation_euler = (math.radians(90), 0, math.atan2(-x, 6) if x else 0)
        p.data.materials.append(mat(f"card{i}", "FFF4E0", emit="FFF4E0", strength=s * 3))
        p.visible_camera = False


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def add_box(name, size, loc, material, bevel=0.05, segs=3):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        b = o.modifiers.new("bevel", "BEVEL")
        b.width = bevel
        b.segments = segs
        b.limit_method = "NONE"
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def add_cyl(loc, r, depth, material, rot=(math.radians(90), 0, 0), verts=48, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    o = bpy.context.object
    if bevel:
        b = o.modifiers.new("bevel", "BEVEL")
        b.width = bevel
        b.segments = 3
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def add_sphere(loc, r, scale, material):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=48, ring_count=24)
    o = bpy.context.object
    o.scale = scale
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


def plate():
    reset()
    W, H = 4.0, 9.0
    scene_setup(800, 1800, H * 1.0)
    brass = mat("brass", "D7A84A", 1.0, 0.28)
    brass_dark = mat("brass_dark", "9C7230", 1.0, 0.4)
    lacquer = mat("lacquer", "2A1E2E", 0.0, 0.25)
    add_box("frame", (W, 0.3, H), (0, 0, 0), brass, 0.12)
    add_box("inset", (W - 0.36, 0.2, H - 0.36), (0, -0.08, 0), lacquer, 0.08)
    # engraved double border on the lacquer
    for inset in (0.32, 0.4):
        w, h = W - inset * 2 - 0.1, H - inset * 2 - 0.1
        for (sx, sz, lx, lz) in ((w, 0.025, 0, h / 2), (w, 0.025, 0, -h / 2), (0.025, h, w / 2, 0), (0.025, h, -w / 2, 0)):
            add_box("line", (sx, 0.04, sz), (lx, -0.19, lz), brass_dark, 0)
    # sunburst behind the dial (dial sits at ~1/3 from the top)
    cz = H / 2 - 1.9
    for k in range(17):
        a = math.radians(180 + k * 180 / 16)
        L = 1.75
        bpy.ops.mesh.primitive_cube_add(size=1, location=(math.cos(a) * L / 2 * -1, -0.19, cz + math.sin(a) * L / 2 * -1))
        o = bpy.context.object
        o.scale = (L, 0.03, 0.03)
        o.rotation_euler = (0, -a, 0)
        o.data.materials.append(brass_dark)
    # rivets
    for x in (-W / 2 + 0.17, W / 2 - 0.17):
        for z in (-H / 2 + 0.17, H / 2 - 0.17, 0):
            add_sphere((x, -0.15, z), 0.06, (1, 0.6, 1), brass)
    # name plate under the dial
    add_box("nameplate", (2.6, 0.06, 0.45), (0, -0.19, cz - 1.75), brass, 0.04)
    render(os.path.join(OUT, "panel_plate.png"))


def button(lit):
    reset()
    scene_setup(256, 256, 2.4)
    brass = mat("brass", "D7A84A", 1.0, 0.25)
    ivory = mat("ivory", "F1E4C6", 0.0, 0.22)
    amber = mat("amber", "FF9F1C", 0.0, 0.2, emit="FF8A00", strength=1.6)
    add_cyl((0, 0, 0), 1.0, 0.25, brass, bevel=0.06)
    add_cyl((0, -0.1, 0), 0.82, 0.2, mat("shadow", "3A2A20", 0.0, 0.6))
    add_sphere((0, -0.18, 0), 0.74, (1, 0.45, 1), amber if lit else ivory)
    render(os.path.join(OUT, "panel_button_lit.png" if lit else "panel_button.png"))


def dial():
    reset()
    scene_setup(512, 512, 2.3)
    brass = mat("brass", "D7A84A", 1.0, 0.25)
    face = mat("face", "F3E6C8", 0.0, 0.5)
    ink = mat("ink", "2A1E2E", 0.0, 0.5)
    add_cyl((0, 0, 0), 1.05, 0.2, brass, bevel=0.06)
    add_cyl((0, -0.08, 0), 0.92, 0.12, face)
    for k in range(9):
        a = math.radians(150 - k * 120 / 8)
        r = 0.72
        bpy.ops.mesh.primitive_cube_add(size=1, location=(math.cos(a) * r, -0.15, math.sin(a) * r - 0.08))
        o = bpy.context.object
        o.scale = (0.14, 0.02, 0.035)
        o.rotation_euler = (0, -a, 0)
        o.data.materials.append(ink)
    for k in range(33):
        a = math.radians(150 - k * 120 / 32)
        r = 0.8
        bpy.ops.mesh.primitive_cube_add(size=1, location=(math.cos(a) * r, -0.15, math.sin(a) * r - 0.08))
        o = bpy.context.object
        o.scale = (0.05, 0.02, 0.012)
        o.rotation_euler = (0, -a, 0)
        o.data.materials.append(ink)
    render(os.path.join(OUT, "panel_dial.png"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    plate()
    button(False)
    button(True)
    dial()
    print("panel art done")
