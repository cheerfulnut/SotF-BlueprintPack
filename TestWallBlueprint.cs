using Construction;
using RedLoader;
using Sons.Crafting;
using Sons.Crafting.Structures;
using Sons.Weapon;
using SonsSdk;
using SonsSdk.Building;
using SonsSdk.Networking;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseBlueprint;

/// <summary>
///     Proof of concept, step 1: a blueprint for a single log wall, made by copying one plain log wall out of the game's
///     own Medium Cabin blueprint. It is a free-form blueprint like the game's shelters, so the finished wall is made of
///     ordinary game logs.
/// </summary>
internal static class TestWallBlueprint
{
    // Game recipe ids are below 200 and its network prefab ids below 1000, so these do not collide with the game.
    private const int RecipeId = 9001;
    private const int BoltPrefabId = 9000;

    // Medium Cabin: a free-form shelter blueprint without an automatic foundation, so the simplest template.
    private const int TemplateRecipeId = 30;
    private const ushort LogProfileId = 5;
    private const int LogItemId = 78;

    private const string DisplayName = "Test Log Wall";
    private const string ShelterPageTitle = "BLUEPRINT_PAGE_SHELTER";
    private const string NotesPageTitle = "BLUEPRINT_PAGE_NOTES";

    private static GameObject _prefabHolder;
    private static StructureRecipe _recipe;

    /// <summary>
    ///     Creates the recipe and its ghost once, and makes sure the given recipe database contains it. Safe to call
    ///     repeatedly; it must run before the game restores saved ghosts, which look recipes up by id.
    /// </summary>
    public static void EnsureRegistered(StructureRecipeDatabase database)
    {
        if (database == null)
            return;

        if (_recipe == null)
        {
            if (!database.TryGetRecipeById(TemplateRecipeId, out var template))
            {
                RLog.Error($"[HouseBlueprint] Template recipe #{TemplateRecipeId} not found; test wall not added.");
                return;
            }

            _recipe = CreateRecipe(template);
            if (_recipe == null)
                return;
        }

        if (!database.TryGetRecipeById(RecipeId, out _))
        {
            database._recipes.Add(_recipe);
            RLog.Msg($"[HouseBlueprint] Registered recipe #{RecipeId} '{DisplayName}'.");
        }
    }

    /// <summary>Adds the book page once, after the game's shelter pages.</summary>
    public static void EnsureBookPage()
    {
        if (_recipe == null)
            return;

        var held = ItemTools.GetHeldPrefab(ItemTools.Identifiers.BlueprintBook);
        var book = held == null ? null : held.GetComponent<BlueprintBookController>();
        if (book == null)
        {
            RLog.Error("[HouseBlueprint] Blueprint book not found; page not added.");
            return;
        }

        var pages = book._pages._pages;
        var insertAt = 0;
        Texture2D image = null;
        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            if (page._topRecipe == _recipe || page._bottomRecipe == _recipe)
                return; // already added in an earlier session

            if (page._pageTitleLocalizationId == ShelterPageTitle)
                insertAt = i + 1;
            if (image == null && page._pageTitleLocalizationId == NotesPageTitle)
                image = page._pageImage; // a plain notes page, until we have our own drawing
        }

