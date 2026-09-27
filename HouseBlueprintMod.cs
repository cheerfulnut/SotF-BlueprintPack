using RedLoader;
using Sons.Crafting.Structures;
using SonsSdk;
using SonsSdk.Attributes;

namespace HouseBlueprint;

public class HouseBlueprintMod : SonsMod
{
    // Set to true to write Mods\HouseBlueprint\diagnostics.txt (the game's recipes, book pages and building pieces).
    private static readonly bool RunDiagnostics = false;

    // Set to true to save a few blueprint book page images to Mods\HouseBlueprint\page-export (local reference only).
    private static readonly bool ExportPages = false;

    public HouseBlueprintMod()
    {
        HarmonyPatchAll = true;
    }

    /// <summary>F1 console: removes a finished building (see StructureRemover).</summary>
    [DebugCommand("hb_remove")]
    private static void RemoveCommand(string args) => StructureRemover.Command(args);

    protected override void OnGameStart()
    {
        try
        {
            // The patches normally registered the recipe already; this is a fallback if they did not run.
            if (StructureCraftingSystem.TryGetInstance(out var system))
                LayoutBlueprints.EnsureRegistered(system._recipeDatabase);
            LayoutBlueprints.EnsureBookPages();
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
