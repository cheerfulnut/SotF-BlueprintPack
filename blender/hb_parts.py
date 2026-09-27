"""Kit parts for designing House Blueprint layouts in Blender.

Each part is a stand-in for a group of the game's own building pieces, at the game's real sizes. Blender is Z-up
and metres; a part's origin is where it touches the ground, and walls run along the part's local Y axis. The mod
builds the real game pieces from the part's type, position and rotation around Z (other rotations are ignored).

Sizes come from the game's building-piece data (diagnostics, 2026-09-27): logs are 0.45 m thick and 2.53 m long and
walls stack them every 0.45 m; the door is 1.1 m wide and 2.2 m tall; stone wall pieces are 0.44 x 0.62 x 0.45 m.
"""
import math
import random

import bmesh
import bpy

# The kit column sits at x = KIT_X; any part left of KIT_LIMIT_X is part of the kit (or a copy left on top of it)
# and is not exported. Every other part is, whatever collection it is in.
KIT_X = -8.0
KIT_LIMIT_X = -5.0

LOG_R = 0.225
LOG_LEN = 2.53
ROW = 0.45
WALL_ROWS = 5

PARTS = {
    # type: (description, ingredients as {item: count}) - the mod counts the real ingredients; these are for the page.
    "LogWall": ("Log wall, 5 logs high", {"Log": 5}),
    "LogWindowWall": ("Log wall with a window, 5 logs high", {"QuarterLog": 5}),
    "StoneWall": ("Stone wall, 5 rows of 4 stones", {"Stone": 20}),
    "Door": ("Plank door, 1.1 m wide", {"Plank": 1}),
    "PlankPlatform": ("Plank floor on 4 log beams, about 2.3 m square", {"Log": 4, "Plank": 5}),
}

COLOURS = {
    "Log": (0.13, 0.065, 0.028, 1.0),
    "LogEnd": (0.42, 0.28, 0.14, 1.0),
    "Plank": (0.22, 0.13, 0.06, 1.0),
    "Stone": (0.22, 0.215, 0.205, 1.0),
}


def material(name):
    """A plain, slightly rough material; created once per file."""
    mat = bpy.data.materials.get("HB_" + name)
    if mat:
        return mat
    mat = bpy.data.materials.new("HB_" + name)
    if hasattr(mat, "use_nodes") and not mat.use_nodes:
        mat.use_nodes = True
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf:
        bsdf.inputs["Base Color"].default_value = COLOURS[name]
        bsdf.inputs["Roughness"].default_value = 0.85
    mat.diffuse_color = COLOURS[name]
    return mat


def _cylinder_y(bm, x, y, z, length, radius, segments=16):
    """A log lying along Y, centred at (x, y, z)."""
    geom = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=length)
    verts = geom["verts"]
    bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0), matrix=_rot_x(math.pi / 2))
    bmesh.ops.translate(bm, verts=verts, vec=(x, y, z))


def _box(bm, x, y, z, sx, sy, sz):
    geom = bmesh.ops.create_cube(bm, size=1.0)
    verts = geom["verts"]
    bmesh.ops.scale(bm, verts=verts, vec=(sx, sy, sz))
    bmesh.ops.translate(bm, verts=verts, vec=(x, y, z))
    return verts


def _rot_x(a):
    from mathutils import Matrix
    return Matrix.Rotation(a, 3, "X")


def _rot_z(a):
    from mathutils import Matrix
    return Matrix.Rotation(a, 3, "Z")


def _finish(name, bm, mat_name):
    mesh = bpy.data.meshes.new("HB_" + name)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.materials.append(material(mat_name))
    return mesh


def build_mesh(part):
    """Returns a new mesh for a part type."""
    bm = bmesh.new()
    if part == "LogWall":
        for i in range(WALL_ROWS):
            _cylinder_y(bm, 0, 0, LOG_R + ROW * i, LOG_LEN, LOG_R)
        return _finish(part, bm, "Log")

    if part == "LogWindowWall":
        gap = 0.9
        seg = (LOG_LEN - gap) / 2
        for i in range(WALL_ROWS):
            z = LOG_R + ROW * i
            if 1 <= i <= 3:
                for side in (-1, 1):
                    _cylinder_y(bm, 0, side * (gap / 2 + seg / 2), z, seg, LOG_R)
            else:
                _cylinder_y(bm, 0, 0, z, LOG_LEN, LOG_R)
        return _finish(part, bm, "Log")

    if part == "StoneWall":
        rng = random.Random(7)
        for i in range(WALL_ROWS):
            for j in range(4):
                verts = _box(bm, 0, -0.93 + 0.62 * j, LOG_R + ROW * i,
                             0.44 * rng.uniform(0.85, 1.0), 0.60 * rng.uniform(0.9, 1.0), 0.43 * rng.uniform(0.85, 1.0))
                bmesh.ops.rotate(bm, verts=verts, cent=(0, -0.93 + 0.62 * j, LOG_R + ROW * i),
                                 matrix=_rot_z(rng.uniform(-0.08, 0.08)))
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=0.05, segments=2, affect="EDGES")
        return _finish(part, bm, "Stone")

    if part == "Door":
        for k in range(3):  # three planks side by side
            _box(bm, 0, -0.367 + 0.367 * k, 1.1, 0.1, 0.35, 2.2)
        return _finish(part, bm, "Plank")

    if part == "PlankPlatform":
        half = 1.13
        for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):  # stumps under the corners
            geom = bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.2, radius2=0.2, depth=0.2)
            bmesh.ops.translate(bm, verts=geom["verts"], vec=(sx * half, sy * half, 0.1))
        for s in (1, -1):  # beams along both edges, both directions
            _cylinder_y(bm, s * half, 0, 0.2 + LOG_R, 2 * half + 0.3, LOG_R * 0.9)
        for k in range(5):  # planks across the beams
            _box(bm, -0.905 + 0.4525 * k, 0, 0.2 + 2 * LOG_R + 0.06, 0.428, 2 * half + 0.1, 0.12)
        return _finish(part, bm, "Plank")

    raise ValueError(f"Unknown part type {part}")


def icon_mesh(item):
    """Small stand-in meshes for the ingredient icons on the book page."""
    bm = bmesh.new()
    if item in ("Log", "QuarterLog"):
        _cylinder_y(bm, 0, 0, 0, LOG_LEN if item == "Log" else LOG_LEN / 4, LOG_R)
        return _finish("Icon" + item, bm, "Log")
    if item == "Plank":
        _box(bm, 0, 0, 0, 0.428, 2.3, 0.12)
        return _finish("Icon" + item, bm, "Plank")
    if item == "Stone":
        verts = _box(bm, 0, 0, 0, 0.44, 0.6, 0.4)
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=0.08, segments=3, affect="EDGES")
        return _finish("Icon" + item, bm, "Stone")
    raise ValueError(f"Unknown item {item}")
