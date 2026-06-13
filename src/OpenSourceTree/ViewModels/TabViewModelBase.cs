using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenSourceTree.ViewModels;

public abstract partial class TabViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private bool _isActive;

    /// <summary>Small badge shown inside the tab (e.g. "2 ↑" when ahead of the remote).</summary>
    [ObservableProperty]
    private string _tabBadge = "";

    [ObservableProperty]
    private bool _hasTabBadge;

    partial void OnTabBadgeChanged(string value) => HasTabBadge = value.Length > 0;

    public virtual void OnClosed() { }
}
