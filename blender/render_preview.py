"""Renders a preview of a design for checking it, optionally as a cutaway.

    blender --background <design.blend> --python blender/render_preview.py -- <output.png> [cutaway height]

With a cutaway height, parts above that height on the two sides nearest the camera are left out, so the rooms and
floors inside can be seen. Only for looking at: nothing here goes into the blueprint.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hb_parts  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = os.path.abspath(args[0])
cut = float(args[1]) if len(args) > 1 else None

scene = bpy.context.scene
parts = [o for o in scene.objects if o.get("hb_part") and o.matrix_world.translation.x >= hb_parts.KIT_LIMIT_X]
for obj in scene.objects:
    if obj.matrix_world.translation.x < hb_parts.KIT_LIMIT_X:
        obj.hide_render = True

corners = [o.matrix_world @ Vector(c) for o in parts for c in o.bound_box]
lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
centre, radius = (lo + hi) / 2, (hi - lo).length / 2

if cut is not None:
    # The camera looks from the -x, -y side; the near sides are found from the walls only, since platforms and posts
    # stick out further.
    walls = [o.matrix_world.translation for o in parts if "Wall" in o["hb_part"] or "Parapet" in o["hb_part"]]
    near_x = min(p.x for p in walls) + 0.6
    near_y = min(p.y for p in walls) + 0.6
    for o in parts:
        p = o.matrix_world.translation
        if p.z > cut and (p.x < near_x or p.y < near_y) and o["hb_part"] not in ("Floor", "HatchFloor"):
            o.hide_render = True

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.render.film_transparent = False
scene.view_settings.view_transform = "Standard"
scene.render.resolution_x, scene.render.resolution_y = 900, 900
world = scene.world or bpy.data.worlds.new("Preview World")
scene.world = world
bg = world.node_tree.nodes.get("Background")
if bg:
    bg.inputs["Color"].default_value = (0.75, 0.8, 0.85, 1.0)
    bg.inputs["Strength"].default_value = 0.5

sun = bpy.data.objects.new("Preview Sun", bpy.data.lights.new("Preview Sun", "SUN"))
sun.data.energy = 2.5
sun.rotation_euler = (math.radians(50), 0, math.radians(-40))
scene.collection.objects.link(sun)
ground = bpy.data.objects.new("Preview Ground", bpy.data.meshes.new("Preview Ground"))
ground.data.from_pydata([(-60, -60, 0), (60, -60, 0), (60, 60, 0), (-60, 60, 0)], [], [(0, 1, 2, 3)])
scene.collection.objects.link(ground)

cam = bpy.data.objects.new("Preview Camera", bpy.data.cameras.new("Preview Camera"))
scene.collection.objects.link(cam)
scene.camera = cam
fov = 2 * math.atan(cam.data.sensor_width / (2 * cam.data.lens))
direction = Vector((-1.0, -1.25, 0.9)).normalized()
cam.location = centre + direction * (radius / math.sin(fov / 2) * 1.0)
cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()

scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print(f"HB: preview {out}")
