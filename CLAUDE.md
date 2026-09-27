# Blueprint Pack

A RedLoader mod for Sons of the Forest that adds buildings to the blueprint book, designed in Blender and built in
the game from the game's own pieces.

**Before any work, read [docs/BUILDING-GUIDE.md](docs/BUILDING-GUIDE.md).** It has the workflow for new buildings,
every verified game fact, the part catalogue, testing steps and lessons learned. Keep it up to date when you learn
something new, and keep [README.md](README.md) (for players) in step with changes to buildings or commands.

Key rules:
- Only change the game's `_Redloader` and `Mods` folders; ask before anything else outside this project folder.
- Never write a `.blend` file the user has open in Blender.
- Show the user previews and get their OK before in-game tests; they test and report back.
- Nothing personal or machine-specific in the repository (`GamePath.user.props` is ignored for that), and no game
  files or game art.
- Recipe ids: 9003 = Stone Tower; 9001 and 9002 are retired; new buildings take the next free id.
