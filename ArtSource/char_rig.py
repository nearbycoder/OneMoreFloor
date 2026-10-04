"""Armatures, skin weights and animation clips for the characters (used by build_characters.py).

Each character is built as separate pivoted parts (Body, Head, LegL, ArmR, Cape, ...). `rig()` turns that
into one skinned mesh on an armature:

    Root                      floor pivot
      Hips                    pelvis: bob and sway in the walk cycle
        Spine                 the Body part (torso, fused arms and anything they carry)
          Head                the Head part
          ArmL, ArmL_Lo       upper arm / forearm + hand + held prop (elbow blended)
          ArmR, ArmR_Lo
          Cape, Balloons, Mirror, Leaves -> Leaf0..N
          (Propeller hangs off Head)
        LegL, LegL_Lo         thigh / shin + shoe (knee blended)
        LegR, LegR_Lo

Clips are posed in Unity space (x = the character's +x side, y up, z = facing) as rotations about each
bone's rest head, so they read the same as the game code that used to swing the parts. They are written
as Blender actions and exported as FBX takes: Idle, Walk, Stomp, Tap, Cheer, Tuck, Fume (+ Salute and
Worry for the bellhop). The game blends them with weights from what the guest is doing.
"""
import math

import bpy
from mathutils import Euler, Matrix, Vector

from omf_lib import U2B

B2U = U2B.inverted()
U2B3 = U2B.to_3x3()
B2U3 = B2U.to_3x3()
FPS = 30


def _u(obj):
    """Unity-space position of an object's origin."""
    return (B2U @ obj.matrix_world.to_translation().to_4d()).to_3d()


