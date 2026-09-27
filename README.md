# Blueprint Pack

A [RedLoader](https://github.com/enderfish/RedLoader) mod for **Sons of the Forest** that adds new buildings to the
blueprint book. You place them and build them the normal way: put down the ghost, carry the materials to it, and
it turns into an ordinary building made of the game's own logs, planks and stones.

## Buildings

### Stone Tower (5 levels) - Shelter section

A five-level watchtower, about 12 m tall, on a 6.8 m square footprint:

| Level | What it is |
|---|---|
| 1 | Stone storey, no door |
| 2-3 | Log storeys (the tower's body) |
| 4 | Enclosed room with a window on each side, a hatch with a rope down to the ground inside |
| 5 | Open deck with a crenellated parapet, a stone patch for a campfire, and a ramp down to the room |

A launch platform sticks out from the front of the deck (for hang gliding), with the entrance rope hanging from it.

**Materials:** 275 logs, 91 planks, 245 stones, 8 quarter logs, 2 half logs, 2 ropes.

## Requirements

- Sons of the Forest (tested with the game version current in September 2026).
- RedLoader for .NET 10, **0.9.2 or later**, from [enderfish/RedLoader](https://github.com/enderfish/RedLoader/releases).
  Run the game once with RedLoader installed before adding mods.

## Installing

1. Download `BlueprintPack-<version>.zip` from this repository's releases.
2. Extract it into the game folder (the one with `SonsOfTheForest.exe`). It contains:
   ```
   Mods\BlueprintPack.dll
   Mods\BlueprintPack\manifest.json
   Mods\BlueprintPack\Blueprints\stone-tower.json
   Mods\BlueprintPack\Blueprints\stone-tower-page.png
   ```
3. Start the game. Each new building's page is at the end of its section in the blueprint book (the Stone Tower is
   after the game's own Shelter pages).

To uninstall, delete `Mods\BlueprintPack.dll` and the `Mods\BlueprintPack` folder.

## Good to know

- **Finished buildings don't depend on the mod.** They are saved exactly like buildings made by hand, so they stay if
  the mod is removed. Only an *unfinished* ghost needs the mod.
- **Multiplayer:** everyone should have the mod while a ghost from it exists. A player without the mod who joins while
  one exists may not see it, or may run into errors. Finish (or cancel) such ghosts before they join. Finished
  buildings should work for everyone; this has not been tested in multiplayer yet.
- **Flat ground:** the tower has no automatic foundation yet, so place it on fairly flat, open ground (about 7 x 7 m).
- **Materials are asked for one type at a time** (stones first, then logs, and so on), as the ghost works through them.
- **Strength:** stone and log pieces have the same health in the game (60 each); whether stone resists attacks better
  has not been tested.

## Console command for testing

`bp_remove` (in the game's console, opened with F1) removes a finished building, for clearing test builds:

| Type | What happens |
|---|---|
| `bp_remove` | Look at a building first. Reports what it would remove; nothing happens yet. |
| `bp_remove 15` | Reports the building parts within 15 m of you instead. |
| `bp_remove confirm` | Removes what the last report found (within 30 seconds). |
| `bp_remove cancel` | Forgets the report. |

It only removes building parts (walls, beams, floors, posts, ramps, stairs, doors, climbing ropes) and leaves tarps,
beds, fires, storage, traps and defensive walls alone. It removes *any* building parts it reaches, including ones you
built by hand, so check the report before confirming. Removal cannot be undone.

## Making new buildings

Buildings are designed in Blender with a kit of parts at the game's real sizes and exported to a layout file; the mod
builds the ghost from the game's own pieces. See [docs/BUILDING-GUIDE.md](docs/BUILDING-GUIDE.md).

## Building from source

Needs the .NET 10 SDK (see `global.json`), and RedLoader installed in the game with the game run once (the project
builds against the game code RedLoader generates). If your game is not in Steam's default folder, create
`GamePath.user.props` next to `BlueprintPack.csproj`:

```xml
<Project><PropertyGroup><GamePath>D:\Games\Steam\steamapps\common\Sons Of The Forest</GamePath></PropertyGroup></Project>
```

Then `dotnet build -c Release -t:Package` builds the mod and writes the release zip to `dist\`.

## Licence

MIT; see [LICENSE](LICENSE).

## Credits

Built on RedLoader and its SDK. Sons of the Forest is made by Endnight Games; this mod is not affiliated with or
endorsed by them. The mod contains no game files: the blueprint pictures are rendered from our own models.
