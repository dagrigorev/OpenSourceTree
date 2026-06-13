using System.Collections.Generic;
using Avalonia;

namespace OpenSourceTree.Services;

/// <summary>
/// String localization. Keys are published into Application.Resources so XAML can use
/// {DynamicResource L.Key} and update live when the language changes; code-built UI
/// uses <see cref="T"/>. Keys not present in a table fall back to English / themselves.
/// </summary>
public static class Loc
{
    public static string Current { get; private set; } = "en";

    /// <summary>Raised after the language changes so views can refresh cached text (combo boxes).</summary>
    public static event System.Action? Changed;

    public static void Apply(string language)
    {
        Current = language == "ru" ? "ru" : "en";
        var resources = Application.Current!.Resources;
        foreach (var (key, en) in En)
            resources["L." + key] = Current == "ru" && Ru.TryGetValue(key, out var ru) ? ru : en;
        Changed?.Invoke();
    }

    /// <summary>Translate a key (or an English label used as key) for code-built dialogs.</summary>
    public static string T(string key)
    {
        if (Current == "ru" && Ru.TryGetValue(key, out var ru))
            return ru;
        return En.TryGetValue(key, out var en) ? en : key;
    }

    private static readonly Dictionary<string, string> En = new()
    {
        // menus
        ["File"] = "File", ["Edit"] = "Edit", ["View"] = "View", ["Repository"] = "Repository",
        ["Actions"] = "Actions", ["Tools"] = "Tools", ["Help"] = "Help",
        ["NewTab"] = "New Tab", ["OpenRepo"] = "Open Repository…", ["Exit"] = "Exit",
        ["Undo"] = "Undo", ["Redo"] = "Redo", ["Cut"] = "Cut", ["Copy"] = "Copy",
        ["Paste"] = "Paste", ["SelectAll"] = "Select All",
        ["FileStatus"] = "File status", ["History"] = "History", ["Search"] = "Search",
        ["Refresh"] = "Refresh",
        ["CommitDots"] = "Commit…", ["Fetch"] = "Fetch", ["Pull"] = "Pull", ["Push"] = "Push",
        ["GitFlowDots"] = "Git Flow…", ["AddSubmodule"] = "Add Submodule…",
        ["UpdateSubmodules"] = "Update Submodules", ["SettingsDots"] = "Settings…",
        ["NewBranchDots"] = "New Branch…", ["MergeDots"] = "Merge…",
        ["StashDots"] = "Stash Changes…", ["NewTagDots"] = "New Tag…",
        ["OpenTerminal"] = "Open in Terminal", ["OpenExplorer"] = "Open in File Explorer",
        ["LaunchSshAgent"] = "Launch SSH Agent", ["AddSshKey"] = "Add SSH Key… (open ~/.ssh)",
        ["GenerateSshKey"] = "Generate SSH Key…", ["OptionsDots"] = "Options…",
        ["GettingStarted"] = "Getting Started (README)", ["Shortcuts"] = "Keyboard Shortcuts",
        ["About"] = "About OpenSourceTree",
        // toolbar
        ["Commit"] = "Commit", ["Branch"] = "Branch", ["Merge"] = "Merge", ["Stash"] = "Stash",
        ["Discard"] = "Discard", ["Tag"] = "Tag", ["GitFlow"] = "Git Flow",
        ["Terminal"] = "Terminal", ["Explorer"] = "Explorer", ["Settings"] = "Settings",
        // sidebar
        ["Workspace"] = "WORKSPACE", ["Branches"] = "BRANCHES", ["TagsHdr"] = "TAGS",
        ["Remotes"] = "REMOTES", ["Stashes"] = "STASHES", ["Submodules"] = "SUBMODULES",
        ["Filter"] = "Filter",
        // history
        ["AllBranches"] = "All branches", ["CurrentBranch"] = "Current branch",
        ["ShowRemote"] = "Show remote branches", ["SortDate"] = "Sort by date",
        ["SortTopo"] = "Sort topologically", ["JumpTo"] = "Jump to:",
        ["JumpWm"] = "message, author or SHA",
        ["ColGraph"] = "Graph", ["ColDescription"] = "Description", ["ColDate"] = "Date",
        ["ColAuthor"] = "Author", ["ColCommit"] = "Commit",
        ["DetCommit"] = "Commit:", ["DetParents"] = "Parents:", ["DetAuthor"] = "Author:",
        ["DetDate"] = "Date:", ["DetRefs"] = "Refs:",
        ["SelectFile"] = "Select a file to view the diff",
        // file status
        ["StagedFiles"] = "Staged files", ["UnstagedFiles"] = "Unstaged files",
        ["StageAll"] = "Stage All", ["UnstageAll"] = "Unstage All",
        ["PendSortStatus"] = "Pending files, sorted by file status",
        ["PendSortPath"] = "Pending files, sorted by path",
        ["PendSortName"] = "Pending files, sorted by file name",
        ["CommitMsgWm"] = "Commit message", ["PushNow"] = "Push changes immediately",
        ["Amend"] = "Amend latest commit",
        // search
        ["SearchWm"] = "Search commit messages, authors or SHA…",
        // new tab
        ["Local"] = "Local", ["Remote"] = "Remote", ["Clone"] = "Clone", ["Add"] = "Add",
        ["New"] = "New",
        ["LocalRepos"] = "Local repositories", ["RemoteRepos"] = "Remote repositories",
        ["LocalHint"] = "Repositories you have opened before appear here. Double-click to open.",
        ["ShowInExplorer"] = "Show in Explorer", ["OpenInTerminal"] = "Open in Terminal",
        ["SourceUrl"] = "Source path / URL", ["DestPath"] = "Destination path",
        ["NameLbl"] = "Name", ["Browse"] = "Browse…", ["WorkingCopy"] = "Working copy path",
        ["CreateRepo"] = "Create a new repository", ["AddAccount"] = "Add Account…",
        ["Remove"] = "Remove",
        ["ParentFolderWm"] = "Parent folder for the clone", ["FolderNameWm"] = "Folder name",
        ["RepoPathWm"] = "Path to an existing repository",
        ["NewRepoFolderWm"] = "Folder for the new repository",
        // context menus
        ["Checkout"] = "Checkout", ["MergeInto"] = "Merge into current branch",
        ["CopyBranchName"] = "Copy branch name", ["DeleteDots"] = "Delete…",
        ["Apply"] = "Apply", ["ApplyPop"] = "Apply and delete (pop)",
        ["OpenRepository"] = "Open repository", ["UpdateSubmodule"] = "Update (init + recursive)",
        ["CheckoutCommitDots"] = "Checkout this commit…", ["TagCommitDots"] = "Tag this commit…",
        ["ResetSoftDots"] = "Reset current branch to this commit (soft)…",
        ["ResetMixedDots"] = "Reset current branch to this commit (mixed)…",
        ["ResetHardDots"] = "Reset current branch to this commit (hard)…",
        ["RebaseDots"] = "Rebase children of this commit interactively…",
        ["CopySha"] = "Copy SHA",
        ["StageItem"] = "Stage", ["UnstageItem"] = "Unstage",
        ["DiscardDots"] = "Discard changes…",
        // dialogs / options
        ["OK"] = "OK", ["Cancel"] = "Cancel", ["Yes"] = "Yes", ["No"] = "No",
        ["Save"] = "Save", ["Close"] = "Close",
        ["Theme"] = "Theme", ["Language"] = "Language",
        ["Dark"] = "Dark", ["Light"] = "Light"
    };

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["File"] = "Файл", ["Edit"] = "Правка", ["View"] = "Вид", ["Repository"] = "Репозиторий",
        ["Actions"] = "Действия", ["Tools"] = "Инструменты", ["Help"] = "Справка",
        ["NewTab"] = "Новая вкладка", ["OpenRepo"] = "Открыть репозиторий…", ["Exit"] = "Выход",
        ["Undo"] = "Отменить", ["Redo"] = "Повторить", ["Cut"] = "Вырезать", ["Copy"] = "Копировать",
        ["Paste"] = "Вставить", ["SelectAll"] = "Выделить всё",
        ["FileStatus"] = "Состояние файлов", ["History"] = "История", ["Search"] = "Поиск",
        ["Refresh"] = "Обновить",
        ["CommitDots"] = "Закоммитить…", ["Fetch"] = "Извлечь", ["Pull"] = "Получить", ["Push"] = "Отправить",
        ["GitFlowDots"] = "Git Flow…", ["AddSubmodule"] = "Добавить подмодуль…",
        ["UpdateSubmodules"] = "Обновить подмодули", ["SettingsDots"] = "Настройки…",
        ["NewBranchDots"] = "Новая ветка…", ["MergeDots"] = "Слияние…",
        ["StashDots"] = "Спрятать изменения…", ["NewTagDots"] = "Новая метка…",
        ["OpenTerminal"] = "Открыть в терминале", ["OpenExplorer"] = "Открыть в проводнике",
        ["LaunchSshAgent"] = "Запустить агент SSH", ["AddSshKey"] = "Добавить ключ SSH… (открыть ~/.ssh)",
        ["GenerateSshKey"] = "Создать ключ SSH…", ["OptionsDots"] = "Настройки…",
        ["GettingStarted"] = "Начало работы (README)", ["Shortcuts"] = "Горячие клавиши",
        ["About"] = "О программе OpenSourceTree",
        ["Commit"] = "Закоммитить", ["Branch"] = "Ветка", ["Merge"] = "Слияние", ["Stash"] = "Спрятать",
        ["Discard"] = "Отменить", ["Tag"] = "Метка", ["GitFlow"] = "Git Flow",
        ["Terminal"] = "Терминал", ["Explorer"] = "Проводник", ["Settings"] = "Настройки",
        ["Workspace"] = "РАБОЧАЯ ОБЛАСТЬ", ["Branches"] = "ВЕТКИ", ["TagsHdr"] = "МЕТКИ",
        ["Remotes"] = "ВНЕШНИЕ ВЕТКИ", ["Stashes"] = "СПРЯТАННЫЕ ИЗМЕНЕНИЯ", ["Submodules"] = "ПОДМОДУЛИ",
        ["Filter"] = "Фильтр",
        ["AllBranches"] = "Все ветки", ["CurrentBranch"] = "Текущая ветка",
        ["ShowRemote"] = "Показывать внешние ветки", ["SortDate"] = "Сортировать по дате",
        ["SortTopo"] = "Топологический порядок", ["JumpTo"] = "Перейти к:",
        ["JumpWm"] = "сообщение, автор или SHA",
        ["ColGraph"] = "Граф", ["ColDescription"] = "Описание", ["ColDate"] = "Дата",
        ["ColAuthor"] = "Автор", ["ColCommit"] = "Коммит",
        ["DetCommit"] = "Коммит:", ["DetParents"] = "Родители:", ["DetAuthor"] = "Автор:",
        ["DetDate"] = "Дата:", ["DetRefs"] = "Метки:",
        ["SelectFile"] = "Выберите файл для просмотра изменений",
        ["StagedFiles"] = "Файлы в индексе", ["UnstagedFiles"] = "Файлы не в индексе",
        ["StageAll"] = "Индексировать всё", ["UnstageAll"] = "Убрать всё из индекса",
        ["PendSortStatus"] = "Ожидающие файлы, отсортированные по состоянию",
        ["PendSortPath"] = "Ожидающие файлы, отсортированные по пути",
        ["PendSortName"] = "Ожидающие файлы, отсортированные по имени",
        ["CommitMsgWm"] = "Сообщение коммита", ["PushNow"] = "Сразу отправить изменения",
        ["Amend"] = "Исправить последний коммит",
        ["SearchWm"] = "Поиск по сообщениям, авторам или SHA…",
        ["Local"] = "Локальный", ["Remote"] = "Внешний", ["Clone"] = "Клонировать", ["Add"] = "Добавить",
        ["New"] = "Создать",
        ["LocalRepos"] = "Локальные репозитории", ["RemoteRepos"] = "Внешние репозитории",
        ["LocalHint"] = "Здесь показываются репозитории, которые вы открывали раньше. Двойной щелчок — открыть.",
        ["ShowInExplorer"] = "Показать в проводнике", ["OpenInTerminal"] = "Открыть в терминале",
        ["SourceUrl"] = "Исходный путь / URL", ["DestPath"] = "Целевой путь",
        ["NameLbl"] = "Название", ["Browse"] = "Обзор…", ["WorkingCopy"] = "Путь рабочей копии",
        ["CreateRepo"] = "Создать новый репозиторий", ["AddAccount"] = "Добавить учётную запись…",
        ["Remove"] = "Удалить",
        ["ParentFolderWm"] = "Папка, в которую клонировать", ["FolderNameWm"] = "Имя папки",
        ["RepoPathWm"] = "Путь к существующему репозиторию",
        ["NewRepoFolderWm"] = "Папка для нового репозитория",
        ["Checkout"] = "Переключиться", ["MergeInto"] = "Слить в текущую ветку",
        ["CopyBranchName"] = "Копировать имя ветки", ["DeleteDots"] = "Удалить…",
        ["Apply"] = "Применить", ["ApplyPop"] = "Применить и удалить (pop)",
        ["OpenRepository"] = "Открыть репозиторий", ["UpdateSubmodule"] = "Обновить (init + recursive)",
        ["CheckoutCommitDots"] = "Переключиться на этот коммит…", ["TagCommitDots"] = "Пометить этот коммит…",
        ["ResetSoftDots"] = "Сбросить текущую ветку на этот коммит (soft)…",
        ["ResetMixedDots"] = "Сбросить текущую ветку на этот коммит (mixed)…",
        ["ResetHardDots"] = "Сбросить текущую ветку на этот коммит (hard)…",
        ["RebaseDots"] = "Интерактивный rebase потомков этого коммита…",
        ["CopySha"] = "Копировать SHA",
        ["StageItem"] = "В индекс", ["UnstageItem"] = "Убрать из индекса",
        ["DiscardDots"] = "Отменить изменения…",
        ["OK"] = "ОК", ["Cancel"] = "Отмена", ["Yes"] = "Да", ["No"] = "Нет",
        ["Save"] = "Сохранить", ["Close"] = "Закрыть",
        ["Theme"] = "Тема", ["Language"] = "Язык",
        ["Dark"] = "Тёмная", ["Light"] = "Светлая"
    };
}
