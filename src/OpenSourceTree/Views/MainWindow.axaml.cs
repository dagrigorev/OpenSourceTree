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

    private void Tab_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: TabViewModelBase tab } &&
            DataContext is MainWindowViewModel vm)
        {
            vm.ActiveTab = tab;
            e.Handled = true;
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

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
