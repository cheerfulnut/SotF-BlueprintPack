"""Creates a Blender design file with the kit and a starting layout.

    blender --background --factory-startup --python blender/make_kit.py -- <output.blend> <blueprint name> [--force]

The file has two collections:
  * "Kit": one of each part, off to the side. Copy parts from here with Alt+D (a linked copy).
  * "Design": the layout that gets exported. Move and rotate parts here (rotate only around Z).
Parts can only move along the ground. Holding Ctrl snaps to a 0.565 m grid (half the 1.13 m spacing the game's walls
use; walls meet every 2.26 m) and turns in 90 degree steps. The file opens looking straight down.
Refuses to overwrite an existing file unless --force is given, so design work is never lost.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hb_parts  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if len(args) < 2:
    raise SystemExit("usage: make_kit.py -- <output.blend> <blueprint name> [--force]")
out_path, blueprint_name = os.path.abspath(args[0]), args[1]
if os.path.exists(out_path) and "--force" not in args:
    raise SystemExit(f"{out_path} already exists; not overwriting (pass --force to replace it)")

# Start from an empty scene.
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.length_unit = "METERS"
scene["hb_blueprint_name"] = blueprint_name

kit = bpy.data.collections.new("Kit")
design = bpy.data.collections.new("Design")
scene.collection.children.link(kit)
scene.collection.children.link(design)

meshes = {part: hb_parts.build_mesh(part) for part in hb_parts.PARTS}


def add_part(collection, part, x, y, yaw_deg=0.0, name=None):
    obj = bpy.data.objects.new(name or part, meshes[part])
    obj.location = (x, y, 0.0)
    obj.rotation_euler = (0.0, 0.0, math.radians(yaw_deg))
    obj["hb_part"] = part
    obj.lock_rotation = (True, True, False)  # only rotation around Z means anything to the game
    obj.lock_location = (False, False, True)  # parts stand on the ground
    collection.objects.link(obj)
    return obj


def add_label(collection, text, x, y):
    curve = bpy.data.curves.new("Label", type="FONT")
    curve.body = text
    curve.size = 0.35
    curve.align_x = "CENTER"
    obj = bpy.data.objects.new("Label " + text, curve)
    obj.location = (x, y, 0.01)
    collection.objects.link(obj)


# Kit: one of each part in a row to the left of the design area, with a label in front of it.
for i, part in enumerate(hb_parts.PARTS):
    x = -8.0
    y = -6.0 + i * 3.5
    add_part(kit, part, x, y, 90.0 if part != "PlankPlatform" else 0.0, name="Kit " + part)
    add_label(kit, part, x, y - 1.6)

# Design: a small corner using every part, on the game's wall grid (walls are centred 2.26 m apart along a line and
# meet at right angles 1.13 m from each other's centre).
M = 1.13
add_part(design, "LogWall", 0.0, 0.0, 0.0)
add_part(design, "StoneWall", 0.0, -2 * M, 0.0)
add_part(design, "LogWindowWall", M, M, 90.0)
add_part(design, "Door", 2 * M + M / 2, M, 90.0)
add_part(design, "PlankPlatform", 1.5 * M, -1.5 * M, 0.0)

# Snapping: Ctrl snaps moves to the absolute grid and turns to 90 degree steps.
tool = scene.tool_settings
tool.use_snap = False
tool.snap_elements = {"INCREMENT"}
if hasattr(tool, "use_snap_grid_absolute"):
    tool.use_snap_grid_absolute = True
if hasattr(tool, "snap_angle_increment_3d"):
    tool.snap_angle_increment_3d = math.radians(90)

# Every 3D view: grid lines every 0.565 m (halving when zoomed in), seen from straight above.
from mathutils import Quaternion  # noqa: E402

for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type != "VIEW_3D":
            continue
        for space in area.spaces:
            if space.type != "VIEW_3D":
                continue
            space.overlay.grid_scale = M / 2
            space.overlay.grid_subdivisions = 2
            r3d = space.region_3d
            if r3d is not None:
                r3d.view_perspective = "ORTHO"
                r3d.view_rotation = Quaternion((1.0, 0.0, 0.0, 0.0))
                r3d.view_location = (-2.0, 0.0, 0.0)
                r3d.view_distance = 16.0

bpy.ops.wm.save_as_mainfile(filepath=out_path)
print(f"HB: wrote {out_path} with {len(kit.objects)} kit objects and {len(design.objects)} design parts")
