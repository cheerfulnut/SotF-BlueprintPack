"""Shared setup for House Blueprint design files: the kit column, part objects, snapping and views.

Every part outside the kit column is exported, whichever collection it is in; parts in the column are ignored.
Parts can only move along the ground (their height is set by the script that places them) and turn around Z.
Holding Ctrl snaps to a 0.565 m grid (half the 1.13 m spacing the game's walls use) and turns in 90 degree steps.
"""
import math

import bpy
from mathutils import Quaternion

import hb_parts

GRID = 1.13


class Design:
    def __init__(self, blueprint_name):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.scene = bpy.context.scene
        self.scene.unit_settings.system = "METRIC"
        self.scene.unit_settings.length_unit = "METERS"
        self.scene["hb_blueprint_name"] = blueprint_name
        self.kit = bpy.data.collections.new("Kit")
        self.design = bpy.data.collections.new("Design")
        self.scene.collection.children.link(self.kit)
        self.scene.collection.children.link(self.design)
        self.meshes = {part: hb_parts.build_mesh(part) for part in hb_parts.PARTS}
        self._add_kit()

    def add(self, part, x, y, z=0.0, yaw_deg=0.0, name=None, collection=None):
        obj = bpy.data.objects.new(name or part, self.meshes[part])
        obj.location = (x, y, z)
        obj.rotation_euler = (0.0, 0.0, math.radians(yaw_deg))
        obj["hb_part"] = part
        obj.lock_rotation = (True, True, False)  # only rotation around Z means anything to the game
        obj.lock_location = (False, False, True)  # the height is part of the design; move parts sideways only
        (collection or self.design).objects.link(obj)
        return obj

    def _label(self, text, x, y):
        curve = bpy.data.curves.new("Label", type="FONT")
        curve.body = text
        curve.size = 0.35
        curve.align_x = "CENTER"
        obj = bpy.data.objects.new("Label " + text, curve)
        obj.location = (x, y, 0.01)
        self.kit.objects.link(obj)

    def _add_kit(self):
        # One of each part in a column left of the design, with a label in front of it.
        for i, part in enumerate(hb_parts.PARTS):
            y = -6.0 + i * 3.5
            z = 1.0 if part in ("Beam", "Floor", "HatchFloor") else (hb_parts.ROPE_LENGTH if part == "Rope" else 0.0)
            self.add(part, hb_parts.KIT_X, y, z, 90.0 if part not in ("PlankPlatform", "Floor", "HatchFloor") else 0.0,
                     name="Kit " + part, collection=self.kit)
            self._label(part, hb_parts.KIT_X, y - 1.6)

    def save(self, path, view_centre=(-2.0, 0.0, 0.0), view_distance=16.0):
        tool = self.scene.tool_settings
        tool.use_snap = False
        tool.snap_elements = {"INCREMENT"}
        if hasattr(tool, "use_snap_grid_absolute"):
            tool.use_snap_grid_absolute = True
        if hasattr(tool, "snap_angle_increment_3d"):
            tool.snap_angle_increment_3d = math.radians(90)

        # Every 3D view: grid lines every 0.565 m (halving when zoomed in), seen from straight above.
        for screen in bpy.data.screens:
            for area in screen.areas:
                if area.type != "VIEW_3D":
                    continue
                for space in area.spaces:
                    if space.type != "VIEW_3D":
                        continue
                    space.overlay.grid_scale = GRID / 2
                    space.overlay.grid_subdivisions = 2
                    r3d = space.region_3d
                    if r3d is not None:
                        r3d.view_perspective = "ORTHO"
                        r3d.view_rotation = Quaternion((1.0, 0.0, 0.0, 0.0))
                        r3d.view_location = view_centre
                        r3d.view_distance = view_distance

        bpy.ops.wm.save_as_mainfile(filepath=path)
        parts = sum(1 for o in self.design.objects if o.get("hb_part"))
        print(f"HB: wrote {path} with {parts} design parts")
