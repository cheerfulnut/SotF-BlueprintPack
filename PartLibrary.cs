using Construction;
using Il2CppInterop.Runtime;
using RedLoader;
using Sons.Crafting.Structures;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseBlueprint;

/// <summary>
///     The parts a layout can use, each copied from the game's own free-form blueprints so that the ghost and the
///     finished structure are made of the game's own building pieces. Positions are in metres in the game's axes.
///     Walls, parapets, rails, doors, platforms and posts have their origin where they stand; walls run along the
///     part's local Z axis. Beams, floors, hatches, balconies and ropes use the origin blender/hb_parts.py describes.
/// </summary>
internal static class PartLibrary
{
    private const int MediumCabin = 30, SmallCabin = 34, LookOutTower = 38, SmallPlatform = 98;
    private const ushort LogProfile = 5, LogWindowProfile = 4, LogPilarProfile = 2, ClimbRopeProfile = 190;
    private const int LogItem = 78, StoneItem = 640, QuarterLogItem = 406;

    // A wall's pivot is the centre of its bottom log, half a log above where it stands.
    private static readonly Vector3 WallPivot = new(0f, 0.225f, 0f);

    // Stone wall pieces: 0.62 m long, one stone each, four to a 2.53 m row (the game's RockWallChunk profiles).
    private static readonly ushort[] StoneChunkProfiles = { 204, 230, 231, 232 };
    private const float StoneChunkSpacing = 0.62f;

    // Flat stones the size of a plank (the game's RockFloor profiles), one stone each.
    private static readonly ushort[] StoneFloorProfiles = { 270, 271, 272, 273, 274 };

    // The LookOut Tower's launch platform: two beams, a floor and two braces, placed relative to the point on its wall
    // line at the beams' height; it sticks out along -X.
    private static readonly string[] BalconyStructures = { "Beam38", "Beam39", "Floor6", "Strut3", "Strut4" };
    private static readonly Vector3 BalconyAnchor = new(-0.786f, 5.77f, 0f);

    /// <summary>Copies the part's game pieces into the ghost. Returns false if the part could not be made.</summary>
    public static bool Place(StructureRecipeDatabase db, string type, Transform ghostRoot, Vector3 position, float yaw)
    {
        var r = Quaternion.Euler(0f, yaw, 0f);
        switch (type)
        {
            case "LogWall":
                return PlaceSingle(FindWall(db, SmallCabin, LogProfile), ghostRoot, position + r * WallPivot, r) != null;
            case "LogWindowWall":
            {
                // The Small Cabin's window-log wall is cut through all five rows (a doorway): use full logs with only
                // rows 3 and 4 (counting from the bottom) cut, a window about 0.9 m high starting 0.9 m up.
                var wall = PlaceSingle(FindWall(db, SmallCabin, LogProfile), ghostRoot, position + r * WallPivot, r);
                return wall != null && SwapRows(wall, new[] { 2, 3 }, LogWindowProfile, QuarterLogItem);
            }
            case "StoneWall":
            {
                var wall = PlaceSingle(FindWall(db, SmallCabin, LogProfile), ghostRoot, position + r * WallPivot, r);
                return wall != null && ReplaceLogsWithStone(wall);
            }
            case "Parapet":
            case "Parapet2":
            case "Rail":
            {
                var wall = PlaceSingle(FindWall(db, SmallCabin, LogProfile), ghostRoot, position + r * WallPivot, r);
                return wall != null && KeepLowest(wall, type == "Parapet" ? 3 : type == "Parapet2" ? 2 : 1);
            }
            case "Door":
                return PlaceSingle(FindFirst<DoorStructure>(db, LookOutTower), ghostRoot, position, r) != null;
            case "PlankPlatform":
                return PlaceWholeGhost(db, SmallPlatform, ghostRoot, position, r);
            case "Beam":
                return PlaceBeam(FindNamed(db, LookOutTower, "Beam27"), ghostRoot, position, r);
            case "Floor":
                return PlaceSingle(FindNamed(db, LookOutTower, "Floor4"), ghostRoot, position, r) != null;
            case "StoneFloor":
            {
                var floor = PlaceSingle(FindNamed(db, LookOutTower, "Floor4"), ghostRoot, position, r);
                return floor != null && ReplacePlanksWithStone(floor);
            }
            case "HatchFloor":
                return PlaceRelative(db, LookOutTower, new[] { "Floor5", "Preset2" }, "Floor5", ghostRoot, position, r);
            case "Balcony":
                return PlaceRelative(db, LookOutTower, BalconyStructures, null, ghostRoot, position, r);
            case "Rope":
                return PlaceSingle(FindNamed(db, LookOutTower, "Preset2"), ghostRoot, position, r) != null;
            case "CornerPost":
            {
                var post = PlaceSingle(FindNamed(db, MediumCabin, "Pilar6"), ghostRoot, position, r);
                return post != null && ReplaceWithFullPost(post);
            }
            default:
                RLog.Error($"[HouseBlueprint] Unknown part type '{type}'.");
                return false;
        }
    }

