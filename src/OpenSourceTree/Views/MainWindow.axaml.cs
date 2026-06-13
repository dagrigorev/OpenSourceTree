using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenSourceTree.ViewModels;

namespace OpenSourceTree.Views;

public partial class MainWindow : Window
{
    // The Edit menu acts on the text box that last had focus — opening the menu
    // itself moves focus, so the current focus cannot be used directly.
    private TextBox? _lastFocusedTextBox;

    public MainWindow()
    {
        InitializeComponent();
        Closing += (_, _) => (DataContext as MainWindowViewModel)?.OnWindowClosing();

        AddHandler(GotFocusEvent, (_, e) =>
        {
            if (e.Source is TextBox tb)
                _lastFocusedTextBox = tb;
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);

        KeyDown += (_, e) =>
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm is null)
                return;
            if (e.Key == Key.F5)
            {
                vm.ActiveRepository?.RefreshCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.T && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                vm.NewTabCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                vm.OpenRepositoryDialogCommand.Execute(null);
                e.Handled = true;
            }
        };
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source is not Button)
            BeginMoveDrag(e);
    }

    private void TitleBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    // ----- Tab activation + drag-to-reorder (live reorder, like browser tabs) -----

    private TabViewModelBase? _dragTab;
    private bool _dragging;
    private double _dragStartX;

    private void Tab_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: TabViewModelBase tab } ||
            DataContext is not MainWindowViewModel vm)
            return;

        vm.ActiveTab = tab;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            TabsHost.ItemsPanelRoot is { } panel)
        {
            _dragTab = tab;
            _dragging = false;
            _dragStartX = e.GetPosition(panel).X;
            // Capture on the strip: it survives container recycling during reorder.
            e.Pointer.Capture(TabStrip);
        }
        e.Handled = true;
    }

    private void Strip_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragTab is null || DataContext is not MainWindowViewModel vm ||
            TabsHost.ItemsPanelRoot is not { } panel)
            return;

        double x = e.GetPosition(panel).X;
        if (!_dragging && Math.Abs(x - _dragStartX) < 8)
            return;
        _dragging = true;

        int from = vm.Tabs.IndexOf(_dragTab);
        if (from < 0)
            return;

        for (int i = 0; i < vm.Tabs.Count; i++)
        {
            if (i == from || TabsHost.ContainerFromIndex(i) is not { } container)
                continue;
            var bounds = container.Bounds;
            double mid = bounds.X + bounds.Width / 2;
            // Move only after the pointer crosses the target tab's midpoint to avoid jitter.
            if ((i < from && x < mid) || (i > from && x > mid))
            {
                vm.Tabs.Move(from, i);
                break;
            }
        }
    }

    private void Strip_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging && DataContext is MainWindowViewModel vm)
            vm.SaveState();
        _dragTab = null;
        _dragging = false;
    }

    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

    private void ScrollTabsLeft_Click(object? sender, RoutedEventArgs e) =>
        TabScroll.Offset = TabScroll.Offset.WithX(System.Math.Max(0, TabScroll.Offset.X - 160));

    private void ScrollTabsRight_Click(object? sender, RoutedEventArgs e) =>
        TabScroll.Offset = TabScroll.Offset.WithX(TabScroll.Offset.X + 160);

    /// <summary>The "▾" next to "+": choose what kind of new tab to open (like SourceTree).</summary>
    private void NewTabMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        void Open(string section)
        {
            vm.NewTabCommand.Execute(null);
            (vm.ActiveTab as NewTabViewModel)?.SelectSectionCommand.Execute(section);
        }

        var flyout = new MenuFlyout();
        void Add(string key, string section)
        {
            var item = new MenuItem { Header = OpenSourceTree.Services.Loc.T(key) };
            item.Click += (_, _) => Open(section);
            flyout.Items.Add(item);
        }
        Add("NewTab", "Local");
        Add("Clone", "Clone");
        Add("Add", "Add");
        Add("New", "New");
        flyout.ShowAt(NewTabMenuButton);
    }

    /// <summary>The "☰" at the strip's right edge: a dropdown listing all open tabs.</summary>
    private void TabList_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var flyout = new MenuFlyout();
        foreach (var tab in vm.Tabs)
        {
            var current = tab;
            var item = new MenuItem
            {
                Header = current.Title,
                FontWeight = current.IsActive
                    ? Avalonia.Media.FontWeight.SemiBold
                    : Avalonia.Media.FontWeight.Normal
            };
            item.Click += (_, _) => vm.ActiveTab = current;
            flyout.Items.Add(item);
        }
        flyout.ShowAt(TabListButton);
    }

    private void WithLastTextBox(System.Action<TextBox> action)
    {
        if (_lastFocusedTextBox is not { } tb)
            return;
        tb.Focus();
        action(tb);
    }

    private void EditUndo_Click(object? sender, RoutedEventArgs e) => WithLastTextBox(tb => tb.Undo());
    private void EditRedo_Click(object? sender, RoutedEventArgs e) => WithLastTextBox(tb => tb.Redo());
    private void EditCut_Click(object? sender, RoutedEventArgs e) => WithLastTextBox(tb => tb.Cut());
    private void EditCopy_Click(object? sender, RoutedEventArgs e) => WithLastTextBox(tb => tb.Copy());
    private void EditPaste_Click(object? sender, RoutedEventArgs e) => WithLastTextBox(tb => tb.Paste());
    private void EditSelectAll_Click(object? sender, RoutedEventArgs e) => WithLastTextBox(tb => tb.SelectAll());
}
