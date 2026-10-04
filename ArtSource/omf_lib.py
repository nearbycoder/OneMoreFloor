"""Modeling helpers for One More Floor (Blender 4.5, run headless with `blender -b -P ...`).

Geometry is authored in *Unity* coordinates (x right, y up, z forward = away from the game camera)
and converted to Blender space only when an object is built. The FBX export settings in
`export_fbx` map Blender (x, y, z) -> Unity (-x, z, -y); `U2B` is the inverse of that mapping.

Materials are named by look so Unity can rebuild shared URP materials at runtime:
    col_RRGGBB[_sNN]   painted / plastic / fabric (NN = smoothness x100, default 35)
    met_RRGGBB[_sNN]   metallic (brass, chrome)
    gls_RRGGBB[_aNN]   transparent glass (NN = alpha x100)
    glo_RRGGBB[_iNN]   emissive (NN = intensity x10)
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

U2B = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
AXIS_ROT = {
    "x": Matrix.Rotation(math.pi / 2, 4, "Y"),   # primitive's +z -> +x
    "y": Matrix.Rotation(-math.pi / 2, 4, "X"),  # primitive's +z -> +y
    "z": Matrix.Identity(4),
}


def V(*a):
    return Vector(a[0]) if len(a) == 1 else Vector(a)


# ----------------------------------------------------------------------------- palette & materials

def hx(h):
    return h.lstrip("#").upper()


def shade(h, f):
    """f > 1 lightens toward white, f < 1 darkens."""
    h = hx(h)
    rgb = [int(h[i:i + 2], 16) for i in (0, 2, 4)]
    rgb = [int(c + (255 - c) * (f - 1)) if f >= 1 else int(c * f) for c in rgb]
    return "".join(f"{max(0, min(255, c)):02X}" for c in rgb)


def mix(a, b, t):
    a, b = hx(a), hx(b)
    return "".join(f"{int(int(a[i:i+2],16)*(1-t)+int(b[i:i+2],16)*t):02X}" for i in (0, 2, 4))


def col(h, s=None):
    return f"col_{hx(h)}" + (f"_s{int(s*100):02d}" if s is not None else "")


def met(h, s=None):
    return f"met_{hx(h)}" + (f"_s{int(s*100):02d}" if s is not None else "")


def gls(h, a=0.3):
    return f"gls_{hx(h)}_a{int(a*100):02d}"


def glo(h, i=2.0):
    return f"glo_{hx(h)}_i{int(i*10):02d}"


BRASS = met("D7A84A", 0.72)
BRASS_DARK = met("9C7230", 0.6)
CHROME = met("C9CED6", 0.85)
INK = col("2A1E2E", 0.3)
WHITE = col("F7F2EA", 0.4)
CREAM = col("F3E6C8", 0.3)
OXBLOOD = col("6E2A33", 0.15)
WOOD = col("7A4A2E", 0.35)
WOOD_DARK = col("4E2E1C", 0.35)
BLACK = col("1C1820", 0.4)


def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def get_material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    parts = name.split("_")
    kind, h = parts[0], parts[1]
    extra = {p[0]: int(p[1:]) for p in parts[2:] if len(p) > 1 and p[1:].isdigit()}
    rgb = [_srgb_to_linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)]
    smooth = extra.get("s", {"met": 72, "gls": 92}.get(kind, 35)) / 100
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Metallic"].default_value = 0.9 if kind == "met" else 0.0
    bsdf.inputs["Roughness"].default_value = 1 - smooth
    mat.diffuse_color = (*rgb, 1)
    if kind == "glo":
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1)
        bsdf.inputs["Emission Strength"].default_value = extra.get("i", 20) / 10
    if kind == "gls":
        bsdf.inputs["Alpha"].default_value = extra.get("a", 30) / 100
        mat.surface_render_method = "BLENDED"
    return mat


# ----------------------------------------------------------------------------- primitives
# Each returns (bmesh in Unity space, smooth flag). Model.add() merges them.

def _flat_caps(bm, axis):
    for f in bm.faces:
        f.smooth = abs(f.normal.normalized().dot(axis)) < 0.9
    for e in bm.edges:
        if len(e.link_faces) == 2 and e.link_faces[0].smooth != e.link_faces[1].smooth:
            e.smooth = False


def box(mn, mx, bevel=0.0, seg=2):
    mn, mx = V(mn), V(mx)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    size = mx - mn
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(mn + mx) / 2, verts=bm.verts)
    if bevel > 0:
        b = min(bevel, min(size) * 0.49)
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=b, offset_type="OFFSET", segments=seg,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    bm.normal_update()
    return bm, ("auto" if bevel > 0 and seg >= 2 else False)


def boxc(center, size, bevel=0.0, seg=2):
    c, s = V(center), V(size) / 2
    return box(c - s, c + s, bevel, seg)


def cyl(center, radius, length, axis="y", radius2=None, seg=24, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=length)
    if bevel > 0:
        bm.normal_update()
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and
               (abs(e.link_faces[0].normal.z) > 0.9) != (abs(e.link_faces[1].normal.z) > 0.9)]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel, offset_type="OFFSET", segments=2, profile=0.5,
                        affect="EDGES", clamp_overlap=True)
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis])
    bm.normal_update()
    _flat_caps(bm, {"x": V(1, 0, 0), "y": V(0, 1, 0), "z": V(0, 0, 1)}[axis])
    return bm, None


def rod(p0, p1, radius, seg=12, radius2=None):
    p0, p1 = V(p0), V(p1)
    d = p1 - p0
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=d.length)
    rot = V(0, 0, 1).rotation_difference(d.normalized()).to_matrix().to_4x4()
    bm.transform(Matrix.Translation((p0 + p1) / 2) @ rot)
    bm.normal_update()
    _flat_caps(bm, d.normalized())
    return bm, None


def sphere(center, radius, scale=(1, 1, 1), seg=24, rings=14):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=radius)
    bm.transform(Matrix.Translation(V(center)) @ Matrix.Diagonal(V(*scale, 1.0)))
    bm.normal_update()
    return bm, True


def torus(center, major, minor, axis="y", seg=28, ring_seg=10, arc=1.0, scale=(1, 1, 1)):
    bm = bmesh.new()
    rings = []
    count = seg if arc >= 1.0 else seg + 1
    for i in range(count):
        a = 2 * math.pi * arc * i / seg
        ring = []
        for j in range(ring_seg):
            b = 2 * math.pi * j / ring_seg
            r = major + minor * math.cos(b)
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), minor * math.sin(b))))
        rings.append(ring)
    for i in range(len(rings) - (0 if arc >= 1.0 else 1)):
        r0, r1 = rings[i], rings[(i + 1) % len(rings)]
        for j in range(ring_seg):
            bm.faces.new((r0[j], r1[j], r1[(j + 1) % ring_seg], r0[(j + 1) % ring_seg]))
    if arc < 1.0:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis] @ Matrix.Diagonal(V(*scale, 1.0)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, True


def lathe(profile, center, axis="y", seg=24, smooth=True):
    """Revolve [(radius, height), ...] bottom to top around `axis`; ends capped."""
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        rings.append([bm.verts.new((max(r, 1e-4) * math.cos(2 * math.pi * i / seg), max(r, 1e-4) * math.sin(2 * math.pi * i / seg), h))
                      for i in range(seg)])
    for k in range(len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    if smooth:
        for f in bm.faces:
            f.smooth = True
        _flat_caps(bm, {"x": V(1, 0, 0), "y": V(0, 1, 0), "z": V(0, 0, 1)}[axis])
    return bm, None


def hull(points, bevel=0.0, seg=2):
    bm = bmesh.new()
    for p in points:
        bm.verts.new(V(p))
    res = bmesh.ops.convex_hull(bm, input=bm.verts)
    left = list({g for g in res["geom_interior"] + res["geom_unused"] if isinstance(g, bmesh.types.BMVert)})
    if left:
        bmesh.ops.delete(bm, geom=left, context="VERTS")
    bmesh.ops.dissolve_limit(bm, angle_limit=0.01, verts=bm.verts, edges=bm.edges)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET", segments=seg,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    bm.normal_update()
    return bm, ("auto" if bevel > 0 else False)


def prism(points2d, z0, z1, bevel=0.0):
    """Extrude a convex outline drawn in the xy plane from z0 to z1."""
    return hull([(x, y, z) for x, y in points2d for z in (z0, z1)], bevel)


def sweep(points, radius, closed=False, seg=10, caps=True):
    pts = [V(p) for p in points]
    n = len(pts)
    tans = []
    for i in range(n):
        if closed:
            t = pts[(i + 1) % n] - pts[i - 1]
        elif i == 0:
            t = pts[1] - pts[0]
        elif i == n - 1:
            t = pts[-1] - pts[-2]
        else:
            t = (pts[i + 1] - pts[i]).normalized() + (pts[i] - pts[i - 1]).normalized()
        tans.append(t.normalized())
    ref = V(0, 1, 0) if abs(tans[0].y) < 0.9 else V(1, 0, 0)
    normal = (ref - tans[0] * ref.dot(tans[0])).normalized()
    bm = bmesh.new()
    rings = []
    for i in range(n):
        if i > 0:
            normal = (tans[i - 1].rotation_difference(tans[i]) @ normal).normalized()
        bin_ = tans[i].cross(normal)
        rings.append([bm.verts.new(pts[i] + (normal * math.cos(2 * math.pi * k / seg) + bin_ * math.sin(2 * math.pi * k / seg)) * radius)
                      for k in range(seg)])
    for i in range(n if closed else n - 1):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for k in range(seg):
            bm.faces.new((r0[k], r0[(k + 1) % seg], r1[(k + 1) % seg], r1[k]))
    if not closed and caps:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    for f in bm.faces:
        f.smooth = len(f.verts) == 4
    return bm, None


def arc_pts(center, radius, a0, a1, plane="xy", steps=12):
    c = V(center)
    out = []
    for i in range(steps + 1):
        a = math.radians(a0 + (a1 - a0) * i / steps)
        u, v = math.cos(a) * radius, math.sin(a) * radius
        out.append(c + {"xy": V(u, v, 0), "xz": V(u, 0, v), "zy": V(0, v, u)}[plane])
    return out


def xf(prim, matrix):
    bm, sm = prim
    bm.transform(matrix)
    bm.normal_update()
    return bm, sm


def rot_about(point, axis, deg):
    p = V(point)
    return Matrix.Translation(p) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-p)


# ----------------------------------------------------------------------------- model

def _sharpen(bm, deg=50.0):
    lim = math.radians(deg)
    bm.normal_update()
    for e in bm.edges:
        e.smooth = len(e.link_faces) == 2 and e.calc_face_angle(math.pi) < lim


class Model:
    """Accumulates primitives (one material each) into one mesh object."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.materials = []

    def add(self, prim, material, smooth=None, transform=None):
        part, default = prim
        if transform is not None:
            part.transform(transform)
            part.normal_update()
        if material not in self.materials:
            self.materials.append(material)
        idx = self.materials.index(material)
        flag = default if smooth is None else smooth
        for f in part.faces:
            f.material_index = idx
            if flag is not None:
                f.smooth = flag is True or flag == "auto"
        if flag == "auto":
            _sharpen(part)
        mesh = bpy.data.meshes.new("_part")
        part.to_mesh(mesh)
        part.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)
        return self

    def empty(self):
        return len(self.bm.verts) == 0

    def build(self, origin=(0, 0, 0), parent=None, parent_origin=(0, 0, 0)):
        bm = self.bm
        bmesh.ops.translate(bm, vec=-V(origin), verts=bm.verts)
        bm.transform(U2B)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)  # the axis change mirrors: flip winding back
        bm.normal_update()
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        for m in self.materials:
            mesh.materials.append(get_material(m))
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.location = (U2B @ (V(origin) - V(parent_origin)).to_4d()).to_3d()
        if parent is not None:
            obj.parent = parent
        return obj


