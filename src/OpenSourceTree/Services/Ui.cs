using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace OpenSourceTree.Services;

/// <summary>Small modal-dialog toolkit; dialogs are built in code so they stay compact.</summary>
public static class Ui
{
    private static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private static readonly IBrush DialogBg = new SolidColorBrush(Color.Parse("#1C2733"));

    private static Window MakeDialog(string title, double width, double height)
    {
        return new Window
        {
            Title = title,
            Width = width,
            Height = height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = DialogBg,
            ShowInTaskbar = false
        };
    }

    private static Button MakeButton(string text, bool accent = false)
    {
        var b = new Button
        {
            Content = text,
            MinWidth = 90,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };
        if (accent)
            b.Classes.Add("accent");
        return b;
    }

    // ---------- Message boxes ----------

    public static Task ShowErrorAsync(string title, string message) => ShowMessageAsync(title, message, isError: true);

    public static Task ShowInfoAsync(string title, string message) => ShowMessageAsync(title, message, isError: false);

    private static async Task ShowMessageAsync(string title, string message, bool isError)
    {
        var owner = MainWindow;
        if (owner is null)
            return;

        var dialog = MakeDialog(title, 460, double.NaN);
        dialog.SizeToContent = SizeToContent.Height;

        var ok = MakeButton("OK", accent: true);
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = isError ? new SolidColorBrush(Color.Parse("#E89A9A")) : Brushes.Gainsboro,
            MaxWidth = 420
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var owner = MainWindow;
        if (owner is null)
            return false;

        var dialog = MakeDialog(title, 460, double.NaN);
        dialog.SizeToContent = SizeToContent.Height;
        bool result = false;

        var yes = MakeButton("Yes", accent: true);
        var no = MakeButton("No");
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(no);
        buttons.Children.Add(yes);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(owner);
        return result;
    }

    // ---------- Input ----------

