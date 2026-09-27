using HarmonyLib;
using RedLoader;
using Sons.Crafting.Structures;
using Sons.Weapon;

namespace HouseBlueprint;

/// <summary>
///     Registers our recipes as soon as the game's structure crafting system starts, and again just before it restores
///     saved blueprint ghosts, which it looks up by recipe id. Registration is idempotent.
///     Also writes our blueprints' names into the book, which only knows names from its own translation tables.
/// </summary>
[HarmonyPatch]
internal static class Patches
{
    [HarmonyPatch(typeof(StructureCraftingSystem), nameof(StructureCraftingSystem.Start))]
    [HarmonyPrefix]
    private static void OnCraftingSystemStart(StructureCraftingSystem __instance) => Register(__instance);

    [HarmonyPatch(typeof(StructureCraftingSystem),
        "Sons_Save_ISaveGameSerializer_Sons_Crafting_Structures_StructureCraftingSystem_ScrewNodeStructuresData__OnDeserialize")]
    [HarmonyPrefix]
    private static void OnCraftingSystemDeserialize(StructureCraftingSystem __instance) => Register(__instance);

    [HarmonyPatch(typeof(BlueprintBookController), nameof(BlueprintBookController.UpdatePageLocalization))]
    [HarmonyPostfix]
    private static void OnBookPageLocalized(BlueprintBookController __instance) => ShowNames(__instance);

    [HarmonyPatch(typeof(BlueprintBookController), nameof(BlueprintBookController.UpdatePageObjects))]
    [HarmonyPostfix]
    private static void OnBookPageObjects(BlueprintBookController __instance) => ShowNames(__instance);

    private static void Register(StructureCraftingSystem system)
    {
        // Never let our code break the game's start-up or save loading.
        try
        {
            TestWallBlueprint.EnsureRegistered(system._recipeDatabase);
            LayoutBlueprints.EnsureRegistered(system._recipeDatabase);
        }
        catch (Exception e)
        {
            RLog.Error($"[HouseBlueprint] Registering blueprints failed: {e}");
        }
    }

    private static void ShowNames(BlueprintBookController book)
    {
        StructureHealthReport.RunOnce();
        try
        {
            var pages = book._pages._pages;
            var index = book._currentPageIndex;
            if (index < 0 || index >= pages.Count)
                return;

            var page = pages[index];
            if (TryGetName(page._topRecipe, out var top) && book._topBlueprintName != null)
                book._topBlueprintName.text = top;
            if (TryGetName(page._bottomRecipe, out var bottom) && book._bottomBlueprintName != null)
                book._bottomBlueprintName.text = bottom;
        }
        catch (Exception e)
        {
            RLog.Error($"[HouseBlueprint] Showing blueprint names failed: {e.Message}");
        }
    }

    private static bool TryGetName(StructureRecipe recipe, out string name) =>
        LayoutBlueprints.TryGetName(recipe, out name) || TestWallBlueprint.TryGetName(recipe, out name);
}
