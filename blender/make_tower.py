"""Creates the design file for a square tower.

    blender --background --factory-startup --python blender/make_tower.py -- <output.blend> [--force]

The tower (agreed with the user, 2026-09-27): 3 x 3 wall sections (about 6.8 m square); a stone ground storey with no
door; two plain log storeys; an enclosed log room on the fourth storey; an open deck on top with a chest-high parapet.
A climbing rope hangs outside from a gap in the parapet down to the ground, and a second rope drops from a hatch in
the deck into the room. Storeys 1-3 are the tower's body and have no floors. Heights follow the game's LookOut Tower:
a ring of beams every 2.7 m with the walls standing on the beams.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hb_design  # noqa: E402
import hb_parts  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if not args:
    raise SystemExit("usage: make_tower.py -- <output.blend> [--force]")
out_path = os.path.abspath(args[0])
if os.path.exists(out_path) and "--force" not in args:
    raise SystemExit(f"{out_path} already exists; not overwriting (pass --force to replace it)")

SECTIONS = 3                                     # wall sections per side
STOREYS = ["StoneWall", "LogWall", "LogWall", "LogWall"]  # bottom to top; the last one is the room
ROOM_STOREY = 3                                  # index in STOREYS that gets a floor (the room)
WALL_HEIGHT = 5 * hb_parts.ROW                   # five logs (or rows of stone)

M = hb_design.GRID
half = SECTIONS * M                              # side lines at +-half
centres = [-half + M + 2 * M * i for i in range(SECTIONS)]
interior_lines = [-half + 2 * M * j for j in range(1, SECTIONS)]

d = hb_design.Design("Stone Tower")


def walls(part, z, skip=None):
    for c in centres:
        for sign in (-1, 1):
            if skip != ("x", sign, c):
                d.add(part, c, sign * half, z, 90.0)   # sides running along X
            if skip != ("y", sign, c):
                d.add(part, sign * half, c, z, 0.0)    # sides running along Y


def beam_ring(z, with_floor_beams):
    for c in centres:
        for sign in (-1, 1):
            d.add("Beam", c, sign * half, z, 90.0)
            d.add("Beam", sign * half, c, z, 0.0)
    if with_floor_beams:  # floor planks run along Y, so they rest on beams running along X
        for y in interior_lines:
            for c in centres:
                d.add("Beam", c, y, z, 90.0)


def floor(z, hatch_at=None):
    for cx in centres:
        for cy in centres:
            d.add("HatchFloor" if (cx, cy) == hatch_at else "Floor", cx, cy, z + hb_parts.BEAM_TO_FLOOR, 0.0)


contact = 0.0  # where the current storey's walls stand
for i, part in enumerate(STOREYS):
    walls(part, contact)
    beam = contact + WALL_HEIGHT + hb_parts.LOG_R   # the next ring sits on top of these walls
    is_room_floor_below = i + 1 == ROOM_STOREY
    is_deck = i == len(STOREYS) - 1
    beam_ring(beam, with_floor_beams=is_room_floor_below or is_deck)
    if is_room_floor_below:
        floor(beam)
    if is_deck:
        floor(beam, hatch_at=(centres[0], centres[0]))
        deck = beam
    contact = beam + hb_parts.BEAM_TO_WALL

# The parapet, with a gap in the middle of the front where the outside rope hangs.
walls("Parapet", contact, skip=("x", -1, centres[SECTIONS // 2]))
d.add("Rope", centres[SECTIONS // 2], -half - 0.35, deck, 0.0, name="Entrance rope")

d.save(out_path, view_centre=(-1.5, 0.0, 0.0), view_distance=22.0)
print(f"HB: deck at {deck + hb_parts.BEAM_TO_FLOOR:.2f} m, parapet top at {contact + 3 * hb_parts.ROW:.2f} m")