        pages.Insert(insertAt, new BlueprintBookPageData
        {
            _topRecipe = _recipe,
            _bottomRecipe = null,
            _pageTitleLocalizationId = ShelterPageTitle,
            _pageImage = image
        });
        RLog.Msg($"[HouseBlueprint] Added book page {insertAt} for '{DisplayName}'.");
    }

    private static StructureRecipe CreateRecipe(StructureRecipe template)
    {
        // Clones made under an inactive parent never run their start-up code, but clones the game makes of them do.
        _prefabHolder = new GameObject("HouseBlueprint.Prefabs");
        _prefabHolder.SetActive(false);
        _prefabHolder.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(_prefabHolder);

        var node = Object.Instantiate(template._structureNodePrefab, _prefabHolder.transform);
        node.name = "HouseBlueprintTestWallStructureNode";

        var ghost = node.GetComponentInChildren<FreeFormGhostPrefab>(true);
        if (ghost == null)
        {
            RLog.Error("[HouseBlueprint] Template has no free-form ghost; test wall not added.");
            return null;
        }

        var wall = KeepOnePlainLogWall(ghost.transform);
        if (wall == null)
        {
            RLog.Error("[HouseBlueprint] No plain log wall found in the template; test wall not added.");
            return null;
        }

        // Stand the wall on the ground at the blueprint's origin: its bottom log's centre is half a log above the ground.
        wall.localPosition = new Vector3(0f, 0.225f, 0f);
        FitPlacementBox(node, wall);

        var craftingNode = node.GetComponent<StructureCraftingNode>();
        var ingredientCount = RebuildIngredientLinks(craftingNode, wall);

        var entity = node.GetComponent<BoltEntity>();
        entity._prefabId = BoltPrefabId;
        EntityManager.RegisterPrefab(entity);

        var recipe = Object.Instantiate(template);
        recipe.name = "HouseBlueprintTestWallRecipe";
        recipe.hideFlags = HideFlags.HideAndDontSave;
        recipe._id = RecipeId;
        recipe._boltPrefabId = BoltPrefabId;
        recipe._displayName = DisplayName;
        recipe._category = StructureRecipe.CategoryType.Shelters;
        recipe._isDiscovered = true;
        recipe._isDiscoveredInCreative = true;
        recipe._structureNodePrefab = node;
        recipe._ingredients = new Il2CppSystem.Collections.Generic.List<StructureCraftingRecipeIngredient>();
        recipe._ingredients.Add(new StructureCraftingRecipeIngredient { ItemId = LogItemId, Count = ingredientCount });
        craftingNode._recipe = recipe;

        try
        {
            CustomBlueprintManager.InitRecipeLocalization(recipe);
        }
        catch (Exception e)
        {
            RLog.Warning($"[HouseBlueprint] Could not add the book text for '{DisplayName}': {e.Message}");
        }

        RLog.Msg($"[HouseBlueprint] Created '{DisplayName}' from '{template.name}': {ingredientCount} logs.");
        return recipe;
    }

    /// <summary>Keeps the first wall made only of full logs, removes every other part of the template's ghost.</summary>
    private static Transform KeepOnePlainLogWall(Transform ghostRoot)
    {
        Transform keep = null;
        foreach (var wall in ghostRoot.GetComponentsInChildren<WallStructure>(true))
        {
            var elements = wall.GetComponentsInChildren<StructureElement>(true);
            if (elements.Length > 0 && elements.All(e => e._profile != null && e._profile._id == LogProfileId))
            {
                keep = wall.transform;
                break;
            }
        }

        if (keep == null)
            return null;

        for (var i = ghostRoot.childCount - 1; i >= 0; i--)
        {
            var child = ghostRoot.GetChild(i);
            if (child != keep)
                Object.DestroyImmediate(child.gameObject);
        }

        return keep;
    }

    /// <summary>The ghost's ingredient list still describes the whole cabin; rebuild it from the logs we kept.</summary>
    private static int RebuildIngredientLinks(StructureCraftingNode craftingNode, Transform wall)
    {
        var ingredients = new Il2CppSystem.Collections.Generic.List<StructureCraftingNodeIngredient>();
        foreach (var ingredient in wall.GetComponentsInChildren<StructureCraftingNodeIngredient>(true))
            ingredients.Add(ingredient);

        craftingNode._craftingIngredientLinks.Clear();
        craftingNode._craftingIngredientLinks.Add(new StructureCraftingNode.CraftingIngredientLink
        {
            Ingredient = new StructureCraftingRecipeIngredient { ItemId = LogItemId, Count = ingredients.Count },
            _ingredients = ingredients
        });
        return ingredients.Count;
    }

    /// <summary>The template's box colliders cover the whole cabin; shrink them to the wall so placement checks fit it.</summary>
    private static void FitPlacementBox(GameObject node, Transform wall)
    {
        var root = node.transform;
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var element in wall.GetComponentsInChildren<StructureElement>(true))
        {
            var half = element._size * 0.5f;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = new Vector3((corner & 1) == 0 ? -half.x : half.x,
                                        (corner & 2) == 0 ? -half.y : half.y,
                                        (corner & 4) == 0 ? -half.z : half.z);
                var p = root.InverseTransformPoint(element.transform.TransformPoint(local));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        foreach (var box in node.GetComponents<BoxCollider>())
        {
            box.center = (min + max) * 0.5f;
            box.size = max - min;
        }
    }
}
