"""Creates the design file for a square tower.

    blender --background --factory-startup --python blender/make_tower.py -- <output.blend> [--force]

The tower (agreed with the user, 2026-09-27): 3 x 3 wall sections (about 6.8 m square); a stone ground storey with no
door; two plain log storeys; an enclosed log room on the fourth storey with a window in the middle of each side; an
open deck on top with a crenellated parapet (3 logs at the corners, 2 in the middle of each side) and a stone patch
for the campfire; upright log posts at the four corners from the room storey up (not lower, so they don't look climbable). At the front, a launch platform (for hang gliding) sticks
out through a gap in the parapet, with a one-log rail on each side; the entrance rope hangs from its front edge down
to the ground. A ramp along the left side of the room rises from front to back to the deck, through an opening with
rails on its front and inner edges; you walk onto it from the room floor at its front end and step sideways onto the
deck at the top. A rope drops from a hatch in the room's floor to the ground inside the tower (for going down inside to
work on defences). The left wall's window sits in its front section so the ramp does not cross it. Storeys 1-3 are the tower's body and have no floors. Heights follow the game's LookOut Tower:
a ring of beams every 2.7 m with the walls standing on the beams.
"""
import math
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
ROOM_WINDOWS = True                              # a window wall in the middle of each side of the room
CRENELLATED = True                               # parapet alternates 3 and 2 logs high
CORNER_POSTS = True                              # upright log posts just outside each corner, above the parapet
POSTS_FROM_STOREY = 3                            # posts start at this storey (0 = ground); lower ones look climbable
POST_OFFSET = 0.45                               # posts touch both walls' outer faces at each corner
LEVEL_NAMES = ["Level 1 - Stone", "Level 2 - Logs", "Level 3 - Logs", "Level 4 - Room"]
WALL_HEIGHT = 5 * hb_parts.ROW                   # five logs (or rows of stone)

M = hb_design.GRID
half = SECTIONS * M                              # side lines at +-half
centres = [-half + M + 2 * M * i for i in range(SECTIONS)]
interior_lines = [-half + 2 * M * j for j in range(1, SECTIONS)]

