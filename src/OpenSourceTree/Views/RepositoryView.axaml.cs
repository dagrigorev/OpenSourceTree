using Avalonia.Controls;
using Avalonia.Input;
using OpenSourceTree.ViewModels;

namespace OpenSourceTree.Views;

public partial class RepositoryView : UserControl
{
    public RepositoryView()
    {
        InitializeComponent();
    }

    private void SidebarItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control ||
            !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            return;
        if (control.DataContext is ISidebarItem item && DataContext is RepositoryViewModel vm)
            vm.SelectSidebarItem(item);
    }

    private void Branch_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: BranchItemViewModel branch })
            branch.CheckoutCommand.Execute(null);
    }

    private void RemoteBranch_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: RemoteBranchItemViewModel branch })
            branch.CheckoutCommand.Execute(null);
    }

    private void Submodule_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: SubmoduleItemViewModel submodule })
            submodule.OpenCommand.Execute(null);
    }
}
