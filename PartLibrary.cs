using Construction;
using Il2CppInterop.Runtime;
using RedLoader;
using Sons.Crafting.Structures;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseBlueprint;

/// <summary>
///     The parts a layout can use, each copied from one of the game's own free-form blueprints so that the ghost and
///     the finished structure are made of the game's own building pieces. Positions are in metres in the game's axes,
///     with a part's origin where it touches the ground; walls run along the part's local Z axis.
/// </summary>
internal static class PartLibrary
{
    private const int SmallCabin = 34, LookOutTower = 38, SmallPlatform = 98;
    private const ushort LogProfile = 5, LogWindowProfile = 4, ClimbRopeProfile = 190;
    private const int StoneItem = 640;

    // A wall's pivot is the centre of its bottom log, half a log above the ground.
    private static readonly Vector3 WallPivot = new(0f, 0.225f, 0f);

    // Stone wall pieces: 0.62 m long, one stone each, four to a 2.53 m row (the game's RockWallChunk profiles).
    private static readonly ushort[] StoneChunkProfiles = { 204, 230, 231, 232 };
    private const float StoneChunkSpacing = 0.62f;

    /// <summary>Copies the part's game pieces into the ghost. Returns false if the part could not be made.</summary>
    public static bool Place(StructureRecipeDatabase db, string type, Transform ghostRoot, Vector3 position, float yaw)
    {
        var rotation = Quaternion.Euler(0f, yaw, 0f);
        switch (type)
        {
            case "LogWall":
                return PlaceSingle(FindWall(db, SmallCabin, LogProfile), ghostRoot, position + rotation * WallPivot, rotation) != null;

            case "LogWindowWall":
                return PlaceSingle(FindWall(db, SmallCabin, LogWindowProfile), ghostRoot, position + rotation * WallPivot, rotation) != null;

            case "StoneWall":
            {
                var wall = PlaceSingle(FindWall(db, SmallCabin, LogProfile), ghostRoot, position + rotation * WallPivot, rotation);
                return wall != null && ReplaceLogsWithStone(wall);
            }

            case "Door":
                return PlaceSingle(FindStructure<DoorStructure>(db, LookOutTower), ghostRoot, position, rotation) != null;

            case "PlankPlatform":
                return PlaceGroup(db, SmallPlatform, ghostRoot, position, rotation);

            default:
                RLog.Error($"[HouseBlueprint] Unknown part type '{type}'.");
                return false;
        }
    }

    private static Transform GhostOf(StructureRecipeDatabase db, int recipeId)
    {
        if (!db.TryGetRecipeById(recipeId, out var recipe) || recipe._structureNodePrefab == null)
            return null;
        var ghost = recipe._structureNodePrefab.GetComponentInChildren<FreeFormGhostPrefab>(true);
        return ghost == null ? null : ghost.transform;
    }

    /// <summary>The first wall in a blueprint made of five pieces of one profile.</summary>
    private static Transform FindWall(StructureRecipeDatabase db, int recipeId, ushort profile)
    {
        var ghost = GhostOf(db, recipeId);
        if (ghost == null)
            return null;
        foreach (var wall in ghost.GetComponentsInChildren<WallStructure>(true))
        {
            var elements = wall.GetComponentsInChildren<StructureElement>(true);
            if (elements.Length == 5 && elements.All(e => e._profile != null && e._profile._id == profile))
                return wall.transform;
        }

        return null;
    }

    private static Transform FindStructure<T>(StructureRecipeDatabase db, int recipeId) where T : Component
    {
        var ghost = GhostOf(db, recipeId);
        if (ghost == null)
            return null;
        var found = ghost.GetComponentsInChildren<T>(true);
        return found.Length > 0 ? found[0].transform : null;
    }

    /// <summary>One structure, placed with its pivot at the given point, ignoring where it sat in its blueprint.</summary>
    private static Transform PlaceSingle(Transform template, Transform ghostRoot, Vector3 position, Quaternion rotation)
    {
        if (template == null)
        {
            RLog.Error("[HouseBlueprint] A part's template was not found in the game's blueprints.");
            return null;
        }

        var copy = Object.Instantiate(template.gameObject, ghostRoot).transform;
        copy.localPosition = position;
        copy.localRotation = rotation;
        return copy;
    }

    /// <summary>Every structure of a blueprint's ghost, keeping their positions relative to the blueprint's origin.</summary>
    private static bool PlaceGroup(StructureRecipeDatabase db, int recipeId, Transform ghostRoot, Vector3 position, Quaternion rotation)
    {
        var ghost = GhostOf(db, recipeId);
        if (ghost == null)
            return false;

        var placed = 0;
        for (var i = 0; i < ghost.childCount; i++)
        {
            var child = ghost.GetChild(i);
            if (child.GetComponent<Structure>() == null)
                continue; // e.g. the climbing rope that comes with the platform
            var element = child.GetComponentInChildren<StructureElement>(true);
            if (element != null && element._profile != null && element._profile._id == ClimbRopeProfile)
                continue;

            var copy = Object.Instantiate(child.gameObject, ghostRoot).transform;
            copy.localPosition = position + rotation * child.localPosition;
            copy.localRotation = rotation * child.localRotation;
            placed++;
        }

        return placed > 0;
    }

    /// <summary>
    ///     No game blueprint has a stone wall, so turn a copied log wall into one: each log becomes four stone pieces
    ///     of the game's own stone wall profiles, each needing one stone.
    /// </summary>
    private static bool ReplaceLogsWithStone(Transform wall)
    {
        var logs = wall.GetComponentsInChildren<StructureElement>(true);
        var profiles = StoneChunkProfiles.Select(id => ElementProfileDatabase.GetProfile(id)).ToArray();
        if (profiles.Any(p => p == null || p._prefab == null))
        {
            RLog.Error("[HouseBlueprint] Stone wall pieces not found in the game's piece database.");
            return false;
        }

        foreach (var log in logs)
        {
            var row = log.transform.localPosition.y;
            var logIngredient = log.GetComponent<StructureCraftingNodeIngredient>();
            var requiresPrevious = logIngredient != null && logIngredient._requiresAllPreviousIngredients;
            Object.DestroyImmediate(log.gameObject);

            for (var j = 0; j < StoneChunkProfiles.Length; j++)
            {
                var stone = Object.Instantiate(profiles[j]._prefab.gameObject, wall);
                stone.transform.localPosition = new Vector3(0f, row, (j - 1.5f) * StoneChunkSpacing);
                stone.transform.localRotation = Quaternion.identity;

                var ingredient = stone.AddComponent(Il2CppType.Of<StructureCraftingNodeIngredient>())
                                      .Cast<StructureCraftingNodeIngredient>();
                ingredient._itemId = StoneItem;
                ingredient._requiresAllPreviousIngredients = requiresPrevious;

                foreach (var renderer in stone.GetComponentsInChildren<Renderer>(true))
                    if (renderer.GetComponent<StructureGhostSwapper>() == null)
                        renderer.gameObject.AddComponent(Il2CppType.Of<StructureGhostSwapper>());
            }
        }

        return true;
    }
}
