using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Svg.Skia;

namespace OpenSourceTree.Views;

/// <summary>
/// Loads the application's SVG icons from the Assets/Icons folder on disk
/// (plain files next to the executable — not embedded assembly resources).
/// The stroke color is overridden per theme; ThemeService publishes the results
/// as I.&lt;Name&gt; resources consumed via {DynamicResource}.
/// </summary>
public static class Icons
{
    /// <summary>Resource-key names (I.&lt;Name&gt;); file names are the lower-case variants.</summary>
    public static readonly string[] Names =
    {
        "Commit", "Pull", "Push", "Fetch", "Branch", "Merge", "Stash", "Discard", "Tag",
        "Terminal", "Explorer", "Settings", "Workspace", "Cloud", "GitFlow", "Submodule", "Plus"
    };

    private static readonly Dictionary<string, IImage> Cache = new();

    public static IImage Load(string name, string stroke)
    {
        string cacheKey = name + "|" + stroke;
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", name.ToLowerInvariant() + ".svg");
        var svgText = File.ReadAllText(path)
            .Replace("stroke=\"#9FB4C8\"", $"stroke=\"{stroke}\"", StringComparison.OrdinalIgnoreCase);
        var source = SvgSource.LoadFromSvg(svgText);
        var image = new SvgImage { Source = source };
        Cache[cacheKey] = image;
        return image;
    }
}
