using Construction;
using Il2CppInterop.Runtime;
using RedLoader;
using SonsSdk;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseBlueprint;

/// <summary>
///     The hb_remove console command (F1): removes a finished building, for clearing test builds.
///       hb_remove            the building you are looking at: the piece you aim at and every structure touching it,
///                            and everything touching those, and so on (the game's own building grouping does not
///                            join up the pieces of a tower finished from a blueprint)
///       hb_remove 15         every structure within 15 m of you
///       hb_remove confirm    remove what the last hb_remove found (within 30 seconds)
///       hb_remove cancel     forget it
///     It only ever reports first; nothing is removed without "confirm". Structures are removed top first through the
///     game's structure database (RuntimeStructureDatabase.DestroyStructure), not deleted directly. (The game's
///     StructureDestructionManager.DestroyStructureDebug reported success without removing anything.)
///     Unfinished blueprint ghosts are not structures; RedLoader's cancelblueprints removes those.
/// </summary>
internal static class StructureRemover
{
    private const float LookDistance = 30f;
    private const float TouchDistance = 0.35f;  // gap between two structures' pieces that still counts as touching
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

        var removed = 0;
        var failed = 0;
        // Top first, so nothing is left without support and collapses on the way.
        foreach (var structure in _pending.Where(s => s != null).OrderByDescending(s => s.transform.position.y).ToList())
        {
            if (structure == null)
                continue; // already gone with something else
            try
            {
                RuntimeStructureDatabase.DestroyStructure(structure);
                removed++;
            }
            catch (Exception e)
            {
                failed++;
                RLog.Error($"[HouseBlueprint] hb_remove could not remove {structure.name}: {e.Message}");
            }
        }

        _pending = null;
        RLog.Msg($"[HouseBlueprint] hb_remove removed {removed} structures, {failed} failed.");
        Report(failed == 0 ? $"hb_remove: asked the game to remove {removed} structures." : $"hb_remove: removed {removed}, {failed} failed (see the log).");
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

        // Everything touching the target, then everything touching those, and so on.
        var all = AllStructures();
        var boxes = all.ToDictionary(st => st.Pointer, BoxOf);
        var building = new List<Structure> { target };
        var seen = new HashSet<IntPtr> { target.Pointer };
        if (!boxes.ContainsKey(target.Pointer))
            boxes[target.Pointer] = BoxOf(target);
        for (var i = 0; i < building.Count; i++)
        {
            var box = boxes[building[i].Pointer];
            foreach (var other in all)
            {
                if (!seen.Contains(other.Pointer) && box.Intersects(boxes[other.Pointer]))
                {
                    seen.Add(other.Pointer);
                    building.Add(other);
                }
            }
        }

        return building;
    }

    /// <summary>A box around a structure's pieces, grown by the touching distance.</summary>
    private static Bounds BoxOf(Structure structure)
    {
        var box = new Bounds(structure.transform.position, Vector3.zero);
        foreach (var element in structure.GetComponentsInChildren<StructureElement>(true))
        {
            var half = element._size * 0.5f;
            for (var corner = 0; corner < 8; corner++)
                box.Encapsulate(element.transform.TransformPoint(new Vector3((corner & 1) == 0 ? -half.x : half.x,
                                                                             (corner & 2) == 0 ? -half.y : half.y,
                                                                             (corner & 4) == 0 ? -half.z : half.z)));
        }

        box.Expand(TouchDistance * 2f);
        return box;
    }

    private static void Report(string text, float seconds = 4f)
    {
        SonsTools.ShowMessage(text, seconds);
        RLog.Msg("[HouseBlueprint] " + text);
    }
}