    public static async Task<(string? Text, bool Checked)> ShowInputAsync(
        string title, string prompt, string initial = "",
        string? checkboxLabel = null, bool checkboxInitial = false)
    {
        var owner = MainWindow;
        if (owner is null)
            return (null, false);

        var dialog = MakeDialog(title, 460, double.NaN);
        dialog.SizeToContent = SizeToContent.Height;

        string? text = null;
        var input = new TextBox { Text = initial, Watermark = prompt };
        var check = new CheckBox { Content = checkboxLabel, IsChecked = checkboxInitial, IsVisible = checkboxLabel is not null };

        var ok = MakeButton("OK", accent: true);
        var cancel = MakeButton("Cancel");
        ok.Click += (_, _) => { text = input.Text ?? ""; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter) { text = input.Text ?? ""; dialog.Close(); }
        };

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = prompt });
        panel.Children.Add(input);
        panel.Children.Add(check);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.Opened += (_, _) => input.Focus();

        await dialog.ShowDialog(owner);
        return (text, check.IsChecked == true);
    }

    public static async Task<string?> ShowPickAsync(string title, string prompt, IReadOnlyList<string> options)
    {
        var owner = MainWindow;
        if (owner is null || options.Count == 0)
            return null;

        var dialog = MakeDialog(title, 460, double.NaN);
        dialog.SizeToContent = SizeToContent.Height;

        string? result = null;
        var combo = new ComboBox { ItemsSource = options, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };

        var ok = MakeButton("OK", accent: true);
        var cancel = MakeButton("Cancel");
        ok.Click += (_, _) => { result = combo.SelectedItem as string; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = prompt });
        panel.Children.Add(combo);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(owner);
        return result;
    }

    // ---------- Repository settings (Remotes + Advanced tabs, like SourceTree) ----------

    private sealed class RemoteRow
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public bool Existing { get; init; }
        public string OriginalUrl { get; init; } = "";
        public string Display => $"{Name}    —    {Url}";
    }

    private static async Task<(string Name, string Url)?> ShowRemoteEditAsync(
        Window owner, string name, string url, bool nameEditable)
    {
        var dialog = MakeDialog(nameEditable ? "Add remote" : "Edit remote", 460, double.NaN);
        dialog.SizeToContent = SizeToContent.Height;

        (string, string)? result = null;
        var nameBox = new TextBox { Text = name, Watermark = "Remote name (e.g. origin)", IsEnabled = nameEditable };
        var urlBox = new TextBox { Text = url, Watermark = "URL or path" };

        var ok = MakeButton("OK", accent: true);
        var cancel = MakeButton("Cancel");
        ok.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(nameBox.Text) && !string.IsNullOrWhiteSpace(urlBox.Text))
            {
                result = (nameBox.Text!.Trim(), urlBox.Text!.Trim());
                dialog.Close();
            }
        };
        cancel.Click += (_, _) => dialog.Close();

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "Remote name", Foreground = Brushes.Gray });
        panel.Children.Add(nameBox);
        panel.Children.Add(new TextBlock { Text = "URL / path", Foreground = Brushes.Gray });
        panel.Children.Add(urlBox);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(owner);
        return result;
    }

    public static async Task<Models.RepoSettingsResult?> ShowRepoSettingsAsync(
        string repoTitle,
        List<(string Name, string Url)> remotes,
        Models.UserConfigInfo user,
        string gitIgnorePath)
    {
        var owner = MainWindow;
        if (owner is null)
            return null;

        var dialog = MakeDialog($"Repository settings — {repoTitle}", 560, 470);
        Models.RepoSettingsResult? result = null;

        // ----- Remotes tab -----
        var rows = new System.Collections.ObjectModel.ObservableCollection<RemoteRow>(
            remotes.Select(r => new RemoteRow { Name = r.Name, Url = r.Url, Existing = true, OriginalUrl = r.Url }));
        var removed = new List<string>();

        var remoteList = new ListBox
        {
            ItemsSource = rows,
            Height = 250,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<RemoteRow>((row, _) =>
                row is null
                    ? new TextBlock()
                    : new TextBlock { Text = row.Display, Margin = new Thickness(4, 2) })
        };

        var addBtn = MakeButton("Add…");
        var editBtn = MakeButton("Edit…");
        var removeBtn = MakeButton("Remove");

        addBtn.Click += async (_, _) =>
        {
            var entry = await ShowRemoteEditAsync(dialog, "", "", nameEditable: true);
            if (entry is null)
                return;
            if (rows.Any(r => r.Name == entry.Value.Name))
                return;
            rows.Add(new RemoteRow { Name = entry.Value.Name, Url = entry.Value.Url, Existing = false });
        };
        editBtn.Click += async (_, _) =>
        {
            if (remoteList.SelectedItem is not RemoteRow row)
                return;
            var entry = await ShowRemoteEditAsync(dialog, row.Name, row.Url, nameEditable: !row.Existing);
            if (entry is null)
                return;
            row.Name = entry.Value.Name;
            row.Url = entry.Value.Url;
            // refresh the row display
            int i = rows.IndexOf(row);
            rows.RemoveAt(i);
            rows.Insert(i, row);
        };
        removeBtn.Click += (_, _) =>
        {
            if (remoteList.SelectedItem is not RemoteRow row)
                return;
            if (row.Existing)
                removed.Add(row.Name);
            rows.Remove(row);
        };

        var remoteButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0)
        };
        remoteButtons.Children.Add(addBtn);
        remoteButtons.Children.Add(editBtn);
        remoteButtons.Children.Add(removeBtn);

        var remotesPanel = new StackPanel { Margin = new Thickness(14), Spacing = 6 };
        remotesPanel.Children.Add(new TextBlock { Text = "Remote repository paths", Foreground = Brushes.Gray });
        remotesPanel.Children.Add(remoteList);
        remotesPanel.Children.Add(remoteButtons);

        // ----- Advanced tab -----
        var editIgnore = MakeButton("Edit…");
        editIgnore.Click += (_, _) =>
        {
            try
            {
                if (!System.IO.File.Exists(gitIgnorePath))
                    System.IO.File.WriteAllText(gitIgnorePath, "");
                PlatformService.OpenInEditor(gitIgnorePath);
            }
            catch
            {
                // best-effort
            }
        };

        var ignoreRow = new DockPanel();
        DockPanel.SetDock(editIgnore, Dock.Right);
        ignoreRow.Children.Add(editIgnore);
        ignoreRow.Children.Add(new TextBlock
        {
            Text = gitIgnorePath,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
        });

        var useGlobal = new CheckBox { Content = "Use global user settings", IsChecked = !user.HasLocal };
        var nameBox = new TextBox { Watermark = "Full name" };
        var emailBox = new TextBox { Watermark = "Email address" };

        void SyncIdentityBoxes()
        {
            bool global = useGlobal.IsChecked == true;
            nameBox.IsEnabled = !global;
            emailBox.IsEnabled = !global;
            nameBox.Text = global ? user.GlobalName : (user.HasLocal ? user.LocalName : user.GlobalName);
            emailBox.Text = global ? user.GlobalEmail : (user.HasLocal ? user.LocalEmail : user.GlobalEmail);
        }

        SyncIdentityBoxes();
        useGlobal.IsCheckedChanged += (_, _) => SyncIdentityBoxes();

        var advancedPanel = new StackPanel { Margin = new Thickness(14), Spacing = 10 };
        advancedPanel.Children.Add(new TextBlock { Text = "Repository ignore list", Foreground = Brushes.Gray });
        advancedPanel.Children.Add(ignoreRow);
        advancedPanel.Children.Add(new Separator { Margin = new Thickness(0, 6) });
        advancedPanel.Children.Add(new TextBlock { Text = "User information", Foreground = Brushes.Gray });
        advancedPanel.Children.Add(useGlobal);
        advancedPanel.Children.Add(new TextBlock { Text = "Full name" });
        advancedPanel.Children.Add(nameBox);
        advancedPanel.Children.Add(new TextBlock { Text = "Email address" });
        advancedPanel.Children.Add(emailBox);

        // ----- Tabs + buttons -----
        var tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "Remotes", Content = remotesPanel },
                new TabItem { Header = "Advanced", Content = advancedPanel }
            }
        };

        var ok = MakeButton("OK", accent: true);
        var cancel = MakeButton("Cancel");
        ok.Click += (_, _) =>
        {
            result = new Models.RepoSettingsResult(
                AddedRemotes: rows.Where(r => !r.Existing).Select(r => (r.Name, r.Url)).ToList(),
                UpdatedRemotes: rows.Where(r => r.Existing && r.Url != r.OriginalUrl).Select(r => (r.Name, r.Url)).ToList(),
                RemovedRemotes: removed,
                UseGlobalIdentity: useGlobal.IsChecked == true,
                LocalName: nameBox.Text ?? "",
                LocalEmail: emailBox.Text ?? "");
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        var bottom = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(14, 8, 14, 12)
        };
        bottom.Children.Add(cancel);
        bottom.Children.Add(ok);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(tabs);
        dialog.Content = root;

        await dialog.ShowDialog(owner);
        return result;
    }

    // ---------- Git Flow ----------

    /// <summary>Initialization dialog; returns the configuration to apply or null.</summary>
    public static async Task<Models.GitFlowConfig?> ShowGitFlowInitAsync(Models.GitFlowConfig defaults)
    {
        var owner = MainWindow;
        if (owner is null)
            return null;

        var dialog = MakeDialog("Initialize Git Flow", 460, double.NaN);
        dialog.SizeToContent = SizeToContent.Height;
        Models.GitFlowConfig? result = null;

        var master = new TextBox { Text = defaults.Master };
        var develop = new TextBox { Text = defaults.Develop };
        var feature = new TextBox { Text = defaults.FeaturePrefix };
        var release = new TextBox { Text = defaults.ReleasePrefix };
        var hotfix = new TextBox { Text = defaults.HotfixPrefix };
        var tagPrefix = new TextBox { Text = defaults.VersionTagPrefix, Watermark = "(none)" };

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
        void Field(string label, TextBox box)
        {
            panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.Gray, FontSize = 12 });
            panel.Children.Add(box);
        }
        Field("Production branch", master);
        Field("Development branch (created if missing)", develop);
        Field("Feature prefix", feature);
        Field("Release prefix", release);
        Field("Hotfix prefix", hotfix);
        Field("Version tag prefix", tagPrefix);

        var ok = MakeButton("Initialize", accent: true);
        var cancel = MakeButton("Cancel");
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(master.Text) || string.IsNullOrWhiteSpace(develop.Text))
                return;
            result = new Models.GitFlowConfig(master.Text!.Trim(), develop.Text!.Trim(),
                feature.Text ?? "feature/", release.Text ?? "release/", hotfix.Text ?? "hotfix/",
                tagPrefix.Text ?? "", true);
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(owner);
        return result;
    }

    /// <summary>Action hub: returns "feature" / "release" / "hotfix" / "finish" or null.</summary>
    public static async Task<string?> ShowGitFlowHubAsync(string currentBranch, string? finishLabel)
    {
        var owner = MainWindow;
        if (owner is null)
            return null;

        var dialog = MakeDialog("Git Flow", 420, double.NaN);
        dialog.SizeToContent = SizeToContent.Height;
        string? result = null;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = $"Current branch:  {currentBranch}", Margin = new Thickness(0, 0, 0, 8) });

        void Action(string label, string value, bool accent = false, bool enabled = true)
        {
            var b = MakeButton(label, accent);
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0);
            b.IsEnabled = enabled;
            b.Click += (_, _) => { result = value; dialog.Close(); };
            panel.Children.Add(b);
        }

        Action("Start New Feature…", "feature");
        Action("Start New Release…", "release");
        Action("Start New Hotfix…", "hotfix");
        panel.Children.Add(new Separator { Margin = new Thickness(0, 4) });
        Action(finishLabel ?? "Finish Current (not on a flow branch)", "finish",
            accent: finishLabel is not null, enabled: finishLabel is not null);

        var cancel = MakeButton("Cancel");
        cancel.HorizontalAlignment = HorizontalAlignment.Right;
        cancel.Margin = new Thickness(0, 10, 0, 0);
        cancel.Click += (_, _) => dialog.Close();
        panel.Children.Add(cancel);
        dialog.Content = panel;

        await dialog.ShowDialog(owner);
        return result;
    }

    // ---------- Interactive rebase ----------

    private sealed class RebaseRow
    {
        public required Models.CommitInfo Commit { get; init; }
        public int ActionIndex { get; set; } // 0 pick, 1 reword, 2 squash, 3 drop
        public string? NewMessage { get; set; }
    }

    public static async Task<List<Models.RebaseStep>?> ShowInteractiveRebaseAsync(
        IReadOnlyList<Models.CommitInfo> commits)
    {
        var owner = MainWindow;
        if (owner is null)
            return null;

        var dialog = MakeDialog("Interactive rebase", 720, 560);
        dialog.CanResize = true;
        List<Models.RebaseStep>? result = null;

        var rows = new System.Collections.ObjectModel.ObservableCollection<RebaseRow>(
            commits.Select(c => new RebaseRow { Commit = c }));

        var mono = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace");
        var list = new ListBox
        {
            ItemsSource = rows,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<RebaseRow>((row, _) =>
            {
                if (row is null)
                    return new TextBlock();
                var combo = new ComboBox
                {
                    ItemsSource = new[] { "Pick", "Reword", "Squash", "Drop" },
                    SelectedIndex = row.ActionIndex,
                    Width = 104,
                    VerticalAlignment = VerticalAlignment.Center
                };
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedIndex >= 0)
                        row.ActionIndex = combo.SelectedIndex;
                };
                var sha = new TextBlock
                {
                    Text = row.Commit.ShortSha,
                    FontFamily = mono,
                    FontSize = 12,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0)
                };
                var msg = new TextBlock
                {
                    Text = row.Commit.MessageShort,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
                };
                var dock = new DockPanel();
                dock.Children.Add(combo);
                DockPanel.SetDock(combo, Dock.Left);
                dock.Children.Add(sha);
                DockPanel.SetDock(sha, Dock.Left);
                dock.Children.Add(msg);
                return dock;
            }, supportsRecycling: false)
        };

        var hint = new TextBlock
        {
            Text = "Oldest commit first. Squash combines a commit into the previous kept one. " +
                   "The message box below is applied when the action is Reword.",
            Foreground = Brushes.Gray,
            FontSize = 12,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var msgBox = new TextBox
        {
            AcceptsReturn = true,
            Height = 80,
            Watermark = "Commit message (select a row to edit)",
            IsEnabled = false
        };

        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is RebaseRow row)
            {
                msgBox.IsEnabled = true;
                msgBox.Text = row.NewMessage ?? row.Commit.FullMessage.TrimEnd();
            }
        };
        msgBox.TextChanged += (_, _) =>
        {
            if (list.SelectedItem is RebaseRow row && msgBox.IsEnabled)
                row.NewMessage = msgBox.Text;
        };

        var upBtn = MakeButton("Move Up");
        var downBtn = MakeButton("Move Down");
        void Move(int delta)
        {
            int i = list.SelectedIndex;
            int j = i + delta;
            if (i < 0 || j < 0 || j >= rows.Count)
                return;
            rows.Move(i, j);
            list.SelectedIndex = j;
        }
        upBtn.Click += (_, _) => Move(-1);
        downBtn.Click += (_, _) => Move(1);

        var ok = MakeButton("Rebase", accent: true);
        var cancel = MakeButton("Cancel");
        ok.Click += (_, _) =>
        {
            var steps = rows.Select(r => new Models.RebaseStep(
                r.Commit.Sha,
                (Models.RebaseAction)r.ActionIndex,
                r.ActionIndex == 1 ? (r.NewMessage ?? r.Commit.FullMessage) : null)).ToList();
            if (steps.All(s => s.Action == Models.RebaseAction.Drop))
                return; // dropping everything is surely a mistake
            result = steps;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        var moveButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        moveButtons.Children.Add(upBtn);
        moveButtons.Children.Add(downBtn);

        var bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var okCancel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        okCancel.Children.Add(cancel);
        okCancel.Children.Add(ok);
        DockPanel.SetDock(okCancel, Dock.Right);
        bottom.Children.Add(okCancel);
        bottom.Children.Add(moveButtons);

        var root = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(hint, Dock.Top);
        root.Children.Add(hint);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        DockPanel.SetDock(msgBox, Dock.Bottom);
        msgBox.Margin = new Thickness(0, 8, 0, 0);
        root.Children.Add(msgBox);
        list.Margin = new Thickness(0, 8, 0, 0);
        root.Children.Add(list);
        dialog.Content = root;

        await dialog.ShowDialog(owner);
        return result;
    }

    // ---------- Submodules ----------

    public static async Task<(string Url, string Path)?> ShowAddSubmoduleAsync()
    {
        var owner = MainWindow;
        if (owner is null)
            return null;

        var dialog = MakeDialog("Add submodule", 480, double.NaN);
        dialog.SizeToContent = SizeToContent.Height;
        (string, string)? result = null;

        var urlBox = new TextBox { Watermark = "Repository URL or local path" };
        var pathBox = new TextBox { Watermark = "Folder inside this repository (e.g. libs/mylib)" };

        var ok = MakeButton("Add", accent: true);
        var cancel = MakeButton("Cancel");
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(urlBox.Text) || string.IsNullOrWhiteSpace(pathBox.Text))
                return;
            result = (urlBox.Text!.Trim(), pathBox.Text!.Trim());
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "Source URL", Foreground = Brushes.Gray });
        panel.Children.Add(urlBox);
        panel.Children.Add(new TextBlock { Text = "Local relative path", Foreground = Brushes.Gray });
        panel.Children.Add(pathBox);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(owner);
        return result;
    }

    // ---------- Application options (Tools -> Options, like SourceTree) ----------

    /// <summary>Shows the global Options dialog. Returns true when settings were changed and saved.</summary>
    public static async Task<bool> ShowOptionsAsync()
    {
        var owner = MainWindow;
        if (owner is null)
            return false;

        var settings = AppSettings.Instance;
        var (globalName, globalEmail) = GitService.GetGlobalUser();

        var dialog = MakeDialog("Options", 560, 470);
        bool saved = false;

        // ----- General tab -----
        var nameBox = new TextBox { Text = globalName, Watermark = "Full name" };
        var emailBox = new TextBox { Text = globalEmail, Watermark = "Email address" };
        var cloneDirBox = new TextBox { Text = settings.DefaultCloneDirectory ?? "", Watermark = "Folder new clones default to" };
        var browseCloneDir = MakeButton("Browse…");
        browseCloneDir.Click += async (_, _) =>
        {
            var folders = await dialog.StorageProvider.OpenFolderPickerAsync(
                new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = "Default clone directory" });
            if (folders.Count > 0)
                cloneDirBox.Text = folders[0].TryGetLocalPath() ?? cloneDirBox.Text;
        };
        var restoreTabs = new CheckBox { Content = "Reopen repositories on startup", IsChecked = settings.RestoreTabsOnStartup };
        var historyLimitBox = new TextBox { Text = settings.HistoryLimit.ToString(), Width = 100, HorizontalAlignment = HorizontalAlignment.Left };

        var cloneRow = new DockPanel();
        DockPanel.SetDock(browseCloneDir, Dock.Right);
        cloneRow.Children.Add(browseCloneDir);
        cloneRow.Children.Add(cloneDirBox);

        var generalPanel = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        generalPanel.Children.Add(new TextBlock { Text = "Default user information (global git config)", Foreground = Brushes.Gray });
        generalPanel.Children.Add(nameBox);
        generalPanel.Children.Add(emailBox);
        generalPanel.Children.Add(new Separator { Margin = new Thickness(0, 6) });
        generalPanel.Children.Add(new TextBlock { Text = "Default clone directory", Foreground = Brushes.Gray });
        generalPanel.Children.Add(cloneRow);
        generalPanel.Children.Add(new Separator { Margin = new Thickness(0, 6) });
        generalPanel.Children.Add(restoreTabs);
        generalPanel.Children.Add(new TextBlock { Text = "Commits to load in History", Foreground = Brushes.Gray, Margin = new Thickness(0, 6, 0, 0) });
        generalPanel.Children.Add(historyLimitBox);

        // ----- Git tab -----
        var gitPathBox = new TextBox { Text = settings.GitExecutablePath ?? "", Watermark = "Leave empty to use git from PATH" };
        var browseGit = MakeButton("Browse…");
        browseGit.Click += async (_, _) =>
        {
            var files = await dialog.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Select git executable" });
            if (files.Count > 0)
                gitPathBox.Text = files[0].TryGetLocalPath() ?? gitPathBox.Text;
        };
        var gitVersionText = new TextBlock { Text = "Detecting git version…", Foreground = Brushes.Gray, FontSize = 12 };
        _ = GitCliService.GetVersionAsync().ContinueWith(t =>
            Dispatcher.UIThread.Post(() => gitVersionText.Text = t.Result ?? "git executable not found"));

        var gitRow = new DockPanel();
        DockPanel.SetDock(browseGit, Dock.Right);
        gitRow.Children.Add(browseGit);
        gitRow.Children.Add(gitPathBox);

        var gitPanel = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        gitPanel.Children.Add(new TextBlock { Text = "Git executable used for clone / fetch / pull / push", Foreground = Brushes.Gray });
        gitPanel.Children.Add(gitRow);
        gitPanel.Children.Add(gitVersionText);
        gitPanel.Children.Add(new TextBlock
        {
            Text = "Local operations (status, commit, branches, diffs…) use the bundled libgit2 and need no git installation.",
            Foreground = Brushes.Gray,
            FontSize = 12,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        });

        // ----- Diff tab -----
        var contextBox = new TextBox { Text = settings.DiffContextLines.ToString(), Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
        var diffPanel = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        diffPanel.Children.Add(new TextBlock { Text = "Context lines shown around changes in diffs", Foreground = Brushes.Gray });
        diffPanel.Children.Add(contextBox);

        var tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "General", Content = generalPanel },
                new TabItem { Header = "Git", Content = gitPanel },
                new TabItem { Header = "Diff", Content = diffPanel }
            }
        };

        var ok = MakeButton("OK", accent: true);
        var cancel = MakeButton("Cancel");
        ok.Click += (_, _) =>
        {
            GitService.SetGlobalUser(nameBox.Text ?? "", emailBox.Text ?? "");
            settings.DefaultCloneDirectory = string.IsNullOrWhiteSpace(cloneDirBox.Text) ? null : cloneDirBox.Text!.Trim();
            settings.RestoreTabsOnStartup = restoreTabs.IsChecked == true;
            if (int.TryParse(historyLimitBox.Text, out int limit) && limit > 0)
                settings.HistoryLimit = Math.Clamp(limit, 100, 100_000);
            if (int.TryParse(contextBox.Text, out int ctx) && ctx >= 0)
                settings.DiffContextLines = Math.Clamp(ctx, 0, 100);
            settings.GitExecutablePath = string.IsNullOrWhiteSpace(gitPathBox.Text) ? null : gitPathBox.Text!.Trim();
            GitCliService.ResetCache();
            settings.Save();
            saved = true;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        var bottom = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(14, 8, 14, 12)
        };
        bottom.Children.Add(cancel);
        bottom.Children.Add(ok);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(tabs);
        dialog.Content = root;

        await dialog.ShowDialog(owner);
        return saved;
    }

    // ---------- Process output ----------

    public interface IOutputSink
    {
        void Append(string line);
        void Complete();
    }

    private sealed class OutputWindowSink : IOutputSink
    {
        private readonly Window _window;
        private readonly TextBox _text;
        private readonly Button _close;
        private readonly ScrollViewer _scroll;

        public OutputWindowSink(string title)
        {
            _text = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace"),
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.Parse("#10161E"))
            };
            _scroll = new ScrollViewer { Content = _text };
            _close = MakeButton("Close", accent: true);
            _close.IsEnabled = false;
            _close.Margin = new Thickness(0, 10, 0, 0);
            _close.HorizontalAlignment = HorizontalAlignment.Right;

            var root = new DockPanel { Margin = new Thickness(14) };
            DockPanel.SetDock(_close, Dock.Bottom);
            root.Children.Add(_close);
            root.Children.Add(_scroll);

            _window = MakeDialog(title, 720, 420);
            _window.CanResize = true;
            _window.Content = root;
            _close.Click += (_, _) => _window.Close();

            if (MainWindow is not null)
                _ = _window.ShowDialog(MainWindow);
            else
                _window.Show();
        }

        public void Append(string line) => Dispatcher.UIThread.Post(() =>
        {
            _text.Text += line + Environment.NewLine;
            _scroll.ScrollToEnd();
        });

        public void Complete() => Dispatcher.UIThread.Post(() => _close.IsEnabled = true);
    }

    public static IOutputSink ShowOutput(string title) => new OutputWindowSink(title);

    // ---------- Clipboard ----------

    public static async Task CopyToClipboardAsync(string text)
    {
        var owner = MainWindow;
        if (owner?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    // ---------- Folder picker ----------

    public static async Task<string?> PickFolderAsync(string title)
    {
        var owner = MainWindow;
        if (owner is null)
            return null;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