d = hb_design.Design("Stone Tower")
middle = centres[SECTIONS // 2]

# The ramp runs along the left column of tiles, middle and back, rising towards +Y (the back); the deck is open above
# it. Its front end starts just past the deck beam on the tile edge, so nobody on the ramp is under a beam (the beam's
# underside is only 2.2 m above the room floor).
RAMP_OPENING = [(centres[0], middle), (centres[0], centres[-1])]


def walls(part, z, group, skip=None, middle_part=None, special_at=None):
    """A storey of walls. middle_part replaces the middle section of each side, or the section given for that side in
    special_at: {("x", -1): front (-Y), ("x", 1): back, ("y", -1): left (-X), ("y", 1): right}."""
    special_at = special_at or {}
    for c in centres:
        for sign in (-1, 1):
            use = middle_part if middle_part and c == special_at.get(("x", sign), middle) else part
            if skip != ("x", sign, c):
                d.add(use, c, sign * half, z, 90.0, collection=group)   # sides running along X
            use = middle_part if middle_part and c == special_at.get(("y", sign), middle) else part
            if skip != ("y", sign, c):
                d.add(use, sign * half, c, z, 0.0, collection=group)    # sides running along Y


def beam_ring(z, group, with_floor_beams):
    for c in centres:
        for sign in (-1, 1):
            d.add("Beam", c, sign * half, z, 90.0, collection=group)
            d.add("Beam", sign * half, c, z, 0.0, collection=group)
    if with_floor_beams:  # floor planks run along Y, so they rest on beams running along X
        for y in interior_lines:
            for c in centres:
                d.add("Beam", c, y, z, 90.0, collection=group)


def floor(z, group, hatch_at=None, stone_at=None, openings=()):
    for cx in centres:
        for cy in centres:
            if (cx, cy) in openings:
                continue
            part = "HatchFloor" if (cx, cy) == hatch_at else "StoneFloor" if (cx, cy) == stone_at else "Floor"
            # A hatch's hole and rope are on its local +X side, and you step off the rope that way at the top: turn
            # it so that side faces the middle of the tower, not the outer wall (there is no floor to step onto there).
            yaw = 180.0 if part == "HatchFloor" and cx > 0 else 0.0
            d.add(part, cx, cy, z + hb_parts.BEAM_TO_FLOOR, yaw, collection=group)


contact = 0.0  # where the current storey's walls stand
groups = [d.group(name) for name in LEVEL_NAMES]
top = d.group("Level 5 - Deck and parapet")
for i, part in enumerate(STOREYS):
    windows = "LogWindowWall" if i == ROOM_STOREY and ROOM_WINDOWS else None
    # The launch platform's braces come down on the front's middle section and the ramp crosses the left side's
    # middle and back sections, so those windows go in the front-left sections.
    walls(part, contact, groups[i], middle_part=windows,
          special_at={("x", -1): centres[0], ("y", -1): centres[0]} if windows else None)
    beam = contact + WALL_HEIGHT + hb_parts.LOG_R   # the next ring sits on top of these walls
    is_room_floor_below = i + 1 == ROOM_STOREY
    is_deck = i == len(STOREYS) - 1
    # The ring on top of a storey and any floor on it belong to the storey above.
    above = groups[i + 1] if i + 1 < len(groups) else top
    beam_ring(beam, above, with_floor_beams=is_room_floor_below or is_deck)
    if is_room_floor_below:
        floor(beam, above, hatch_at=(centres[-1], centres[0]))  # front-right: rope down inside to the ground
    if is_room_floor_below:
        room_floor = beam
    if is_deck:
        floor(beam, above, stone_at=(centres[-1], centres[-1]), openings=RAMP_OPENING)
        deck = beam
    contact = beam + hb_parts.BEAM_TO_WALL

# The ramp: its lowest plank sits on the room floor just past the beam on the opening's front edge; it rises towards
# the back wall, and at the top you step sideways onto the deck (that edge has no beam), about 0.17 m up.
plank_half = 0.1125                                    # half a plank's thickness
ramp_start_y = middle - M + 0.28                       # front edge of the opening, plus the bottom plank's half depth
ramp_z = room_floor + hb_parts.BEAM_TO_FLOOR + 2 * plank_half
d.add("Ramp", centres[0], ramp_start_y, ramp_z, 90.0, name="Ramp", collection=groups[ROOM_STOREY])
# Rails on the opening's front edge (on the floor beam there) and on its inner edge beside the middle tile, leaving the
# inner edge beside the back tile open for stepping off the ramp.
d.add("Rail", centres[0], middle - M, contact, 90.0, name="Opening rail front", collection=top)
d.add("Rail", centres[0] + M, middle, contact, 0.0, name="Opening rail side", collection=top)

# The parapet, with a gap in the middle of the front for the launch platform.
walls("Parapet", contact, top, skip=("x", -1, middle), middle_part="Parapet2" if CRENELLATED else None)

# Launch platform through the gap, sticking out from the front (the side at -Y); its local -X points outwards.
d.add("Balcony", middle, -half, deck, 90.0, name="Launch platform", collection=top)
for side in (-1, 1):
    d.add("Rail", middle + side * M, -half - M, contact, 0.0, name="Platform rail", collection=top)
# The climbing rope has a front: you climb it facing its local -Y and step off that way at the top. Turned 180
# degrees it faces the tower; the rope itself hangs about 0.1 m towards its front from its top point, so the top point
# sits 0.25 m out and the rope hangs just past the platform's edge.
d.add("Rope", middle, -half - 2 * M - 0.25, deck, 180.0, name="Entrance rope", collection=top)

if CORNER_POSTS:
    posts = d.group("Corner posts")
    post_top = contact + 3 * hb_parts.ROW
    for sx in (-1, 1):
        for sy in (-1, 1):
            z = 2.7 * POSTS_FROM_STOREY
            while z < post_top:
                d.add("CornerPost", sx * (half + POST_OFFSET), sy * (half + POST_OFFSET), z, 0.0, collection=posts)
                z += 2.7

d.save(out_path, view_centre=(-1.5, 0.0, 0.0), view_distance=22.0)
print(f"HB: deck at {deck + hb_parts.BEAM_TO_FLOOR:.2f} m, parapet top at {contact + 3 * hb_parts.ROW:.2f} m")
