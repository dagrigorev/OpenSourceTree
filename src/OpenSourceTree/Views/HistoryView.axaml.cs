using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using OpenSourceTree.ViewModels;

namespace OpenSourceTree.Views;

public partial class HistoryView : UserControl
{
    private RepositoryViewModel? _vm;

    public HistoryView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
                _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = DataContext as RepositoryViewModel;
            if (_vm is not null)
                _vm.PropertyChanged += OnVmPropertyChanged;
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryViewModel.SelectedCommit) &&
            _vm?.SelectedCommit is { } row)
        {
            HistoryList.ScrollIntoView(row);
        }
    }

    private void JumpBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not RepositoryViewModel vm)
            return;
        var row = vm.JumpToNext();
        if (row is not null)
            HistoryList.ScrollIntoView(row);
        e.Handled = true;
    }
}