def empty(name, origin=(0, 0, 0), parent=None, parent_origin=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = (U2B @ (V(origin) - V(parent_origin)).to_4d()).to_3d()
    obj.parent = parent
    return obj


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.objects, bpy.data.lights, bpy.data.cameras):
        for block in list(coll):
            coll.remove(block)


def export_fbx(path, objects):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
        for c in o.children_recursive:
            c.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP")


# ----------------------------------------------------------------------------- preview renders

def setup_preview(world_rgb=(0.55, 0.6, 0.7)):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = 24
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Punchy"
    world = bpy.data.worlds.new("preview")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (*world_rgb, 1.0)
    bg.inputs["Strength"].default_value = 0.85
    scene.world = world
    sun = bpy.data.objects.new("pv_sun", bpy.data.lights.new("pv_sun", "SUN"))
    sun.data.energy = 3.4
    sun.data.angle = math.radians(10)
    # Unity sun ~ Euler(40, -32): from front-left above
    sun.rotation_euler = (math.radians(45), math.radians(-15), math.radians(-30))
    scene.collection.objects.link(sun)
    fill = bpy.data.objects.new("pv_fill", bpy.data.lights.new("pv_fill", "SUN"))
    fill.data.energy = 0.9
    fill.rotation_euler = (math.radians(70), 0, math.radians(150))
    scene.collection.objects.link(fill)


