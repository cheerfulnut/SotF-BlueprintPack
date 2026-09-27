using HarmonyLib;
using RedLoader;
using Sons.Crafting.Structures;

namespace HouseBlueprint;

/// <summary>
///     Registers our recipes as soon as the game's structure crafting system starts, and again just before it restores
///     saved blueprint ghosts, which it looks up by recipe id. Registration is idempotent.
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

    private static void Register(StructureCraftingSystem system)
    {
        // Never let our code break the game's start-up or save loading.
        try
        {
            TestWallBlueprint.EnsureRegistered(system._recipeDatabase);
        }
        catch (Exception e)
        {
            RLog.Error($"[HouseBlueprint] Registering blueprints failed: {e}");
        }
    }
}
