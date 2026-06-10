using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenSourceTree.Services;

namespace OpenSourceTree.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public AppSettings Settings { get; }

    public ObservableCollection<TabViewModelBase> Tabs { get; } = new();

    [ObservableProperty] private TabViewModelBase? _activeTab;

    public RepositoryViewModel? ActiveRepository => ActiveTab as RepositoryViewModel;

    partial void OnActiveTabChanged(TabViewModelBase? value)
    {
        foreach (var tab in Tabs)
            tab.IsActive = ReferenceEquals(tab, value);
        OnPropertyChanged(nameof(ActiveRepository));
    }

    public MainWindowViewModel()
    {
        Settings = AppSettings.Instance;
        RepositoryViewModel.OpenRepositoryHandler = path => OpenRepository(path);

        foreach (var path in Settings.RestoreTabsOnStartup
                     ? Settings.OpenRepositories.Where(Directory.Exists)
                     : Enumerable.Empty<string>())
        {
            try
            {
                if (GitService.IsRepository(path))
                    Tabs.Add(new RepositoryViewModel(path));
            }
            catch
            {
                // skip repositories that fail to open (moved/corrupt)
            }
        }

        if (Tabs.Count == 0)
            Tabs.Add(new NewTabViewModel(this));

        int idx = Math.Clamp(Settings.ActiveTabIndex, 0, Tabs.Count - 1);
        ActiveTab = Tabs[idx];
    }

    public void OpenRepository(string path, TabViewModelBase? replaceTab = null)
    {
        var full = Path.GetFullPath(path);
        var existing = Tabs.OfType<RepositoryViewModel>()
            .FirstOrDefault(t => string.Equals(t.RepoPath, full.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActiveTab = existing;
            return;
        }

        RepositoryViewModel repo;
        try
        {
            repo = new RepositoryViewModel(full);
        }
        catch (Exception ex)
        {
            _ = Ui.ShowErrorAsync("Open repository", ex.Message);
            return;
        }

        if (replaceTab is not null && Tabs.Contains(replaceTab))
        {
            int i = Tabs.IndexOf(replaceTab);
            Tabs[i] = repo;
            replaceTab.OnClosed();
        }
        else
        {
            Tabs.Add(repo);
        }

        ActiveTab = repo;
        Settings.TouchRecent(repo.RepoPath);
        SaveState();
    }

    [RelayCommand]
    private void NewTab()
    {
        var tab = new NewTabViewModel(this);
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    [RelayCommand]
    private void CloseTab(TabViewModelBase? tab)
    {
        if (tab is null)
            return;
        int index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        tab.OnClosed();
        if (Tabs.Count == 0)
            Tabs.Add(new NewTabViewModel(this));
        if (ActiveTab == tab || ActiveTab is null)
            ActiveTab = Tabs[Math.Clamp(index, 0, Tabs.Count - 1)];
        SaveState();
    }

    [RelayCommand]
    private async Task OpenRepositoryDialog()
    {
        var folder = await Ui.PickFolderAsync("Select a repository folder");
        if (folder is null)
            return;
        if (!GitService.IsRepository(folder))
        {
            await Ui.ShowErrorAsync("Open", $"'{folder}' is not a git repository.");
            return;
        }
        OpenRepository(folder);
    }

    // ---------- Tools menu ----------

    [RelayCommand]
    private async Task LaunchSshAgent()
    {
        try
        {
            PlatformService.StartSshAgent();
            await Ui.ShowInfoAsync("SSH Agent", "SSH agent start was requested.");
        }
        catch (Exception ex)
        {
            await Ui.ShowErrorAsync("SSH Agent", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenSshFolder() => PlatformService.OpenSshFolder();

    [RelayCommand]
    private async Task GenerateSshKey()
    {
        try
        {
            PlatformService.GenerateSshKey();
        }
        catch (Exception ex)
        {
            await Ui.ShowErrorAsync("Generate SSH key", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenOptions()
    {
        if (await Ui.ShowOptionsAsync() && ActiveRepository is { } repo)
            await repo.RefreshAsync();
    }

    // ---------- Help menu ----------

    [RelayCommand]
    private async Task ShowShortcuts()
    {
        await Ui.ShowInfoAsync("Keyboard shortcuts",
            "Ctrl+T — new tab\n" +
            "Ctrl+O — open repository\n" +
            "F5 — refresh current repository\n" +
            "Ctrl+Z / Ctrl+Y — undo / redo in text fields\n" +
            "Ctrl+X / Ctrl+C / Ctrl+V — cut / copy / paste\n" +
            "Ctrl+A — select all\n" +
            "Enter in 'Jump to' — go to next matching commit\n" +
            "Double-click branch — checkout");
    }

    [RelayCommand]
    private async Task OpenReadme()
    {
        // Look for README.md next to the executable, then up the source tree.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var readme = Path.Combine(dir.FullName, "README.md");
            if (File.Exists(readme))
            {
                PlatformService.OpenInEditor(readme);
                return;
            }
            dir = dir.Parent;
        }
        await Ui.ShowInfoAsync("Getting started",
            "README.md was not found next to the application.\nSee the project repository for documentation.");
    }

    [RelayCommand]
    private async Task About()
    {
        await Ui.ShowInfoAsync("About OpenSourceTree",
            "OpenSourceTree 0.1.0\n\nAn open-source, cross-platform Git client inspired by Atlassian SourceTree.\nBuilt with AvaloniaUI, LibGit2Sharp and the system git CLI.");
    }

    public void SaveState()
    {
        Settings.OpenRepositories = Tabs.OfType<RepositoryViewModel>().Select(t => t.RepoPath).ToList();
        Settings.ActiveTabIndex = ActiveTab is null ? 0 : Math.Max(0, Tabs.IndexOf(ActiveTab));
        Settings.Save();
    }

    public void OnWindowClosing()
    {
        SaveState();
        foreach (var tab in Tabs)
            tab.OnClosed();
    }
}
