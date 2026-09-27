using SonsSdk;

namespace HouseBlueprint;

public class HouseBlueprintMod : SonsMod
{
    protected override void OnGameStart()
    {
        // Research build: record how the game's blueprints and building pieces are set up. Reads only; changes nothing.
        Diagnostics.Run(DataPath);
    }
}
