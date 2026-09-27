"""Creates a Blender design file with the kit and a small starting layout.

    blender --background --factory-startup --python blender/make_kit.py -- <output.blend> <blueprint name> [--force]

See hb_design.py for how the file works. Refuses to overwrite an existing file unless --force is given, so design
work is never lost.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hb_design  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if len(args) < 2:
    raise SystemExit("usage: make_kit.py -- <output.blend> <blueprint name> [--force]")
out_path, blueprint_name = os.path.abspath(args[0]), args[1]
if os.path.exists(out_path) and "--force" not in args:
    raise SystemExit(f"{out_path} already exists; not overwriting (pass --force to replace it)")

d = hb_design.Design(blueprint_name)

# A small corner using the first parts, on the game's wall grid (walls are centred 2.26 m apart along a line and meet
# at right angles 1.13 m from each other's centre).
M = hb_design.GRID
d.add("LogWall", 0.0, 0.0, 0.0, 0.0)
d.add("StoneWall", 0.0, -2 * M, 0.0, 0.0)
d.add("LogWindowWall", M, M, 0.0, 90.0)
d.add("Door", 2 * M + M / 2, M, 0.0, 90.0)
d.add("PlankPlatform", 1.5 * M, -1.5 * M, 0.0, 0.0)
d.save(out_path)
