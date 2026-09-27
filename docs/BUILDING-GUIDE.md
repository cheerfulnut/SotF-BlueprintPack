# Building guide

How to design, build and test new Blueprint Pack buildings. Written for future Claude sessions (and anyone else
working on the mod). Everything marked VERIFIED was confirmed in the game in September 2026; INFERRED means reasoned
from the game's code or data but not yet tested.

## 1. How it works

```
 describe the building          Blender design file             layout + book page              in the game
 (with the user)       ->  design/<name>.blend (parts)  ->  Blueprints/<name>.json  ->  mod builds the ghost from the
                           made by a generator script        Blueprints/<name>-page.png     game's own blueprint pieces
                           or arranged by hand                                             (free-form, like the cabins)
```

- A **part** (log wall, beam, floor, ramp...) is a group of the game's own building pieces. The mod copies each part
  from one of the game's own blueprints (`PartLibrary.cs`), so ghosts and finished buildings are exactly the game's
  pieces: same durability, repairs and saving as hand-built ones.
- Each layout becomes a **free-form blueprint** (like the game's Small/Medium Cabin): a ghost the player fills with
  materials; when complete it turns into ordinary game structures. VERIFIED: the save then contains no reference to
  the mod.
- The **book page** is a 1024 x 1024 picture we render ourselves (no game art), added at the end of the blueprint's
  book section (section 4, step 2). The book draws the blueprint's name itself; the mod supplies it (`Patches.cs`).

## 2. Repository layout

| Path | What |
|---|---|
| `BlueprintPackMod.cs` | Mod entry point; registers the `bp_remove` console command; research switches (off). |
| `LayoutBlueprints.cs` | Reads `Mods\BlueprintPack\Blueprints\*.json`, builds recipes, ghosts and book pages. |
| `PartLibrary.cs` | Every part type: which game blueprint it is copied from and how it is changed. |
| `Patches.cs` | Registers recipes before saves load; writes our names into the book. |
| `StructureRemover.cs` | `bp_remove` console command. |
| `Diagnostics.cs`, `PageExport.cs`, `StructureHealthReport.cs` | Research helpers (see section 9). |
| `blender/hb_parts.py` | Kit parts: sizes, stand-in meshes, ingredient counts for the page. |
| `blender/hb_design.py` | Design-file setup (kit column, locked parts, snapping, level collections, book section). |
| `blender/make_kit.py`, `blender/make_tower.py` | Generators for design files. |
| `blender/export_design.py` | Design file -> layout JSON + renders (Blender, headless). |
| `blender/make_page.py` | Renders -> book page PNG (Python + Pillow). |
| `blender/render_preview.py` | Preview and cutaway renders for showing the user. |
| `design/*.blend` | Design files. `Blueprints/` holds what gets installed. `build/`, `dist/` are generated. |
| `tools/typedump` | Read-only inspector for the game's generated DLLs (section 9). |

The mod was first called House Blueprint: the Blender scripts' `hb_` prefix and the `hb_part` / `hb_section`
properties in design files come from that; they stay as they are so existing design files keep working.

## 3. Tools and setup

- **.NET 10 SDK** (version pinned in `global.json`).
- **RedLoader 0.9.2+** installed in the game, and the game run once, so `_Redloader\Game` exists.
- **Game folder**: `GamePath.user.props` next to `BlueprintPack.csproj` (not committed; see README). Blender and
  Python paths below are whatever is installed on the machine.
- **Blender 5.2** (LTS). Always run headless for scripted work:
  `blender --background --factory-startup --python <script> -- <args>` (new files), or
  `blender --background <file.blend> --python <script> -- <args>` (reading a design). `--factory-startup` ignores the
  user's own Blender settings and add-ons.
- **Python 3 with Pillow** for `make_page.py`.

## 4. Making a new building, step by step

1. **Agree the design with the user.** Ask about size, levels, access (ropes, ramps), openings, defences, looks, and
   where it will stand (flat ground is needed; section 8). Offer choices rather than long explanations.
2. **Write a generator** `blender/make_<name>.py`, modelled on `make_tower.py`: compute every part's position from a
   few constants (sections per side, storeys, which walls have windows...). Put each level in its own collection
   (`d.group("Level 2 - Logs")`) so the user can hide levels in Blender to look inside. Choose the book section with
   `hb_design.Design("<Name (n levels)>", section="...")`: `Shelter`, `TreeShelter`, `Platform`, `DefensiveWalls`,
   `Furniture`, `Storage`, `Utility`, `Traps` or `Rafts`. The page gets that section's colours and goes at the end of
   it (Shelter is tested; the others are INFERRED to work the same way). Hand-arranging in Blender also
   works (the kit column is on the left; every part outside it is exported), but users find it fiddly.
3. **Generate the design file.** It refuses to overwrite an existing file unless `--force`. **Never write a `.blend`
   the user has open in Blender**; ask them to close it, or write a new file name.
   ```
   blender --background --factory-startup --python blender/make_<name>.py -- design/<name>.blend [--force]
   ```
4. **Show the user previews** and iterate. They check things renders miss, so ask them to open the file and look.
   ```
   blender --background design/<name>.blend --python blender/render_preview.py -- build/<name>-outside.png
   blender --background design/<name>.blend --python blender/render_preview.py -- build/<name>-cutaway.png 7.0
   ```
5. **Pick a recipe id.** Use the next free id: `9003` is the Stone Tower; `9001` and `9002` were test blueprints and
   are retired (do not reuse them). The network prefab id is the recipe id + 10000. Game recipe ids are below 200.
6. **Export the layout and make the page.**
   ```
   blender --background design/<name>.blend --python blender/export_design.py -- <id> build/<name>
   python blender/make_page.py build/<name> Blueprints
   ```
   Check the export's warnings and the ingredient totals; view `Blueprints/<name>-page.png`.
7. **Build the mod** if any part code changed: `dotnet build -c Release` (add `-t:Package` for the release zip).
8. **Install** (game closed): copy `bin\Release\net10.0\BlueprintPack.dll` to `<game>\Mods\`, and the layout and
   page to `<game>\Mods\BlueprintPack\Blueprints\`. Record a before/after listing of the game folder (section 10).
9. **Test in a Peaceful single-player save** (section 7), read `<game>\_Redloader\Latest.log` (every part logs the
   ingredients it made; errors say which part failed), and commit.

## 5. Coordinates and the building grid

- Blender is Z-up and right-handed; the game is Y-up and left-handed. The exporter converts:
  game (x, y, z) = Blender (x, z, y), and a turn of A degrees around Blender Z is -A degrees around the game's Y.
  Turns within 2 degrees of a right angle are straightened.
- **Grid** (VERIFIED from the game's cabins): wall sections are centred 2.26 m apart along a line; walls meeting at a
  corner are 1.13 m from each other's centre. Logs are 0.45 m thick and 2.53 m long, so they overlap at corners.
  Floor tiles are 2.26 m square. The kit snaps to 0.565 m.
- **Vertical layout** (VERIFIED from the LookOut Tower): a ring of beams every 2.70 m; walls stand on the beams
  (wall origin = beam centre + 0.225 m); floor planks' centre = beam centre + 0.163 m. A storey needs beams but not a
  floor. The Stone Tower's heights: stone storey on the ground, beam rings at 2.475, 5.175, 7.875 (room floor) and
  10.575 m (deck), parapet from 10.80 m.

## 6. Part catalogue

Origins are in the part's own frame (Blender terms; walls run along local Y). Ingredient counts are what the page
shows; the mod counts the real pieces and logs them.

| Part | Copied from (recipe id) | Origin | Cost | Notes |
|---|---|---|---|---|
| `LogWall` | Small Cabin (34), 5-log wall | ground, wall centre | 5 logs | 2.26 m section, 2.25 m high |
| `LogWindowWall` | LogWall, rows 3-4 swapped for window logs | as LogWall | 3 logs, 2 quarter logs | window 0.9 m high, 0.9 m up. The Small Cabin's all-window wall is really its doorway. |
| `StoneWall` | LogWall, logs swapped for stone chunks 204/230/231/232 | as LogWall | 20 stones | 4 chunks per row; no game blueprint has stone walls |
| `Parapet`, `Parapet2`, `Rail` | LogWall kept to its lowest 3 / 2 / 1 logs | as LogWall | 3 / 2 / 1 logs | |
| `Door` | LookOut Tower (38) door | ground | 1 plank | 1.1 m wide |
| `PlankPlatform` | Small Platform (98), whole ghost minus rope | ground, centre | 4 logs, 5 planks | |
| `Beam` | LookOut Tower `Beam27` | log centre | 1 log | game pivot is at one end; the mod corrects |
| `Floor` | LookOut Tower `Floor4` | planks' centre | 5 planks | planks run along local Y |
| `StoneFloor` | Floor, planks swapped for RockFloor 270-274 | as Floor | 5 stones | the game merges it into its own combined stone floor |
| `HatchFloor` | LookOut Tower `Floor5` + `Preset2` rope | as Floor | 5 planks, 1 rope | hole and rope on local +X: see section 8 |
| `Rope` | LookOut Tower `Preset2` | rope top | 1 rope | has a front: see section 8 |
| `Balcony` | LookOut Tower `Beam38/39`, `Floor6`, `Strut3/4` | wall line at beam height | 2 logs, 2 half logs, 5 planks | sticks out along local -X |
| `CornerPost` | Medium Cabin (30) `Pilar6` + full log post piece | foot | 1 log | 2.7 m |
| `Ramp` | Medium Cabin `Ramp1`, rebuilt as 11 planks | lowest plank's centre | 11 planks | rises along local +X at 30.852 degrees |

## 7. Testing in the game

- Use a **Peaceful** single-player save (the game has no Creative mode). Back up saves first if they matter.
- **F1** opens the game console. RedLoader's `finishblueprints <radius>` completes ghosts within the radius instantly
  (skips the material cost; test saves only). `cancelblueprints <radius>` removes ghosts. Our `bp_remove` removes
  finished buildings (README). `noforest` clears trees (unknown whether that persists in the save).
- Checklist for a new building: page (name, picture, ingredients); ghost appears whole and fits the ground; finishes
  without errors; walk every route (ropes both ways, ramps both ways, no crouching); nothing falls; save, reload.
- The mod writes each built structure's health to the log the first time the book is opened in a session
  (`StructureHealthReport`).

## 8. Game facts and lessons learned

- **Health** (VERIFIED): 60 per piece for every material; a structure's health is 60 x its pieces. A log wall is
  300/300. Our 20-chunk stone wall starts at 1200 but its maximum is 300, like the game's 5-row stone walls. Whether
  stone resists damage better is untested (the game has a rock flag and separate siege/explosion/projectile ratios).
- **Ropes have a front** (VERIFIED): you climb standing on the rope's local +Z side (game axes), facing local -Z, and
  step off towards local -Z at the top. The tower's entrance rope is turned 180 degrees so it steps off onto the
  platform.
- **Hatches**: the hole and rope are on the tile's local +X side and you step off that way. Turn hatches so that side
  faces open floor, not a wall (the tower turns hatches on its +X half by 180 degrees).
- **Headroom**: beams under a floor are only 2.2 m above the floor below. Anyone on a ramp or step under a beam has
  to crouch. Keep ramps, their ends and their landings out from under beams, and drop beams that would cross an
  opening (the tower skips floor beams with an opening on both sides).
- **Ramps**: the game's roof angle, 30.852 degrees, rises exactly one storey (2.70 m) over two floor tiles. A ramp needs
  open floor at its bottom end; at the top you can step sideways onto a floor edge that has no beam under it. The
  tower's ramp is 11 planks (a 0.17 m step at the top) so it fits between a beam and the back wall.
- **Window walls**: the Small Cabin's wall of window logs is cut through all five rows, a doorway; a window needs full
  logs above and below.
- **Launch platform braces** come down on the wall below the platform's middle section: keep windows out of that
  section.
- **Unsupported pieces**: rails standing on floor planks (not beams) and posts not reaching the ground worked in the
  tower, but support rules are not fully understood; check new cases in the game.
- **Ghosts in multiplayer** (INFERRED): a ghost is a network object with our prefab id, so every player needs the mod
  while one exists. Finished buildings need nothing.
- **Build order**: the ghost asks for one material at a time, in the order of each material's lowest piece (stones,
  then logs, then planks...). Not yet improved.
- **Book**: our page goes after the game's Shelter pages; a page with no bottom blueprint works. Game page textures
  only load full resolution while shown, so never build pages from the game's own art.
- **bp_remove**: the game's `StructureDestructionManager.DestroyStructureDebug` reports success without removing
  anything; `RuntimeStructureDatabase.DestroyStructure` works. The game's own "super structure" grouping does not join
  a blueprint-built tower, so the command finds buildings by touching pieces. It must never remove tarps (save points).

## 9. Research tools

- `Diagnostics.cs` (switch `RunDiagnostics` in `BlueprintPackMod.cs`): writes the game's recipes, book pages,
  building-piece profiles (ids, items, sizes) and the layout of every free-form and shelter blueprint to
  `Mods\BlueprintPack\diagnostics.txt`. This is where templates and piece ids come from.
- `PageExport.cs` (switch `ExportPages`): saves some book pages as PNGs, for layout reference only. They are the
  game's artwork: never commit or publish them.
- `tools/typedump`: `dotnet build tools/typedump -c Release -o tools/typedump/out`, then
  `typedump <dll or folder> <type regex> [--members <regex>]` on `<game>\_Redloader\Game` lists types, fields,
  properties and method signatures. Reads the files as data only.
- Save files (`%USERPROFILE%\AppData\LocalLow\Endnight\SonsOfTheForest\Saves\<id>\...\SaveData.zip`) are zipped JSON;
  `ConstructionsSaveData.json` lists built structures with their pieces' profile ids and positions. Building a
  sample by hand and reading it back is the reliable way to learn a structure the game's blueprints do not contain.

## 10. Working rules

- Only change the game's `_Redloader` and `Mods` folders; never system settings or other software. Ask before
  anything outside the project folder.
- Before installing, record a listing (size and time) of every game file outside `_Redloader`; after the test, compare
  and report what changed. Expected: our files in `Mods`, and mod settings re-saved in `UserData`.
- Back up saves before risky tests; never touch the user's multiplayer saves without asking.
- Nothing personal in the repository: machine paths go in `GamePath.user.props` (ignored); the DLL maps source paths
  to `/_/`; no game files or game art.
- One change per commit, with a message explaining why.

## 11. Releasing

The repository is public: <https://github.com/cheerfulnut/SotF-BlueprintPack>. For a new release:
1. Bump the version in `BlueprintPack.csproj` (`<Version>`) and `manifest.json` (`version`) together.
2. Update README.md (buildings, materials) and this guide.
3. Before pushing, check for anything personal or machine-specific: no local paths, user names or save ids in files
   or commit messages, and the DLL's embedded debug information lists only `/_/...` source paths.
4. `dotnet build -c Release -t:Package`, then
   `gh release create v<version> dist/BlueprintPack-<version>.zip --title "Blueprint Pack <version>" --notes-file <notes>`
   with notes for players: what is new, materials, install, known limits, and the zip's SHA-256.
5. Only publish after the user has tested the build in the game and agreed.

## 12. Open items

- Automatic foundations for slopes (the Small Cabin's `AutoFoundations`), so buildings sit well on hills.
- Build order: let players build level by level instead of one material at a time.
- Roof parts (the cabins' `RampStructure`/`ApexStructure` roofs) for pitched-roof buildings.
- Confirm in multiplayer that finished buildings work for players without the mod.
- Durability against real enemies, especially stone.
- Next buildings the user has in mind: cabins, a large multi-storey hall, a gatehouse (see the reference fort they
  shared: palisade, pitched-roof cabins, hall, tall tower, gatehouse). The palisade itself is best left to the game's
  own Defensive Wall blueprint.
