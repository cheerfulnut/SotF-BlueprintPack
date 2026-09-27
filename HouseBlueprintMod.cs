using RedLoader;
using Sons.Crafting.Structures;
using SonsSdk;

namespace HouseBlueprint;

public class HouseBlueprintMod : SonsMod
{
    // Set to true to write Mods\HouseBlueprint\diagnostics.txt (the game's recipes, book pages and building pieces).
    private static readonly bool RunDiagnostics = false;

    // Set to true to save a few blueprint book page images to Mods\HouseBlueprint\page-export (local reference only).
    private static readonly bool ExportPages = true;

    public HouseBlueprintMod()
    {
        HarmonyPatchAll = true;
    }

    protected override void OnGameStart()
    {
        try
        {
            // The patches normally registered the recipe already; this is a fallback if they did not run.
            if (StructureCraftingSystem.TryGetInstance(out var system))
                TestWallBlueprint.EnsureRegistered(system._recipeDatabase);
            TestWallBlueprint.EnsureBookPage();
        }
        catch (Exception e)
        {
            RLog.Error($"[HouseBlueprint] Setting up the blueprint failed: {e}");
        }

        if (RunDiagnostics)
            Diagnostics.Run(DataPath);

        if (ExportPages)
            PageExport.Run(DataPath);
    }
}
