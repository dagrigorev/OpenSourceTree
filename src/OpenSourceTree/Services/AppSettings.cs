using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OpenSourceTree.Services;

/// <summary>A GitHub / GitLab / Bitbucket account used to browse remote repositories.</summary>
public sealed class HostingAccount
{
    public string Provider { get; set; } = "GitHub";
    public string Username { get; set; } = "";

    /// <summary>
    /// Personal access token (Bitbucket: app password); empty = anonymous (public repositories only).
    /// Never serialized — it lives in the OS credential store under <see cref="CredentialKey"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Token { get; set; } = "";

    /// <summary>Server base URL for self-hosted instances (GitLab); null = the public cloud host.</summary>
    public string? BaseUrl { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string Display => $"{Provider} — {Username}";

    [System.Text.Json.Serialization.JsonIgnore]
    public string CredentialKey => $"{Provider}:{Username}@{BaseUrl ?? "default"}";
}

public sealed class AppSettings
{
    private static AppSettings? _instance;

    /// <summary>Process-wide settings instance, loaded once on first access.</summary>
    public static AppSettings Instance => _instance ??= Load();

    public List<string> OpenRepositories { get; set; } = new();
    public List<string> RecentRepositories { get; set; } = new();
    public int ActiveTabIndex { get; set; }
    public string? DefaultCloneDirectory { get; set; }
    public double WindowWidth { get; set; } = 1400;
    public double WindowHeight { get; set; } = 860;

    // ----- Options dialog (Tools -> Options) -----

    /// <summary>Reopen the previously open repository tabs on startup.</summary>
    public bool RestoreTabsOnStartup { get; set; } = true;

    /// <summary>Maximum number of commits loaded into the history log.</summary>
    public int HistoryLimit { get; set; } = 3000;

    /// <summary>Unified-diff context lines shown around changes.</summary>
    public int DiffContextLines { get; set; } = 3;

    /// <summary>Explicit path to the git executable; null/empty = use PATH.</summary>
    public string? GitExecutablePath { get; set; }

    /// <summary>Hosting accounts shown in the New tab's Remote view.</summary>
    public List<HostingAccount> Accounts { get; set; } = new();

    private static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenSourceTree");

    private static string SettingsFile => Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new AppSettings();
        }
        catch
        {
            // fall through to defaults on corrupt settings
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // settings persistence is best-effort
        }
    }

    public void TouchRecent(string path)
    {
        RecentRepositories.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentRepositories.Insert(0, path);
        if (RecentRepositories.Count > 15)
            RecentRepositories.RemoveRange(15, RecentRepositories.Count - 15);
    }
}
