using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenSourceTree.ViewModels;

public abstract partial class TabViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private bool _isActive;

    public virtual void OnClosed() { }
}
