using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenSourceTree.Services;

namespace OpenSourceTree.ViewModels;

public sealed partial class NewTabViewModel : TabViewModelBase
{
    private readonly MainWindowViewModel _main;

    public NewTabViewModel(MainWindowViewModel main)
    {
        _main = main;
        Title = "New tab";
        Recent = new ObservableCollection<string>(main.Settings.RecentRepositories.Where(Directory.Exists));
    }

    public ObservableCollection<string> Recent { get; }

    [ObservableProperty] private string _cloneUrl = "";
    [ObservableProperty] private string _cloneTarget = "";
    [ObservableProperty] private string _openPath = "";
    [ObservableProperty] private string _createPath = "";

    partial void OnCloneUrlChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        try
        {
            var name = value.TrimEnd('/').Split('/').LastOrDefault() ?? "repository";
            if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            var baseDir = _main.Settings.DefaultCloneDirectory
                          ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            CloneTarget = Path.Combine(baseDir, name);
        }
        catch
        {
            // leave target as-is for unusual URLs
        }
    }

    [RelayCommand]
    private async Task BrowseOpen()
    {
        var folder = await Ui.PickFolderAsync("Select a repository folder");
        if (folder is not null)
            OpenPath = folder;
    }

    [RelayCommand]
    private async Task BrowseCloneTarget()
    {
        var folder = await Ui.PickFolderAsync("Select destination folder");
        if (folder is not null)
            CloneTarget = folder;
    }

    [RelayCommand]
    private async Task BrowseCreate()
    {
        var folder = await Ui.PickFolderAsync("Select folder for the new repository");
        if (folder is not null)
            CreatePath = folder;
    }

    [RelayCommand]
    private async Task OpenRepository()
    {
        var path = OpenPath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!Directory.Exists(path))
        {
            await Ui.ShowErrorAsync("Open", $"Folder not found: {path}");
            return;
        }
        if (!GitService.IsRepository(path))
        {
            await Ui.ShowErrorAsync("Open", $"'{path}' is not a git repository.");
            return;
        }
        _main.OpenRepository(path, replaceTab: this);
    }

    [RelayCommand]
    private void OpenRecent(string? path)
    {
        if (path is null)
            return;
        _main.OpenRepository(path, replaceTab: this);
    }

    [RelayCommand]
    private async Task Clone()
    {
        var url = CloneUrl.Trim();
        var target = CloneTarget.Trim();
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(target))
        {
            await Ui.ShowErrorAsync("Clone", "Enter both a source URL and a destination path.");
            return;
        }

        var output = Ui.ShowOutput($"Clone — {url}");
        int code;
        try
        {
            code = await GitCliService.CloneAsync(url, target, line => output.Append(line));
        }
        catch (Exception ex)
        {
            output.Append("ERROR: " + ex.Message);
            code = -1;
        }
        output.Complete();

        if (code == 0 && Directory.Exists(target))
            _main.OpenRepository(target, replaceTab: this);
    }

    [RelayCommand]
    private async Task Create()
    {
        var path = CreatePath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            Directory.CreateDirectory(path);
            GitService.Init(path);
            _main.OpenRepository(path, replaceTab: this);
        }
        catch (Exception ex)
        {
            await Ui.ShowErrorAsync("Create repository", ex.Message);
        }
    }
}
