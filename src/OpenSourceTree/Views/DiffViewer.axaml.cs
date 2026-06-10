using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace OpenSourceTree.Views;

public partial class DiffViewer : UserControl
{
    public static readonly StyledProperty<IEnumerable?> LinesProperty =
        AvaloniaProperty.Register<DiffViewer, IEnumerable?>(nameof(Lines));

    public IEnumerable? Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    public DiffViewer()
    {
        InitializeComponent();
    }
}
