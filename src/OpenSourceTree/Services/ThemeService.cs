using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace OpenSourceTree.Services;

/// <summary>
/// Theme-dependent brushes used by view models (diff lines etc.). Views are recolored
/// instantly via DynamicResource; items built from these fields pick the new palette on
/// the next refresh.
/// </summary>
public static class ThemePalette
{
    public static IBrush DiffAddedBg = null!;
    public static IBrush DiffRemovedBg = null!;
    public static IBrush DiffHunkBg = null!;
    public static IBrush DiffHeaderBg = null!;
    public static IBrush DiffAddedFg = null!;
    public static IBrush DiffRemovedFg = null!;
    public static IBrush DiffHunkFg = null!;
    public static IBrush DiffNormalFg = null!;
    public static IBrush DiffDimFg = null!;

    internal static void Apply(bool dark)
    {
        static IBrush B(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));
        DiffAddedBg = B(dark ? "#1A3322" : "#DDF4E4");
        DiffRemovedBg = B(dark ? "#3A2022" : "#FBE5E8");
        DiffHunkBg = B(dark ? "#1E3245" : "#DEEBF7");
        DiffHeaderBg = B(dark ? "#1B2530" : "#EEF2F6");
        DiffAddedFg = B(dark ? "#7FD49B" : "#1A7F37");
        DiffRemovedFg = B(dark ? "#E89A9A" : "#C0392B");
        DiffHunkFg = B(dark ? "#6FA8DC" : "#2E74B5");
        DiffNormalFg = B(dark ? "#C9D4DF" : "#24313F");
        DiffDimFg = B(dark ? "#6E8295" : "#8095A8");
    }
}

/// <summary>
/// Publishes the color palette (B.*) and the tinted icon set (I.*) into
/// Application.Resources so every view restyles live via DynamicResource.
/// </summary>
public static class ThemeService
{
    public static string Current { get; private set; } = "Dark";

    public static void Apply(string theme)
    {
        bool dark = theme != "Light";
        Current = dark ? "Dark" : "Light";

        var app = Application.Current!;
        app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var res = app.Resources;

        void B(string key, string darkHex, string lightHex) =>
            res["B." + key] = new ImmutableSolidColorBrush(Color.Parse(dark ? darkHex : lightHex));

        B("Window", "#1C2733", "#F2F5F8");
        // The tab strip is SourceTree's vivid accent blue in both themes; tabs are dark
        // navy with light text, the active one slightly darker and marked by its ✕.
        B("Strip", "#1B72CE", "#1B72CE");
        B("TabInactive", "#161C28", "#161C28");
        B("TabHover", "#1F2735", "#1F2735");
        B("TabActive", "#0F1420", "#0F1420");
        B("TabActiveText", "#FFFFFF", "#FFFFFF");
        B("TabText", "#C9D6E4", "#C9D6E4");
        B("StripIcon", "#E3EEF9", "#E3EEF9");  // +, ▾, scroll arrows, hamburger
        B("Menu", "#0E1822", "#CDD7E1");       // menu row shares the title-bar color
        B("Toolbar", "#18242F", "#E8EDF2");    // toolbar, column headers, filter rows
        B("Panel", "#131D27", "#E2E8EF");      // sidebar
        B("Content", "#10161E", "#FFFFFF");    // history list, diff, new-tab body
        B("Input", "#141E28", "#FFFFFF");      // text boxes, cards, commit box
        B("Border", "#0B141D", "#C3CDD7");
        B("Divider", "#2A3A4A", "#C3CDD7");
        B("Text", "#CDD9E5", "#22313F");
        B("TextDim", "#7E92A5", "#5C7080");
        B("TextBright", "#D9E1E8", "#16202B");
        B("Hover", "#243546", "#D3DDE7");
        B("Chip", "#243546", "#D7E0E9");
        B("ChipText", "#9FB4C8", "#46586A");
        B("Accent", "#2E74B5", "#2E74B5");
        B("AccentHover", "#3A84C8", "#3A84C8");

        // Icons are SVG files tinted per theme and exposed as I.<Name> image resources.
        string stroke = dark ? "#9FB4C8" : "#46586A";
        foreach (var name in Views.Icons.Names)
            res["I." + name] = Views.Icons.Load(name, stroke);

        ThemePalette.Apply(dark);
    }
}