def _seg_param(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return t, (a + ab * t - p).length


def _smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


# ----------------------------------------------------------------------------- building the rig

def rig(root, joints, held, name):
    """Join root's mesh parts into one skinned mesh under a new armature. Returns (armature, info)."""
    bpy.context.view_layer.update()
    parts = {o.name: o for o in root.children_recursive if o.type == "MESH"}
    empties = [o for o in root.children_recursive if o.type == "EMPTY"]
    up = Vector((0, 1, 0))

    hips_y = joints["LegL"][0][1] if "LegL" in joints else (_u(parts["LegL"]).y if "LegL" in parts else 0.6)
    head_o = parts.get("Head") or next((e for e in empties if e.name == "Head"), None)
    neck_y = _u(head_o).y if head_o else hips_y + 0.7
    if "LegL" in parts and "LegL" not in joints:   # stubby single-piece legs (the houseplant)
        hips_y = _u(parts["LegL"]).y

    # bone name -> (head, tail, parent) in Unity space
    bones = {
        "Root": (Vector((0, 0, 0)), Vector((0, 0.2, 0)), None),
        "Hips": (Vector((0, hips_y, 0)), Vector((0, hips_y + 0.12, 0)), "Root"),
        "Spine": (Vector((0, hips_y + 0.02, 0)), Vector((0, neck_y, 0)), "Hips"),
        "Head": (Vector((0, neck_y, 0)), Vector((0, neck_y + 0.45, 0)), "Spine"),
    }
    for side in "LR":
        for limb, parent in (("Leg", "Hips"), ("Arm", "Spine")):
            n = limb + side
            if n not in parts:
                continue
            if n in joints:
                a, b, c = (Vector(p) for p in joints[n])
                bones[n] = (a, b, parent)
                bones[n + "_Lo"] = (b, c, n)
            else:
                o = _u(parts[n])
                bones[n] = (o, Vector((o.x, 0.0, o.z)) if o.y > 0.05 else o + Vector((0, -0.1, 0)), parent)
    extra_parent = {"Cape": "Spine", "Mirror": "Spine", "Balloons": "Spine", "Propeller": "Head"}
    for n, parent in extra_parent.items():
        if n in parts:
            o = _u(parts[n])
            bones[n] = (o, o + up * 0.3, parent)
    leaves = next((e for e in empties if e.name == "Leaves"), None)
    leaf_dirs = {}
    if leaves:
        base = _u(leaves)
        bones["Leaves"] = (base, base + up * 0.2, "Spine")
        for n, o in parts.items():
            if n.startswith("Leaf"):
                # leaf direction = from the base toward the centre of its geometry
                ws = [B2U @ (o.matrix_world @ v.co).to_4d() for v in o.data.vertices]
                c = sum((w.to_3d() for w in ws), Vector()) / max(1, len(ws))
                d = (c - base)
                if d.length < 1e-4:
                    d = up.copy()
                bones[n] = (base.copy(), base + d.normalized() * max(0.2, d.length), "Leaves")
                leaf_dirs[n] = d.normalized()

    # vertex groups (weights computed in Unity space)
    for n, o in parts.items():
        o.vertex_groups.clear()
        mw = o.matrix_world
        if n in joints and n in bones and n + "_Lo" in bones:
            a, b, c = (Vector(p) for p in joints[n])
            l1 = (b - a).length
            g_up, g_lo = o.vertex_groups.new(name=n), o.vertex_groups.new(name=n + "_Lo")
            blend = 0.06
            for v in o.data.vertices:
                p = (B2U @ (mw @ v.co).to_4d()).to_3d()
                t1, d1 = _seg_param(p, a, b)
                t2, d2 = _seg_param(p, b, c)
                along = (t1 - 1.0) * l1 if d1 < d2 else t2 * (c - b).length
                w = _smooth(-blend, blend, along)
                if w < 0.999:
                    g_up.add([v.index], 1.0 - w, "REPLACE")
                if w > 0.001:
                    g_lo.add([v.index], w, "REPLACE")
            continue
        bone = "Spine" if n == "Body" else n
        if bone not in bones:
            bone = "Spine"
        g = o.vertex_groups.new(name=bone)
        g.add([v.index for v in o.data.vertices], 1.0, "REPLACE")

    # armature
    arm_data = bpy.data.armatures.new(name + "_Rig")
    arm = bpy.data.objects.new("Rig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    arm.parent = root
    bpy.context.view_layer.objects.active = arm
    for o in bpy.context.view_layer.objects:
        o.select_set(o == arm)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    for n, (h, t, parent) in bones.items():
        b = arm_data.edit_bones.new(n)
        b.head = (U2B @ h.to_4d()).to_3d()
        b.tail = (U2B @ t.to_4d()).to_3d()
        b.roll = 0.0
        eb[n] = b
    for n, (h, t, parent) in bones.items():
        if parent:
            eb[n].parent = eb[parent]
            eb[n].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")

    # join the parts into one mesh, parented to the armature
    meshes = list(parts.values())
    for o in bpy.context.view_layer.objects:
        o.select_set(o in meshes)
    body = parts.get("Body") or meshes[0]
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body.name = "Mesh"
    body.data.name = name + "_Mesh"
    mw = body.matrix_world.copy()
    body.parent = arm
    body.matrix_world = mw
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    for e in empties:
        bpy.data.objects.remove(e)
    return arm, {"bones": bones, "held": held, "leaf_dirs": leaf_dirs}


# ----------------------------------------------------------------------------- posing

def _rot_u(e):
    """Unity Quaternion.Euler(x, y, z) as a 3x3 matrix (Z, then X, then Y)."""
    x, y, z = (math.radians(a) for a in e)
    return Matrix.Rotation(y, 3, "Y") @ Matrix.Rotation(x, 3, "X") @ Matrix.Rotation(z, 3, "Z")


def _apply_pose(arm, pose):
    """pose: bone -> dict(r=(x,y,z) Unity Euler degrees about the bone head, t=(x,y,z) Unity offset)."""
    for pb in arm.pose.bones:
        spec = pose.get(pb.name)
        rest = pb.bone.matrix_local
        if not spec:
            basis = Matrix.Identity(4)
        else:
            rb = (U2B3 @ _rot_u(spec.get("r", (0, 0, 0))) @ B2U3).to_4x4()
            h = pb.bone.head_local
            d = Matrix.Translation(h) @ rb @ Matrix.Translation(-h)
            t = spec.get("t")
            if t:
                d = Matrix.Translation(U2B3 @ Vector(t)) @ d
            basis = rest.inverted() @ d @ rest
        pb.rotation_mode = "QUATERNION"
        loc, rot, _ = basis.decompose()
        pb.location = loc
        pb.rotation_quaternion = rot
        pb.scale = (1, 1, 1)


def _clip(arm, name, length, pose_fn):
    bpy.context.scene.render.fps = FPS
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    frames = max(2, int(round(length * FPS)))
    for f in range(frames + 1):
        _apply_pose(arm, pose_fn(f / frames))
        for pb in arm.pose.bones:
            pb.keyframe_insert("location", frame=f + 1, group=pb.name)
            pb.keyframe_insert("rotation_quaternion", frame=f + 1, group=pb.name)
    return act


def _limbs(info):
    b = info["bones"]
    return {k: k in b for k in ("ArmL", "ArmR", "LegL", "LegR", "ArmL_Lo", "ArmR_Lo", "LegL_Lo", "LegR_Lo")}


def clips(arm, info, kind):
    """Author every clip for this character. Arms that hold a prop keep it low in showy poses."""
    held = info["held"]
    has = _limbs(info)
    plant = kind == "Houseplant"
    TAU = 2 * math.pi

    def arm_pair(p, l, r):
        """Write both arms: l/r = dict(r=(x,y,z), lo=x elbow bend). Right side mirrors y/z."""
        for side, spec in (("L", l), ("R", r)):
            n = "Arm" + side
            if not has[n] or spec is None:
                continue
            x, y, z = spec.get("r", (0, 0, 0))
            p[n] = {"r": (x, y if side == "L" else -y, z if side == "L" else -z)}
            if has[n + "_Lo"]:
                p[n + "_Lo"] = {"r": (spec.get("lo", 0.0), 0, spec.get("loz", 0.0) * (1 if side == "L" else -1))}

    def legs(p, lx, lk, rx, rk):
        for n, x, k in (("LegL", lx, lk), ("LegR", rx, rk)):
            if has[n]:
                p[n] = {"r": (x, 0, 0)}
            if has[n + "_Lo"]:
                p[n + "_Lo"] = {"r": (k, 0, 0)}

    def idle(t):
        a = TAU * t
        p = {"Hips": {"t": (0, 0.006 * math.sin(2 * a), 0)},
             "Spine": {"r": (1.2 * math.sin(a), 0, 0.8 * math.sin(a + 1))},
             "Head": {"r": (-1.5 * math.sin(a + 0.7), 4 * math.sin(a), 1.5 * math.sin(a + 2))}}
        sway = 2 * math.sin(a)
        arm_pair(p, {"r": (2 + sway, 0, 5 + sway * 0.5), "lo": -10}, {"r": (2 - sway, 0, 5 - sway * 0.5), "lo": -10})
        return p

    def walk(t, big=False):
        a = TAU * t
        s, c = math.sin(a), math.cos(a)
        amp = 36 if big else 30
        lift = 70 if big else 50
        p = {"Hips": {"t": (0, (0.05 if big else 0.035) * c * c - 0.02, 0), "r": (0, 6 * s, 0)},
             "Spine": {"r": ((14 if big else 5) + 2 * c * c, -9 * s, 2 * s)},
             "Head": {"r": ((8 if big else -3), 6 * s, 0)}}
        legs(p, -amp * s, 8 + lift * max(0.0, c) ** 1.5, amp * s, 8 + lift * max(0.0, -c) ** 1.5)
        if big:   # storming off: fists pumping
            arm_pair(p, {"r": (-40 + 30 * s, 0, 14), "lo": -95}, {"r": (-40 - 30 * s, 0, 14), "lo": -95})
        else:
            arm_pair(p, {"r": (26 * s, 0, 6), "lo": -(14 + 24 * max(0.0, -s))},
                     {"r": (-26 * s, 0, 6), "lo": -(14 + 24 * max(0.0, s))})
        return p

    def tap(t):
        a = TAU * t
        foot = max(0.0, math.sin(3 * a)) ** 2   # three quick taps per loop
        p = {"Hips": {"t": (0, 0.004 * foot, 0), "r": (0, 0, 2 * math.sin(a))},
             "Spine": {"r": (-2, 0, -3 + 2 * math.sin(a))},
             "Head": {"r": (-4, 12 * math.sin(a), 6 * math.sin(a + 0.5))}}
        legs(p, 0, 0, -10 * foot, 22 * foot)
        hip_hand = {"r": (6, 0, 40), "lo": 0, "loz": -100}
        down = {"r": (4 + 3 * math.sin(a), 0, 4), "lo": -8}
        arm_pair(p, down if "ArmL" in held else hip_hand, down if "ArmR" in held else hip_hand)
        if plant:
            p["Spine"] = {"r": (0, 0, 4 * math.sin(a))}
        return p

    def cheer(t):
        a = TAU * t
        air = max(0.0, math.sin(a))
        p = {"Hips": {"t": (0, 0.14 * air, 0)},
             "Spine": {"r": (-7, 0, 3 * math.sin(2 * a))},
             "Head": {"r": (-14, 0, 6 * math.sin(2 * a))}}
        legs(p, -14 * air, 30 * air, -10 * air, 26 * air)
        up = {"r": (-10, 0, 150 + 10 * math.sin(2 * a)), "lo": -18}
        arm_pair(p, up, dict(up, r=(-10, 0, 150 - 10 * math.sin(2 * a))))
        return p

    def tuck(t):
        p = {"Spine": {"r": (8, 0, 0)}, "Head": {"r": (-6, 0, 0)}}
        legs(p, -45, 85, -40, 80)
        arm_pair(p, {"r": (-10, 0, 42), "lo": -45}, {"r": (-10, 0, 42), "lo": -45})
        return p

    def fume(t):
        a = TAU * t
        s = math.sin(a)
        p = {"Hips": {"r": (0, 0, 0)},
             "Spine": {"r": (7 + 1.5 * math.sin(2 * a), 0, 0)},
             "Head": {"r": (9, 0, 3 * math.sin(2 * a))}}
        stomp = max(0.0, math.sin(a)) ** 3
        legs(p, 0, 0, -16 * stomp, 18 * stomp)
        fist = lambda ph: {"r": (-55 + 12 * ph, 0, 18), "lo": -95}
        shake = lambda ph: {"r": (-28 + 10 * ph, 0, 10), "lo": -35}
        arm_pair(p, (shake if "ArmL" in held else fist)(s), (shake if "ArmR" in held else fist)(-s))
        return p

    made = [
        _clip(arm, "Idle", 2.4, idle),
        _clip(arm, "Walk", 0.5, walk),
        _clip(arm, "Stomp", 0.36, lambda t: walk(t, True)),
        _clip(arm, "Tap", 1.5, tap),
        _clip(arm, "Cheer", 0.6, cheer),
        _clip(arm, "Tuck", 0.2, tuck),
        _clip(arm, "Fume", 0.3, fume),
    ]
    if kind == "Bellhop":
        def salute(t):
            p = idle(0.0)
            p["Spine"] = {"r": (-3, 0, 0)}
            p["Head"] = {"r": (-6, 0, 0)}
            arm_pair(p, {"r": (3, 0, 4), "lo": -6}, {"r": (-128, 0, 34), "lo": -78})
            return p

        def worry(t):
            a = TAU * t
            p = idle(t)
            p["Head"] = {"r": (4, 14 * math.sin(a), 0)}
            arm_pair(p, {"r": (-30 + 16 * math.sin(2 * a), 0, 8), "lo": -60 + 10 * math.sin(4 * a)}, {"r": (2, 0, 5), "lo": -10})
            return p
        made += [_clip(arm, "Salute", 0.2, salute), _clip(arm, "Worry", 1.2, worry)]
    arm.animation_data.action = None
    _apply_pose(arm, {})
    return made
