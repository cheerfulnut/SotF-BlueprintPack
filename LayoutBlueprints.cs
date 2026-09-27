using System.Text.Json;
using Construction;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using RedLoader;
using RedLoader.Utils;
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
///     Blueprints designed in Blender: each Mods\HouseBlueprint\Blueprints\*.json layout (from blender/export_design.py
///     and blender/make_page.py) becomes a free-form blueprint with its own book page, built from the game's own pieces.
/// </summary>
internal static class LayoutBlueprints
{
    // Network prefab ids are the recipe id plus this, keeping them clear of the game's (below 1000).
    private const int BoltPrefabOffset = 10000;

    // Medium Cabin: a free-form shelter blueprint without an automatic foundation; supplies the ghost's frame.
    private const int FrameRecipeId = 30;
    private const string ShelterPageTitle = "BLUEPRINT_PAGE_SHELTER";

    private sealed class Layout
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Page { get; set; }
        public List<LayoutPart> Parts { get; set; }
    }

    private sealed class LayoutPart
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public float[] Position { get; set; }
        public float Yaw { get; set; }
    }

    private sealed class Built
    {
        public Layout Layout;
        public StructureRecipe Recipe;
        public Texture2D PageImage;
    }

    private static readonly Dictionary<int, Built> BuiltById = new();
    private static List<Layout> _layouts;
    private static GameObject _prefabHolder;

    private static string Folder => Path.Combine(LoaderEnvironment.ModsDirectory, "HouseBlueprint", "Blueprints");

    public static bool TryGetName(StructureRecipe recipe, out string name)
    {
        name = null;
        if (recipe == null || !BuiltById.TryGetValue(recipe._id, out var built))
            return false;
        name = built.Layout.Name;
        return true;
    }

    /// <summary>Builds every layout once and makes sure the recipe database contains them. Safe to call repeatedly.</summary>
    public static void EnsureRegistered(StructureRecipeDatabase db)
    {
        if (db == null)
            return;

        _layouts ??= LoadLayouts();
        foreach (var layout in _layouts)
        {
            try
            {
                if (!BuiltById.ContainsKey(layout.Id))
                {
                    var built = Build(db, layout);
                    if (built == null)
                        continue;
                    BuiltById[layout.Id] = built;
                }

                if (!db.TryGetRecipeById(layout.Id, out _))
                {
                    db._recipes.Add(BuiltById[layout.Id].Recipe);
                    RLog.Msg($"[HouseBlueprint] Registered recipe #{layout.Id} '{layout.Name}'.");
                }
            }
            catch (Exception e)
            {
                RLog.Error($"[HouseBlueprint] Blueprint '{layout.Name}' failed: {e}");
            }
        }
    }

    /// <summary>Adds each blueprint's page once, after the game's shelter pages.</summary>
    public static void EnsureBookPages()
    {
        var held = ItemTools.GetHeldPrefab(ItemTools.Identifiers.BlueprintBook);
        var book = held == null ? null : held.GetComponent<BlueprintBookController>();
        if (book == null)
            return;

        var pages = book._pages._pages;
        foreach (var built in BuiltById.Values.OrderBy(b => b.Layout.Id))
        {
            var insertAt = 0;
            var present = false;
            for (var i = 0; i < pages.Count; i++)
            {
                if (pages[i]._topRecipe == built.Recipe)
                    present = true;
                if (pages[i]._pageTitleLocalizationId == ShelterPageTitle)
                    insertAt = i + 1;
            }

            if (present)
                continue;
            pages.Insert(insertAt, new BlueprintBookPageData
            {
                _topRecipe = built.Recipe,
                _bottomRecipe = null,
                _pageTitleLocalizationId = ShelterPageTitle,
                _pageImage = built.PageImage
            });
            RLog.Msg($"[HouseBlueprint] Added book page {insertAt} for '{built.Layout.Name}'.");
        }
    }

    private static List<Layout> LoadLayouts()
    {
        var layouts = new List<Layout>();
        if (!Directory.Exists(Folder))
            return layouts;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in Directory.GetFiles(Folder, "*.json").OrderBy(f => f))
        {
            try
            {
                var layout = JsonSerializer.Deserialize<Layout>(File.ReadAllText(file), options);
                if (layout?.Parts == null || layout.Parts.Count == 0 || layout.Id <= 0)
                {
                    RLog.Error($"[HouseBlueprint] {Path.GetFileName(file)} has no id or no parts; skipped.");
                    continue;
                }

                layouts.Add(layout);
            }
            catch (Exception e)
            {
                RLog.Error($"[HouseBlueprint] Could not read {Path.GetFileName(file)}: {e.Message}");
            }
        }

        RLog.Msg($"[HouseBlueprint] Found {layouts.Count} blueprint layout(s) in {Folder}.");
        return layouts;
    }

    private static Built Build(StructureRecipeDatabase db, Layout layout)
    {
        if (!db.TryGetRecipeById(FrameRecipeId, out var frame))
        {
            RLog.Error($"[HouseBlueprint] Frame recipe #{FrameRecipeId} not found.");
            return null;
        }

        if (_prefabHolder == null)
        {
            // Clones made under an inactive parent never run their start-up code, but clones the game makes of them do.
            _prefabHolder = new GameObject("HouseBlueprint.LayoutPrefabs");
            _prefabHolder.SetActive(false);
            _prefabHolder.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(_prefabHolder);
        }

        var node = Object.Instantiate(frame._structureNodePrefab, _prefabHolder.transform);
        node.name = $"HouseBlueprint{layout.Id}StructureNode";
        var ghost = node.GetComponentInChildren<FreeFormGhostPrefab>(true).transform;
        for (var i = ghost.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(ghost.GetChild(i).gameObject);

        foreach (var part in layout.Parts)
        {
            var before = CountIngredients(ghost);
            var position = new Vector3(part.Position[0], part.Position[1], part.Position[2]);
            if (!PartLibrary.Place(db, part.Type, ghost, position, part.Yaw))
            {
                RLog.Error($"[HouseBlueprint] '{layout.Name}': part '{part.Name}' ({part.Type}) could not be made.");
                continue;
            }

            var added = CountIngredients(ghost).Select(kv => $"{kv.Value - before.GetValueOrDefault(kv.Key)}x{kv.Key}")
                                               .Where(s => !s.StartsWith("0x"));
            RLog.Msg($"[HouseBlueprint]   {part.Name} ({part.Type}): {string.Join(", ", added)}");
        }

        var craftingNode = node.GetComponent<StructureCraftingNode>();
        var totals = RebuildIngredientLinks(craftingNode, ghost);
        FitPlacementBox(node, ghost);

        var entity = node.GetComponent<BoltEntity>();
        entity._prefabId = layout.Id + BoltPrefabOffset;
        EntityManager.RegisterPrefab(entity);

        var recipe = Object.Instantiate(frame);
        recipe.name = $"HouseBlueprint{layout.Id}Recipe";
        recipe.hideFlags = HideFlags.HideAndDontSave;
        recipe._id = layout.Id;
        recipe._boltPrefabId = layout.Id + BoltPrefabOffset;
        recipe._displayName = layout.Name;
        recipe._category = StructureRecipe.CategoryType.Shelters;
        recipe._isDiscovered = true;
        recipe._isDiscoveredInCreative = true;
        recipe._structureNodePrefab = node;
        recipe._ingredients = new Il2CppSystem.Collections.Generic.List<StructureCraftingRecipeIngredient>();
        foreach (var (item, count) in totals)
            recipe._ingredients.Add(new StructureCraftingRecipeIngredient { ItemId = item, Count = count });
        craftingNode._recipe = recipe;

        try
        {
            CustomBlueprintManager.InitRecipeLocalization(recipe);
        }
        catch (Exception e)
        {
            RLog.Warning($"[HouseBlueprint] Could not add book text for '{layout.Name}': {e.Message}");
        }

        RLog.Msg($"[HouseBlueprint] Built '{layout.Name}' (#{layout.Id}): "
                 + string.Join(", ", totals.Select(t => $"{t.Count}x item {t.Item}")));
        return new Built { Layout = layout, Recipe = recipe, PageImage = LoadPageImage(layout) };
    }

    private static Dictionary<int, int> CountIngredients(Transform ghost)
    {
        var counts = new Dictionary<int, int>();
        foreach (var ingredient in ghost.GetComponentsInChildren<StructureCraftingNodeIngredient>(true))
            counts[ingredient._itemId] = counts.GetValueOrDefault(ingredient._itemId) + 1;
        return counts;
    }

    /// <summary>One ingredient link per item, pieces ordered bottom first, links ordered by their lowest piece.</summary>
    private static List<(int Item, int Count)> RebuildIngredientLinks(StructureCraftingNode craftingNode, Transform ghost)
    {
        var groups = ghost.GetComponentsInChildren<StructureCraftingNodeIngredient>(true)
                          .GroupBy(i => i._itemId)
                          .Select(g => g.OrderBy(i => i.transform.position.y).ToList())
                          .OrderBy(g => g[0].transform.position.y)
                          .ToList();

        craftingNode._craftingIngredientLinks.Clear();
        var totals = new List<(int, int)>();
        foreach (var group in groups)
        {
            var list = new Il2CppSystem.Collections.Generic.List<StructureCraftingNodeIngredient>();
            foreach (var ingredient in group)
                list.Add(ingredient);
            var item = group[0]._itemId;
            craftingNode._craftingIngredientLinks.Add(new StructureCraftingNode.CraftingIngredientLink
            {
                Ingredient = new StructureCraftingRecipeIngredient { ItemId = item, Count = group.Count },
                _ingredients = list
            });
            totals.Add((item, group.Count));
        }

        return totals;
    }

    /// <summary>The frame's box colliders cover a whole cabin; fit them to our pieces so placement checks match.</summary>
    private static void FitPlacementBox(GameObject node, Transform ghost)
    {
        var root = node.transform;
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var element in ghost.GetComponentsInChildren<StructureElement>(true))
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

    private static Texture2D LoadPageImage(Layout layout)
    {
        var path = Path.Combine(Folder, layout.Page ?? "");
        if (string.IsNullOrEmpty(layout.Page) || !File.Exists(path))
        {
            RLog.Warning($"[HouseBlueprint] No page image for '{layout.Name}' ({layout.Page}).");
            return null;
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!ImageConversion.LoadImage(texture, (Il2CppStructArray<byte>)File.ReadAllBytes(path)))
        {
            RLog.Warning($"[HouseBlueprint] Could not read page image {layout.Page}.");
            return null;
        }

        texture.name = $"HouseBlueprint{layout.Id}Page";
        texture.hideFlags = HideFlags.HideAndDontSave;
        return texture;
    }
}
