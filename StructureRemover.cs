using Construction;
using Il2CppInterop.Runtime;
using RedLoader;
using SonsSdk;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseBlueprint;

/// <summary>
///     The hb_remove console command (F1): removes a finished building, for clearing test builds.
///       hb_remove            the building you are looking at: every structure the game counts as connected to the
///                            piece you aim at, plus anything standing within that building's bounds (posts, ropes)
///       hb_remove 15         every structure within 15 m of you
///       hb_remove confirm    remove what the last hb_remove found (within 30 seconds)
///       hb_remove cancel     forget it
///     It only ever reports first; nothing is removed without "confirm". Structures are removed top first through the
///     game's own debug destroy (StructureDestructionManager.DestroyStructureDebug), not deleted directly.
///     Unfinished blueprint ghosts are not structures; RedLoader's cancelblueprints removes those.
/// </summary>
internal static class StructureRemover
{
    private const float LookDistance = 30f;
    private const float BoundsMargin = 1f;
    private const float ConfirmSeconds = 30f;

    private static List<Structure> _pending;
    private static float _pendingSince;

    public static void Command(string args)
    {
        args = (args ?? "").Trim().ToLowerInvariant();
        try
        {
            if (args == "confirm")
                Confirm();
            else if (args == "cancel")
            {
                _pending = null;
                Report("hb_remove: cancelled.");
            }
            else if (float.TryParse(args, out var radius) && radius > 0)
                Preview(InRadius(radius), $"within {radius:0.#} m of you");
            else if (args.Length == 0)
                Preview(LookedAtBuilding(), "in the building you are looking at");
            else
                Report("hb_remove: use 'hb_remove' (look at a building), 'hb_remove <metres>', 'hb_remove confirm' or 'hb_remove cancel'.");
        }
        catch (Exception e)
        {
            RLog.Error($"[HouseBlueprint] hb_remove failed: {e}");
            Report("hb_remove failed; see the log.");
        }
    }

    private static void Preview(List<Structure> found, string where)
    {
        if (found == null || found.Count == 0)
        {
            _pending = null;
            Report($"hb_remove: no finished structures {where}.");
            return;
        }

        _pending = found;
        _pendingSince = Time.realtimeSinceStartup;
        var summary = string.Join(", ", found.GroupBy(s => s.GetIl2CppType().Name.Replace("Structure", ""))
                                             .OrderByDescending(g => g.Count())
                                             .Select(g => $"{g.Count()} {g.Key}"));
        RLog.Msg($"[HouseBlueprint] hb_remove found {found.Count} structures {where}: {summary}");
        Report($"hb_remove: {found.Count} structures {where} ({summary}). Type 'hb_remove confirm' within "
               + $"{ConfirmSeconds:0} s to remove them.", 8f);
    }

    private static void Confirm()
    {
        if (_pending == null || Time.realtimeSinceStartup - _pendingSince > ConfirmSeconds)
        {
            _pending = null;
            Report("hb_remove: nothing to confirm; run hb_remove first.");
            return;
        }

        if (!StructureDestructionManager.TryGetInstance(out var manager))
        {
            Report("hb_remove: the game's structure destruction system is not available.");
            return;
        }

        var removed = 0;
        var failed = 0;
        // Top first, so nothing is left without support and collapses on the way.
        foreach (var structure in _pending.Where(s => s != null).OrderByDescending(s => s.transform.position.y).ToList())
        {
            if (structure == null)
                continue; // already gone with something else
            var name = $"HBRemove_{removed + failed}_{structure.GetInstanceID()}";
            structure.gameObject.name = name; // the game's debug destroy finds structures by name
            if (manager.DestroyStructureDebug(name))
                removed++;
            else
                failed++;
        }

        _pending = null;
        RLog.Msg($"[HouseBlueprint] hb_remove removed {removed} structures, {failed} failed.");
        Report(failed == 0 ? $"hb_remove: removed {removed} structures." : $"hb_remove: removed {removed}, {failed} failed (see the log).");
    }

    private static List<Structure> AllStructures()
    {
        var list = new List<Structure>();
        foreach (var o in Object.FindObjectsOfType(Il2CppType.Of<Structure>()))
        {
            var s = o.TryCast<Structure>();
            if (s != null && s.gameObject.activeInHierarchy)
                list.Add(s);
        }

        return list;
    }

    private static List<Structure> InRadius(float radius)
    {
        var centre = PlayerPosition();
        return AllStructures().Where(s => Vector3.Distance(s.transform.position, centre) <= radius).ToList();
    }

    private static Vector3 PlayerPosition()
    {
        var cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    /// <summary>The structure hit by a ray from the camera, then everything in the same building.</summary>
    private static List<Structure> LookedAtBuilding()
    {
        var cam = Camera.main;
        if (cam == null)
            return null;

        Structure target = null;
        var hits = Physics.RaycastAll(new Ray(cam.transform.position, cam.transform.forward), LookDistance);
        foreach (var hit in hits.OrderBy(h => h.distance))
        {
            target = hit.collider == null ? null : hit.collider.GetComponentInParent<Structure>();
            if (target != null)
                break;
        }

        if (target == null)
            return null;

        var all = AllStructures();
        var building = new List<Structure> { target };
        var super = target.SuperStructure;
        if (super != null)
            building = all.Where(s => s.SuperStructure != null && s.SuperStructure.Pointer == super.Pointer).ToList();

        // Add anything else standing within the building's bounds, such as posts or ropes the game keeps separate.
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var element in building.SelectMany(s => s.GetComponentsInChildren<StructureElement>(true)))
        {
            min = Vector3.Min(min, element.transform.position);
            max = Vector3.Max(max, element.transform.position);
        }

        var margin = new Vector3(BoundsMargin, BoundsMargin, BoundsMargin);
        var bounds = new Bounds();
        bounds.SetMinMax(min - margin, max + margin);
        var inBuilding = new HashSet<IntPtr>(building.Select(s => s.Pointer));
        building.AddRange(all.Where(s => !inBuilding.Contains(s.Pointer) && bounds.Contains(s.transform.position)));
        return building;
    }

    private static void Report(string text, float seconds = 4f)
    {
        SonsTools.ShowMessage(text, seconds);
        RLog.Msg("[HouseBlueprint] " + text);
    }
}