def bounds(objects):
    bpy.context.view_layer.update()
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for o in objects:
        for obj in [o] + list(o.children_recursive):
            if obj.type != "MESH":
                continue
            for c in obj.bound_box:
                w = obj.matrix_world @ Vector(c)
                mins = Vector(map(min, mins, w))
                maxs = Vector(map(max, maxs, w))
    return mins, maxs


def render_views(path, objects, size=(640, 480), views=((8, 10),), lens=50, ground=True, transparent=False, margin=1.08):
    """Render from the game camera side. In Blender space the game camera looks along +y
    (Unity +z); azimuth rotates around z, elevation tilts up."""
    scene = bpy.context.scene
    mins, maxs = bounds(objects)
    center = (mins + maxs) / 2
    radius = (maxs - mins).length / 2
    temp = []
    if ground:
        bpy.ops.mesh.primitive_plane_add(size=radius * 10, location=(center.x, center.y, mins.z - 0.002))
        g = bpy.context.object
        gm = bpy.data.materials.get("pv_ground") or bpy.data.materials.new("pv_ground")
        gm.diffuse_color = (0.32, 0.3, 0.33, 1)
        g.data.materials.append(gm)
        temp.append(g)
    cam_data = bpy.data.cameras.new("pv_cam")
    cam_data.lens = lens
    cam = bpy.data.objects.new("pv_cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.film_transparent = transparent
    stem, ext = os.path.splitext(path)
    out = []
    for k, (el, az) in enumerate(views):
        e, a = math.radians(el), math.radians(az)
        # Unity camera at -z looking +z == Blender camera at +y looking -y
        d = Vector((math.sin(a) * math.cos(e), math.cos(a) * math.cos(e), math.sin(e)))
        fov = min(cam_data.angle_x, cam_data.angle_y) if size[0] != size[1] else cam_data.angle
        cam.location = center + d * (radius / math.tan(fov / 2) * margin)
        cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
        p = path if k == 0 else f"{stem}_{k}{ext}"
        scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
        out.append(p)
    bpy.data.objects.remove(cam)
    for t in temp:
        bpy.data.objects.remove(t)
    return out
