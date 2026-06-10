using Avalonia.Controls;
using Avalonia.Input;
using OpenSourceTree.ViewModels;

namespace OpenSourceTree.Views;

public partial class NewTabView : UserControl
{
    public NewTabView()
    {
        InitializeComponent();
    }

    private void RemoteRepo_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is NewTabViewModel vm &&
            RemoteList.SelectedItem is OpenSourceTree.Services.RemoteRepo repo)
        {
            vm.CloneRemote(repo);
        }
    }

    private void LocalRepo_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is NewTabViewModel vm &&
            LocalList.SelectedItem is NewTabViewModel.LocalRepoItem repo)
        {
            vm.OpenLocal(repo);
        }
    }
}
