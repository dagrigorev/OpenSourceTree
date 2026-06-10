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
        CloneParent = main.Settings.DefaultCloneDirectory
                      ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        RebuildLocal();
        InitAccounts();
    }

    // ---------- Sections (Local / Remote | Clone / Add / New, like SourceTree) ----------

    [ObservableProperty] private bool _isLocalSelected = true;
    [ObservableProperty] private bool _isRemoteSelected;
    [ObservableProperty] private bool _isCloneSelected;
    [ObservableProperty] private bool _isAddSelected;
    [ObservableProperty] private bool _isNewSelected;

    [RelayCommand]
    private void SelectSection(string section)
    {
        IsLocalSelected = section == "Local";
        IsRemoteSelected = section == "Remote";
        IsCloneSelected = section == "Clone";
        IsAddSelected = section == "Add";
        IsNewSelected = section == "New";
    }

    // ---------- Local repositories ----------

    public sealed partial class LocalRepoItem : ObservableObject
    {
        public LocalRepoItem(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }
        public string Path { get; }

        [ObservableProperty] private string _branch = "";
        [ObservableProperty] private bool _hasBranch;
        [ObservableProperty] private bool _isDirty;
    }

    public ObservableCollection<LocalRepoItem> LocalRepos { get; } = new();

    [ObservableProperty] private string _localFilter = "";
    [ObservableProperty] private bool _hasLocalRepos;
    [ObservableProperty] private LocalRepoItem? _selectedLocal;

    public bool HasLocalSelection => SelectedLocal is not null;

    partial void OnSelectedLocalChanged(LocalRepoItem? value) => OnPropertyChanged(nameof(HasLocalSelection));

    partial void OnLocalFilterChanged(string value) => RebuildLocal();

    [RelayCommand]
    private void RefreshLocal() => RebuildLocal();

    private void RebuildLocal()
    {
        var query = LocalFilter.Trim();
        LocalRepos.Clear();
        foreach (var path in _main.Settings.RecentRepositories.Where(Directory.Exists))
        {
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (query.Length > 0 &&
                !name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !path.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;
            LocalRepos.Add(new LocalRepoItem(name, path));
        }
        HasLocalRepos = LocalRepos.Count > 0;
        _ = ProbeLocalAsync(LocalRepos.ToList());
    }

    /// <summary>Fills in branch / dirty badges in the background so the page opens instantly.</summary>
    private static async Task ProbeLocalAsync(System.Collections.Generic.List<LocalRepoItem> items)
    {
        foreach (var item in items)
        {
            var probe = await Task.Run(() => GitService.ProbeRepository(item.Path));
            if (probe is null)
                continue;
            item.Branch = probe.Value.Branch;
            item.HasBranch = probe.Value.Branch.Length > 0;
            item.IsDirty = probe.Value.IsDirty;
        }
    }

    public void OpenLocal(LocalRepoItem? repo)
    {
        if (repo is not null)
            _main.OpenRepository(repo.Path, replaceTab: this);
    }

    [RelayCommand]
    private void OpenLocalInExplorer()
    {
        if (SelectedLocal is not null)
            PlatformService.OpenFileExplorer(SelectedLocal.Path);
    }

    [RelayCommand]
    private void OpenLocalInTerminal()
    {
        if (SelectedLocal is not null)
            PlatformService.OpenTerminal(SelectedLocal.Path);
    }

    // ---------- Remote repositories (hosting accounts) ----------

    public ObservableCollection<HostingAccount> Accounts { get; } = new();
    public ObservableCollection<RemoteRepo> RemoteRepos { get; } = new();
    private System.Collections.Generic.List<RemoteRepo> _allRemote = new();

    [ObservableProperty] private HostingAccount? _selectedAccount;
    [ObservableProperty] private string _remoteFilter = "";
    [ObservableProperty] private bool _isRemoteLoading;
    [ObservableProperty] private string _remoteStatus = "";
    [ObservableProperty] private bool _hasAccounts;

    public bool HasSelectedAccount => SelectedAccount is not null;

    partial void OnSelectedAccountChanged(HostingAccount? value)
    {
        OnPropertyChanged(nameof(HasSelectedAccount));
        _ = LoadRemoteAsync();
    }

    partial void OnRemoteFilterChanged(string value) => ApplyRemoteFilter();

    partial void OnIsRemoteSelectedChanged(bool value)
    {
        if (value && SelectedAccount is null && Accounts.Count > 0)
            SelectedAccount = Accounts[0];
    }

    private void InitAccounts()
    {
        foreach (var account in _main.Settings.Accounts)
            Accounts.Add(account);
        HasAccounts = Accounts.Count > 0;
        RemoteStatus = HasAccounts ? "" : "Add a GitHub or GitLab account to browse its repositories.";
    }

    [RelayCommand]
    private async Task AddAccount()
    {
        var account = await Ui.ShowAddAccountAsync();
        if (account is null)
            return;
        Accounts.Add(account);
        _main.Settings.Accounts.Add(account);
        _main.Settings.Save();
        HasAccounts = true;
        SelectedAccount = account;
    }

    [RelayCommand]
    private async Task RemoveAccount()
    {
        if (SelectedAccount is not { } account)
            return;
        if (!await Ui.ShowConfirmAsync("Remove account", $"Remove '{account.Display}'?"))
            return;
        Accounts.Remove(account);
        _main.Settings.Accounts.Remove(account);
        _main.Settings.Save();
        HasAccounts = Accounts.Count > 0;
        SelectedAccount = Accounts.FirstOrDefault();
        if (SelectedAccount is null)
        {
            _allRemote.Clear();
            ApplyRemoteFilter();
            RemoteStatus = "Add a GitHub or GitLab account to browse its repositories.";
        }
    }

    [RelayCommand]
    private Task RefreshRemote() => LoadRemoteAsync();

    private async Task LoadRemoteAsync()
    {
        if (SelectedAccount is not { } account)
            return;

        IsRemoteLoading = true;
        RemoteStatus = $"Loading repositories for {account.Display}…";
        try
        {
            _allRemote = await HostingService.ListAsync(account);
            RemoteStatus = _allRemote.Count == 0
                ? "No repositories found."
                : $"{_allRemote.Count} repositories. Double-click one to set up a clone.";
        }
        catch (Exception ex)
        {
            _allRemote = new();
            RemoteStatus = ex.Message;
        }
        finally
        {
            IsRemoteLoading = false;
        }
        ApplyRemoteFilter();
    }

    private void ApplyRemoteFilter()
    {
        var query = RemoteFilter.Trim();
        RemoteRepos.Clear();
        foreach (var repo in _allRemote)
        {
            if (query.Length > 0 &&
                !repo.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !repo.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;
            RemoteRepos.Add(repo);
        }
    }

    /// <summary>Double-click on a remote repo: prefill the Clone form and switch to it.</summary>
    public void CloneRemote(RemoteRepo? repo)
    {
        if (repo is null)
            return;
        CloneUrl = repo.CloneUrl;
        SelectSection("Clone");
    }

    // ---------- Clone ----------

    [ObservableProperty] private string _cloneUrl = "";
    [ObservableProperty] private string _cloneParent = "";
    [ObservableProperty] private string _cloneName = "";

    public string CloneTargetPreview =>
        string.IsNullOrWhiteSpace(CloneName) ? "" : Path.Combine(CloneParent, CloneName.Trim());

    partial void OnCloneParentChanged(string value) => OnPropertyChanged(nameof(CloneTargetPreview));
    partial void OnCloneNameChanged(string value) => OnPropertyChanged(nameof(CloneTargetPreview));

    partial void OnCloneUrlChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        try
        {
            var name = value.TrimEnd('/').Split('/', '\\').LastOrDefault() ?? "";
            if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            if (name.Length > 0)
                CloneName = name;
        }
        catch
        {
            // leave the name as-is for unusual URLs
        }
    }

    [RelayCommand]
    private async Task BrowseCloneParent()
    {
        var folder = await Ui.PickFolderAsync("Select destination folder");
        if (folder is not null)
            CloneParent = folder;
    }

    [RelayCommand]
    private async Task Clone()
    {
        var url = CloneUrl.Trim();
        var target = CloneTargetPreview;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(target))
        {
            await Ui.ShowErrorAsync("Clone", "Enter a source URL, a destination path and a name.");
            return;
        }
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
        {
            await Ui.ShowErrorAsync("Clone", $"'{target}' already exists and is not empty.");
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

    // ---------- Add an existing local repository ----------

    [ObservableProperty] private string _addPath = "";

    [RelayCommand]
    private async Task BrowseAdd()
    {
        var folder = await Ui.PickFolderAsync("Select a repository folder");
        if (folder is not null)
            AddPath = folder;
    }

    [RelayCommand]
    private async Task AddRepository()
    {
        var path = AddPath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!Directory.Exists(path))
        {
            await Ui.ShowErrorAsync("Add", $"Folder not found: {path}");
            return;
        }
        if (!GitService.IsRepository(path))
        {
            await Ui.ShowErrorAsync("Add", $"'{path}' is not a git repository.");
            return;
        }
        _main.OpenRepository(path, replaceTab: this);
    }

    // ---------- Create a new repository ----------

    [ObservableProperty] private string _createPath = "";

    [RelayCommand]
    private async Task BrowseCreate()
    {
        var folder = await Ui.PickFolderAsync("Select folder for the new repository");
        if (folder is not null)
            CreatePath = folder;
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
