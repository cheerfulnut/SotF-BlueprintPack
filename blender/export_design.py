"""Exports a design file's "Design" collection as a blueprint layout, and renders the pictures for its book page.

    blender --background <design.blend> --python blender/export_design.py -- <recipe id> <output base path>

Writes <base>.json (the layout the mod builds), and <base>-render.png plus <base>-icon-<item>.png (pictures for
blender/make_page.py). Coordinates are converted from Blender (Z up, right-handed) to the game (Y up, left-handed):
game (x, y, z) = Blender (x, z, y), and a turn of A degrees around Blender's Z is -A degrees around the game's Y.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hb_parts  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if len(args) < 2:
    raise SystemExit("usage: export_design.py -- <recipe id> <output base path>")
recipe_id, base = int(args[0]), os.path.abspath(args[1])
os.makedirs(os.path.dirname(base), exist_ok=True)

scene = bpy.context.scene
design = bpy.data.collections.get("Design")
if design is None:
    raise SystemExit("No 'Design' collection in this file")

# --- Layout -----------------------------------------------------------------------------------------------------
parts, totals, problems = [], {}, []
for obj in sorted(design.all_objects, key=lambda o: o.name):
    part = obj.get("hb_part")
    if part is None:
        continue
    if part not in hb_parts.PARTS:
        problems.append(f"{obj.name}: unknown part type {part}")
        continue
    rx, ry, rz = obj.matrix_world.to_euler("XYZ")
    if abs(rx) > 1e-3 or abs(ry) > 1e-3:
        problems.append(f"{obj.name}: tilted; only rotation around Z is used")
    if any(abs(s - 1.0) > 1e-3 for s in obj.matrix_world.to_scale()):
        problems.append(f"{obj.name}: scaled; parts always have their real size in the game")
    x, y, z = obj.matrix_world.translation
    parts.append({
        "name": obj.name,
        "type": part,
        "position": [round(x, 3), round(z, 3), round(y, 3)],
        "yaw": round(-math.degrees(rz), 2) % 360.0,
    })
    for item, count in hb_parts.PARTS[part][1].items():
        totals[item] = totals.get(item, 0) + count

name = scene.get("hb_blueprint_name", os.path.basename(base))
layout = {
    "id": recipe_id,
    "name": name,
    "category": "Shelters",
    "page": os.path.basename(base) + "-page.png",
    "ingredients": totals,
    "parts": parts,
}
with open(base + ".json", "w", encoding="utf-8") as f:
    json.dump(layout, f, indent=2)
print(f"HB: {len(parts)} parts, ingredients {totals} -> {base}.json")
for p in problems:
    print("HB WARNING: " + p)

# --- Pictures ---------------------------------------------------------------------------------------------------
kit = bpy.data.collections.get("Kit")
if kit:
    kit.hide_render = True

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 64
scene.cycles.use_denoising = True
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"

world = scene.world or bpy.data.worlds.new("HB World")
scene.world = world
if hasattr(world, "use_nodes") and not world.use_nodes:
    world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
if bg:
    bg.inputs["Color"].default_value = (0.85, 0.80, 0.74, 1.0)
    bg.inputs["Strength"].default_value = 0.35

sun_data = bpy.data.lights.new("HB Sun", type="SUN")
sun_data.energy = 2.2
sun_data.angle = math.radians(12)
sun = bpy.data.objects.new("HB Sun", sun_data)
sun.rotation_euler = (math.radians(50), 0, math.radians(-40))
scene.collection.objects.link(sun)

cam_data = bpy.data.cameras.new("HB Camera")
cam = bpy.data.objects.new("HB Camera", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def aim(target, direction, distance):
    cam.location = target + direction.normalized() * distance
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()


# Main render: three-quarter view from the front left and above, with a soft shadow on an invisible ground.
corners = [o.matrix_world @ Vector(c) for o in design.all_objects if o.type == "MESH" for c in o.bound_box]
lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
centre, radius = (lo + hi) / 2, (hi - lo).length / 2

ground_mesh = bpy.data.meshes.new("HB Ground")
ground_mesh.from_pydata([(-50, -50, 0), (50, -50, 0), (50, 50, 0), (-50, 50, 0)], [], [(0, 1, 2, 3)])
ground = bpy.data.objects.new("HB Ground", ground_mesh)
ground.is_shadow_catcher = True
scene.collection.objects.link(ground)

cam_data.lens = 50
fov = 2 * math.atan(cam_data.sensor_width / (2 * cam_data.lens))
aim(centre, Vector((-1.0, -1.25, 0.9)), radius / math.sin(fov / 2) * 0.95)
scene.render.resolution_x = scene.render.resolution_y = 512
scene.render.filepath = base + "-render.png"
bpy.ops.render.render(write_still=True)
print(f"HB: rendered {scene.render.filepath}")

# Icons: each ingredient on its own, seen from the same side, no shadow.
ground.hide_render = True
design.hide_render = True
cam_data.type = "ORTHO"
scene.render.resolution_x = scene.render.resolution_y = 160
for item in totals:
    mesh = hb_parts.icon_mesh(item)
    icon = bpy.data.objects.new("HB Icon " + item, mesh)
    icon.rotation_euler = (0, 0, math.radians(45))  # lying diagonally across the picture
    scene.collection.objects.link(icon)
    size = max(icon.dimensions)
    cam_data.ortho_scale = size * 1.15
    aim(Vector((0, 0, 0)), Vector((0.0, -0.45, 1.0)), 10)  # mostly from above, like the book's icons
    scene.render.filepath = f"{base}-icon-{item}.png"
    bpy.ops.render.render(write_still=True)
    icon.hide_render = True
    print(f"HB: rendered {scene.render.filepath}")
