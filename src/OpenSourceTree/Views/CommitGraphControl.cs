using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OpenSourceTree.Models;

namespace OpenSourceTree.Views;

/// <summary>Draws one row of the commit graph: lane lines, merge edges and the commit node.</summary>
public sealed class CommitGraphControl : Control
{
    public static readonly StyledProperty<GraphRow?> RowProperty =
        AvaloniaProperty.Register<CommitGraphControl, GraphRow?>(nameof(Row));

    public GraphRow? Row
    {
        get => GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    static CommitGraphControl()
    {
        AffectsRender<CommitGraphControl>(RowProperty);
        ClipToBoundsProperty.OverrideDefaultValue<CommitGraphControl>(true);
    }

    private const double LaneWidth = 14;
    private const double NodeRadius = 3.6;

    private static readonly Color[] Palette =
    {
        Color.Parse("#4A9EE0"), // blue
        Color.Parse("#E8A33D"), // orange
        Color.Parse("#5CB85C"), // green
        Color.Parse("#B07CC6"), // purple
        Color.Parse("#D9534F"), // red
        Color.Parse("#4ECDC4"), // teal
        Color.Parse("#E867A5"), // pink
        Color.Parse("#C8C25A")  // olive
    };

    private static readonly Pen[] Pens = CreatePens();

    private static Pen[] CreatePens()
    {
        var pens = new Pen[Palette.Length];
        for (int i = 0; i < Palette.Length; i++)
            pens[i] = new Pen(new SolidColorBrush(Palette[i]), 2);
        return pens;
    }

    private static double LaneX(int lane) => LaneWidth / 2 + lane * LaneWidth;

    public override void Render(DrawingContext context)
    {
        var row = Row;
        if (row is null)
            return;

        double h = Bounds.Height;
        double cy = h / 2;

        foreach (var seg in row.Segments)
        {
            var pen = Pens[seg.ColorIndex % Pens.Length];
            if (seg.IsTopHalf)
                context.DrawLine(pen, new Point(LaneX(seg.FromLane), 0), new Point(LaneX(seg.ToLane), cy));
            else
                context.DrawLine(pen, new Point(LaneX(seg.FromLane), cy), new Point(LaneX(seg.ToLane), h));
        }

        var brush = new SolidColorBrush(Palette[row.ColorIndex % Palette.Length]);
        context.DrawEllipse(brush, null, new Point(LaneX(row.NodeLane), cy), NodeRadius, NodeRadius);
    }
}
