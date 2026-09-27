"""Creates a Blender design file with the kit and a starting layout.

    blender --background --factory-startup --python blender/make_kit.py -- <output.blend> <blueprint name> [--force]

The file has two collections:
  * "Kit": one of each part, off to the side. Copy parts from here with Alt+D (a linked copy).
  * "Design": the layout that gets exported. Move and rotate parts here (rotate only around Z).
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

# Design: a small corner using every part.
add_part(design, "LogWall", 0.0, 1.265, 0.0)
add_part(design, "LogWindowWall", 1.265, 2.53, 90.0)
add_part(design, "StoneWall", 0.0, -1.265, 0.0)
add_part(design, "Door", 3.08, 2.53, 90.0)
add_part(design, "PlankPlatform", 1.6, 0.6, 0.0)

# A ground grid reference: 1 m squares, like Blender's own grid; snapping moves in whole grid steps.
tool = scene.tool_settings
tool.use_snap = False
if hasattr(tool, "snap_elements"):
    tool.snap_elements = {"INCREMENT"}

bpy.ops.wm.save_as_mainfile(filepath=out_path)
print(f"HB: wrote {out_path} with {len(kit.objects)} kit objects and {len(design.objects)} design parts")
