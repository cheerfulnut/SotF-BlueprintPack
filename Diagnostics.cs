using System.Reflection;
using System.Text;
using Construction;
using Il2CppInterop.Runtime;
using RedLoader;
using Sons.Crafting;
using Sons.Crafting.Structures;
using Sons.Weapon;
using SonsSdk;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlueprintPack;

/// <summary>
///     Research only: writes the game's blueprint recipes, blueprint book pages, building-piece types and the layout of
///     free-form and shelter blueprints to Mods\BlueprintPack\diagnostics.txt. It only reads game data.
/// </summary>
internal static class Diagnostics
{
    private const int MaxLinesPerPrefab = 600;

    private static bool _done;

    private static readonly Dictionary<int, string> ItemNames = typeof(ItemTools.Identifiers)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(int))
        .GroupBy(f => (int)f.GetRawConstantValue())
        .ToDictionary(g => g.Key, g => string.Join("/", g.Select(f => f.Name)));

    public static void Run(string dataDir)
    {
        // OnGameStart runs on every save load; one dump per session is enough.
        if (_done)
            return;
        _done = true;

        var sb = new StringBuilder();
        sb.AppendLine($"BlueprintPack diagnostics {DateTime.Now:yyyy-MM-dd HH:mm:ss}, game {Sons.GameApplication.Version.GetVersionString()}");
        Section(sb, "Recipes", DumpRecipes);
        Section(sb, "Blueprint book pages", DumpBookPages);
        Section(sb, "Element profiles (building pieces)", DumpProfiles);
        Section(sb, "Free-form and shelter blueprint layouts", DumpLayouts);

        Directory.CreateDirectory(dataDir);
        var path = Path.Combine(dataDir, "diagnostics.txt");
        File.WriteAllText(path, sb.ToString());
        RLog.Msg($"[BlueprintPack] Diagnostics written to {path}");
    }

    private static void Section(StringBuilder sb, string title, Action<StringBuilder> body)
    {
        sb.AppendLine().AppendLine($"===== {title} =====");
        try
        {
            body(sb);
        }
        catch (Exception e)
        {
            sb.AppendLine($"!! section failed: {e}");
            RLog.Error($"[BlueprintPack] Diagnostics section '{title}' failed: {e}");
        }
    }

    private static List<StructureRecipe> GetRecipes()
    {
        var list = new List<StructureRecipe>();
        if (!StructureCraftingSystem.TryGetInstance(out var system))
            return list;

        foreach (var recipe in system._recipeDatabase._recipes)
            list.Add(recipe);
        return list;
    }

    private static void DumpRecipes(StringBuilder sb)
    {
        var recipes = GetRecipes();
        sb.AppendLine($"{recipes.Count} recipes");
        foreach (var r in recipes.OrderBy(r => r._category).ThenBy(r => r._id))
        {
            sb.AppendLine($"#{r._id} '{r.name}' display='{r._displayName}' loc='{r._localizationId}' category={r._category} "
                          + $"page={r._recipePageId} freeForm={r._useFreeFormStructures} hybrid={r._hybridFreeFormAndScrew} "
                          + $"autoFoundation={r._autoFoundation} usesBuilt={r.UsesBuiltPrefab} place={r._placeMode} anchor={r._anchor} "
                          + $"bolt={r._boltPrefabId} discovered={r._isDiscovered}");
            sb.AppendLine($"    node={Name(r._structureNodePrefab)} built={Name(r._builtPrefab)} image={Name(r._recipeImage)} "
                          + $"ingredients={Ingredients(r)}");
        }
    }

    private static void DumpBookPages(StringBuilder sb)
    {
        var held = ItemTools.GetHeldPrefab(ItemTools.Identifiers.BlueprintBook);
        var controller = held == null ? null : held.GetComponent<BlueprintBookController>();
        if (controller == null)
        {
            sb.AppendLine("blueprint book controller not found");
            return;
        }

        var pages = controller._pages._pages;
        sb.AppendLine($"{pages.Count} pages");
        for (var i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            var img = p._pageImage;
            var imgInfo = img == null ? "-" : $"{img.name} {img.width}x{img.height} {img.format}";
            sb.AppendLine($"page {i}: title='{p._pageTitleLocalizationId}' top={RecipeLabel(p._topRecipe)} "
                          + $"bottom={RecipeLabel(p._bottomRecipe)} image={imgInfo}");
        }
    }

    private static void DumpProfiles(StringBuilder sb)
    {
        var found = Resources.FindObjectsOfTypeAll(Il2CppType.Of<ElementProfile>());
        var profiles = new List<ElementProfile>();
        foreach (var o in found)
        {
            var p = o.TryCast<ElementProfile>();
            if (p != null)
                profiles.Add(p);
        }

        sb.AppendLine($"{profiles.Count} profiles");
        foreach (var p in profiles.OrderBy(p => p._id))
        {
            var item = p._item;
            var itemInfo = item == null ? "-" : $"{item._id}({ItemName(item._id)})";
            var prefab = p._prefab;
            var size = prefab == null ? "-" : V(prefab._size);
            var mesh = "-";
            if (prefab != null)
            {
                var mf = prefab.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null)
                    mesh = $"{V(mf.sharedMesh.bounds.size)} scale={V(mf.transform.lossyScale)}";
            }

            var cuts = new List<string>();
            if (p._quarterCutProfiles != null)
                foreach (var q in p._quarterCutProfiles)
                    cuts.Add(q == null ? "-" : q._id.ToString());

            sb.AppendLine($"{p._id} '{p.name}' item={itemInfo} prefab={Name(prefab)} size={size} mesh={mesh} "
                          + $"hp+={p._additionalHp} reinforced={p._isReinforced} rock={p._isRockStructure} window={p._isWindow} "
                          + $"solidGround={p._isSolidGround} ghost={ProfileId(p._ghostProfile)} nonGhost={ProfileId(p._nonGhostProfile)} "
                          + $"lengthSplit={ProfileId(p._lengthSplitProfile)} quarterCuts=[{string.Join(",", cuts)}] "
                          + $"disassembleYield={p._disassembleItemYield} flags={p._profileFlags}");
        }
    }

    private static void DumpLayouts(StringBuilder sb)
    {
        foreach (var r in GetRecipes().OrderBy(r => r._id))
        {
            if (!r._useFreeFormStructures && !r._hybridFreeFormAndScrew && r._category != StructureRecipe.CategoryType.Shelters)
                continue;

            sb.AppendLine().AppendLine($"#{r._id} '{r.name}' freeForm={r._useFreeFormStructures} hybrid={r._hybridFreeFormAndScrew} category={r._category}");
            DumpHierarchy(sb, r._structureNodePrefab == null ? null : r._structureNodePrefab.transform, "node prefab");
            DumpHierarchy(sb, r._builtPrefab == null ? null : r._builtPrefab.transform, "built prefab");
        }
    }

    private static void DumpHierarchy(StringBuilder sb, Transform root, string label)
    {
        sb.AppendLine($"  -- {label}: {(root == null ? "(none)" : root.name)}");
        if (root == null)
            return;

        var lines = 0;
        Walk(root, 0);
        if (lines > MaxLinesPerPrefab)
            sb.AppendLine($"     ... truncated after {MaxLinesPerPrefab} lines");

        void Walk(Transform t, int depth)
        {
            if (++lines > MaxLinesPerPrefab)
                return;

            var names = new List<string>();
            var extra = new StringBuilder();
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null)
                    continue;

                var typeName = c.GetIl2CppType().Name;
                if (typeName != "Transform")
                    names.Add(typeName);

                var element = c.TryCast<StructureElement>();
                if (element != null)
                    extra.Append($" element(profile={ProfileId(element._profile)} '{Name(element._profile)}' size={V(element._size)})");

                var preset = c.TryCast<PresetElementsTemplate>();
                if (preset != null)
                    extra.Append($" preset(fill={preset._fillType} count={preset.ElementCount})");

                var ingredient = c.TryCast<StructureCraftingNodeIngredient>();
                if (ingredient != null)
                    extra.Append($" ingredient(item={ingredient._itemId}({ItemName(ingredient._itemId)}))");
            }

            sb.AppendLine($"{new string(' ', 5 + depth * 2)}{t.name} pos={V(t.localPosition)} rot={V(t.localEulerAngles)} "
                          + $"scale={V(t.localScale)} [{string.Join(",", names)}]{extra}");

            for (var i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1);
        }
    }

    private static string Ingredients(StructureRecipe r)
    {
        if (r._ingredients == null)
            return "-";

        var parts = new List<string>();
        foreach (var ing in r._ingredients)
            parts.Add($"{ing.Count}x{ing.ItemId}({ItemName(ing.ItemId)})");
        return string.Join(", ", parts);
    }

    private static string RecipeLabel(StructureRecipe r) => r == null ? "-" : $"#{r._id}'{r.name}'";

    private static string ProfileId(ElementProfile p) => p == null ? "-" : p._id.ToString();

    private static string ItemName(int id) => ItemNames.TryGetValue(id, out var n) ? n : "?";

    private static string Name(Object o) => o == null ? "-" : o.name;

    private static string V(Vector3 v) => $"({v.x:0.###},{v.y:0.###},{v.z:0.###})";
}
