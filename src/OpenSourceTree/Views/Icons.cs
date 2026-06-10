using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace OpenSourceTree.Views;

/// <summary>
/// Loads the application's SVG icons from the Assets/Icons folder on disk
/// (plain files next to the executable — not embedded assembly resources).
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, IImage> Cache = new();

    private static IImage Get(string name)
    {
        if (Cache.TryGetValue(name, out var cached))
            return cached;

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", name + ".svg");
        var source = SvgSource.Load(path);
        var image = new SvgImage { Source = source };
        Cache[name] = image;
        return image;
    }

    public static IImage Commit => Get("commit");
    public static IImage Pull => Get("pull");
    public static IImage Push => Get("push");
    public static IImage Fetch => Get("fetch");
    public static IImage Branch => Get("branch");
    public static IImage Merge => Get("merge");
    public static IImage Stash => Get("stash");
    public static IImage Discard => Get("discard");
    public static IImage Tag => Get("tag");
    public static IImage Terminal => Get("terminal");
    public static IImage Explorer => Get("explorer");
    public static IImage Settings => Get("settings");
    public static IImage Workspace => Get("workspace");
    public static IImage Cloud => Get("cloud");
    public static IImage GitFlow => Get("gitflow");
    public static IImage Submodule => Get("submodule");
}