    // --- Finding templates in the game's blueprints -----------------------------------------------------------------

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

    private static Transform FindFirst<T>(StructureRecipeDatabase db, int recipeId) where T : Component
    {
        var ghost = GhostOf(db, recipeId);
        if (ghost == null)
            return null;
        var found = ghost.GetComponentsInChildren<T>(true);
        return found.Length > 0 ? found[0].transform : null;
    }

    private static Transform FindNamed(StructureRecipeDatabase db, int recipeId, string name)
    {
        var ghost = GhostOf(db, recipeId);
        return ghost == null ? null : ghost.Find(name);
    }

    // --- Placing copies ---------------------------------------------------------------------------------------------

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

    /// <summary>A beam, placed by the centre of its log (its own pivot is at one end).</summary>
    private static bool PlaceBeam(Transform template, Transform ghostRoot, Vector3 centre, Quaternion rotation)
    {
        var element = template == null ? null : template.GetComponentInChildren<StructureElement>(true);
        if (element == null)
            return PlaceSingle(null, ghostRoot, centre, rotation) != null;
        return PlaceSingle(template, ghostRoot, centre - rotation * element.transform.localPosition, rotation) != null;
    }

    /// <summary>
    ///     Several structures of a blueprint, keeping their positions relative to each other: to the named anchor
    ///     structure (placed at the given point with its own rotation replaced), or to the balcony anchor point.
    /// </summary>
    private static bool PlaceRelative(StructureRecipeDatabase db, int recipeId, string[] names, string anchorName,
                                      Transform ghostRoot, Vector3 position, Quaternion rotation)
    {
        var ghost = GhostOf(db, recipeId);
        if (ghost == null)
            return false;

        var anchor = anchorName == null ? null : ghost.Find(anchorName);
        var anchorPosition = anchor != null ? anchor.localPosition : BalconyAnchor;
        var toPart = rotation * Quaternion.Inverse(anchor != null ? anchor.localRotation : Quaternion.identity);

        foreach (var name in names)
        {
            var child = ghost.Find(name);
            if (child == null)
            {
                RLog.Error($"[HouseBlueprint] '{name}' not found in blueprint #{recipeId}.");
                return false;
            }

            var copy = Object.Instantiate(child.gameObject, ghostRoot).transform;
            copy.localPosition = position + toPart * (child.localPosition - anchorPosition);
            copy.localRotation = toPart * child.localRotation;
        }

        return true;
    }

    /// <summary>Every structure of a blueprint's ghost, keeping their positions relative to the blueprint's origin.</summary>
    private static bool PlaceWholeGhost(StructureRecipeDatabase db, int recipeId, Transform ghostRoot, Vector3 position, Quaternion rotation)
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

    // --- Changing the pieces of a copied structure ------------------------------------------------------------------

    /// <summary>Keeps a wall's lowest logs, for parapets and rails.</summary>
    private static bool KeepLowest(Transform wall, int count)
    {
        var elements = wall.GetComponentsInChildren<StructureElement>(true).OrderBy(e => e.transform.localPosition.y).ToList();
        foreach (var extra in elements.Skip(count))
            Object.DestroyImmediate(extra.gameObject);
        return elements.Count >= count;
    }

    /// <summary>Swaps the given rows (0 = bottom) of a copied wall for pieces of another profile.</summary>
    private static bool SwapRows(Transform wall, int[] rows, ushort profile, int itemId)
    {
        if (!ProfilesExist(new[] { profile }))
            return false;

        var elements = wall.GetComponentsInChildren<StructureElement>(true).OrderBy(e => e.transform.localPosition.y).ToList();
        foreach (var row in rows.Where(r => r < elements.Count))
        {
            var t = elements[row].transform;
            var (position, rotation, requiresPrevious) = (t.localPosition, t.localRotation, RequiresPrevious(elements[row]));
            Object.DestroyImmediate(elements[row].gameObject);
            AddElement(wall, profile, position, rotation, itemId, requiresPrevious);
        }

        return true;
    }

