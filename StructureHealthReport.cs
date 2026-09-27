using Construction;
using Il2CppInterop.Runtime;
using RedLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlueprintPack;

/// <summary>
///     Research only: logs the health the game gives each built structure (for example stone walls versus log walls),
///     once per session, the first time the blueprint book is opened. Reads only.
/// </summary>
internal static class StructureHealthReport
{
    private const int MaxLines = 150;
    private static bool _done;

    public static void RunOnce()
    {
        if (_done)
            return;
        _done = true;

        try
        {
            var found = Object.FindObjectsOfType(Il2CppType.Of<Structure>());
            var lines = new List<string>();
            foreach (var o in found)
            {
                var structure = o.TryCast<Structure>();
                if (structure == null)
                    continue;

                if (!StructureDestructionManager.TryGetStructureHealth(structure, out var health, out var maxHealth))
                    continue;

                var elements = structure.GetComponentsInChildren<StructureElement>(true);
                var profiles = string.Join(",", elements.Where(e => e._profile != null)
                                                        .GroupBy(e => e._profile.name)
                                                        .Select(g => $"{g.Count()}x{g.Key}"));
                var p = structure.transform.position;
                lines.Add($"{structure.GetIl2CppType().Name} health {health:0.#}/{maxHealth:0.#} pieces [{profiles}] "
                          + $"at ({p.x:0.0},{p.y:0.0},{p.z:0.0})");
            }

            RLog.Msg($"[BlueprintPack] Structure health report: {lines.Count} structures with health");
            foreach (var line in lines.OrderBy(l => l).Take(MaxLines))
                RLog.Msg("[BlueprintPack]   " + line);
        }
        catch (Exception e)
        {
            RLog.Error($"[BlueprintPack] Structure health report failed: {e}");
        }
    }
}
