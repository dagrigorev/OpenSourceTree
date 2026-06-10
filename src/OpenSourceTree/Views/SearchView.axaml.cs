using Avalonia.Controls;
using Avalonia.Input;
using OpenSourceTree.ViewModels;

namespace OpenSourceTree.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
    }

    private void Results_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is RepositoryViewModel vm && ResultsList.SelectedItem is CommitRowViewModel row)
            vm.GoToCommit(row);
    }
}