    /// <summary>No game blueprint has a stone wall: each log of a copied log wall becomes four stone wall pieces.</summary>
    private static bool ReplaceLogsWithStone(Transform wall)
    {
        var logs = wall.GetComponentsInChildren<StructureElement>(true);
        if (!ProfilesExist(StoneChunkProfiles))
            return false;

        foreach (var log in logs)
        {
            var row = log.transform.localPosition.y;
            var requiresPrevious = RequiresPrevious(log);
            Object.DestroyImmediate(log.gameObject);
            for (var j = 0; j < StoneChunkProfiles.Length; j++)
                AddElement(wall, StoneChunkProfiles[j], new Vector3(0f, row, (j - 1.5f) * StoneChunkSpacing),
                           Quaternion.identity, StoneItem, requiresPrevious);
        }

        return true;
    }

    /// <summary>No game blueprint has a stone floor: each plank of a copied floor becomes a flat stone.</summary>
    private static bool ReplacePlanksWithStone(Transform floor)
    {
        if (!ProfilesExist(StoneFloorProfiles))
            return false;

        var planks = floor.GetComponentsInChildren<StructureElement>(true);
        for (var i = 0; i < planks.Length; i++)
        {
            var t = planks[i].transform;
            var (position, rotation, requiresPrevious) = (t.localPosition, t.localRotation, RequiresPrevious(planks[i]));
            Object.DestroyImmediate(planks[i].gameObject);
            AddElement(floor, StoneFloorProfiles[i % StoneFloorProfiles.Length], position, rotation, StoneItem, requiresPrevious);
        }

        return true;
    }

    /// <summary>The Medium Cabin's posts are quarter-log stubs; swap in a full 2.7 m log post standing on the same foot.</summary>
    private static bool ReplaceWithFullPost(Transform post)
    {
        var stub = post.GetComponentInChildren<StructureElement>(true);
        var profile = ElementProfileDatabase.GetProfile(LogPilarProfile);
        if (stub == null || profile == null || profile._prefab == null)
        {
            RLog.Error("[HouseBlueprint] Log post piece not found.");
            return false;
        }

        var (rotation, requiresPrevious) = (stub.transform.localRotation, RequiresPrevious(stub));
        Object.DestroyImmediate(stub.gameObject);
        AddElement(post, LogPilarProfile, new Vector3(0f, profile._prefab._size.z * 0.5f, 0f), rotation, LogItem, requiresPrevious);
        return true;
    }

    private static bool ProfilesExist(IEnumerable<ushort> ids)
    {
        foreach (var id in ids)
        {
            var profile = ElementProfileDatabase.GetProfile(id);
            if (profile == null || profile._prefab == null)
            {
                RLog.Error($"[HouseBlueprint] Building piece {id} not found in the game's piece database.");
                return false;
            }
        }

        return true;
    }

    private static bool RequiresPrevious(StructureElement element)
    {
        var ingredient = element.GetComponent<StructureCraftingNodeIngredient>();
        return ingredient != null && ingredient._requiresAllPreviousIngredients;
    }

    /// <summary>Adds one of the game's building pieces to a structure, as a ghost piece that needs the given item.</summary>
    private static void AddElement(Transform parent, ushort profileId, Vector3 localPosition, Quaternion localRotation,
                                   int itemId, bool requiresPrevious)
    {
        var piece = Object.Instantiate(ElementProfileDatabase.GetProfile(profileId)._prefab.gameObject, parent);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = localRotation;

        var ingredient = piece.AddComponent(Il2CppType.Of<StructureCraftingNodeIngredient>())
                              .Cast<StructureCraftingNodeIngredient>();
        ingredient._itemId = itemId;
        ingredient._requiresAllPreviousIngredients = requiresPrevious;

        foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
            if (renderer.GetComponent<StructureGhostSwapper>() == null)
                renderer.gameObject.AddComponent(Il2CppType.Of<StructureGhostSwapper>());
    }
}
