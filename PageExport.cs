using RedLoader;
using Sons.Weapon;
using SonsSdk;
using UnityEngine;

namespace BlueprintPack;

/// <summary>
///     Research only: saves a few of the blueprint book's page images as PNGs in Mods\BlueprintPack\page-export, as a
///     local reference for the layout of our own page. They are the game's artwork: keep them out of the repository.
/// </summary>
internal static class PageExport
{
    private static bool _done;

    public static void Run(string dataDir)
    {
        if (_done)
            return;
        _done = true;

        var held = ItemTools.GetHeldPrefab(ItemTools.Identifiers.BlueprintBook);
        var book = held == null ? null : held.GetComponent<BlueprintBookController>();
        if (book == null)
            return;

        var outDir = Path.Combine(dataDir, "page-export");
        Directory.CreateDirectory(outDir);
        var pages = book._pages._pages;
        var saved = new HashSet<string>();
        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            var title = page._pageTitleLocalizationId ?? "";
            var image = page._pageImage;
            // The shelter pages, one of each other category, and the notes background.
            if (image == null || !saved.Add(title == "BLUEPRINT_PAGE_SHELTER" ? image.name : title))
                continue;

            try
            {
                var file = Path.Combine(outDir, $"{i:00}_{title}_{image.name}.png");
                File.WriteAllBytes(file, ReadPixels(image));
            }
            catch (Exception e)
            {
                RLog.Error($"[BlueprintPack] Could not export page {i}: {e.Message}");
            }
        }

        RLog.Msg($"[BlueprintPack] Exported {saved.Count} page images to {outDir}");
    }

    /// <summary>Page textures are GPU-compressed and not readable, so draw them into a readable copy first.</summary>
    private static byte[] ReadPixels(Texture2D source)
    {
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
                                            RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            var png = ImageConversion.EncodeToPNG(copy);
            UnityEngine.Object.Destroy(copy);
            return png;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
