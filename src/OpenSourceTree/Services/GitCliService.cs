using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OpenSourceTree.Services;

/// <summary>
/// Network operations (fetch/pull/push/clone) shell out to the system git executable —
/// the same approach SourceTree uses — so credential helpers and SSH agents just work.
/// </summary>
public static class GitCliService
{
    private static string? _gitPath;

    /// <summary>Forget the cached git path (call after the Options dialog changes it).</summary>
    public static void ResetCache() => _gitPath = null;

    public static async Task<string?> GetVersionAsync()
    {
        try
        {
            string? version = null;
            await RunAsync(null, new[] { "--version" }, line =>
            {
                if (line.StartsWith("git version", StringComparison.OrdinalIgnoreCase))
                    version = line;
            }).ConfigureAwait(false);
            return version;
        }
        catch
        {
            return null;
        }
    }

    public static string? FindGit()
    {
        if (_gitPath is not null)
            return _gitPath;

        var custom = AppSettings.Instance.GitExecutablePath;
        if (!string.IsNullOrWhiteSpace(custom) && System.IO.File.Exists(custom))
        {
            _gitPath = custom;
            return _gitPath;
        }

        string probe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "git.exe" : "git";
        try
        {
            var psi = new ProcessStartInfo(probe, "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (!p!.WaitForExit(5000))
            {
                // A hung probe tells us nothing; treat git as unavailable rather than
                // reading ExitCode, which throws while the process is still running.
                try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return null;
            }
            if (p.ExitCode == 0)
            {
                _gitPath = probe;
                return _gitPath;
            }
        }
        catch
        {
            // not on PATH
        }
        return null;
    }

    /// <summary>
    /// Runs git with each argument passed separately, so values that contain spaces or quotes
    /// (URLs, branch and submodule paths) cannot break out into extra arguments.
    /// </summary>
    public static async Task<int> RunAsync(string? workingDirectory, string[] arguments,
        Action<string> onOutput)
    {
        var git = FindGit() ?? throw new InvalidOperationException(
            "git executable not found on PATH. Install git to use network operations.");

        var psi = new ProcessStartInfo(git)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        foreach (var a in arguments)
            psi.ArgumentList.Add(a);
        if (workingDirectory is not null)
            psi.WorkingDirectory = workingDirectory;
        // Never let git block on an interactive credential prompt inside a GUI app.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        onOutput($"$ git {string.Join(' ', arguments.Select(Quote))}");

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onOutput(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync().ConfigureAwait(false);
        onOutput(process.ExitCode == 0 ? "Completed successfully." : $"Exited with code {process.ExitCode}.");
        return process.ExitCode;
    }

    /// <summary>Renders an argument for the echoed command line only — never used to launch git.</summary>
    private static string Quote(string arg) =>
        arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '"', '\t' }) < 0 ? arg : $"\"{arg.Replace("\"", "\\\"")}\"";

    public static Task<int> FetchAsync(string repoDir, Action<string> onOutput) =>
        RunAsync(repoDir, new[] { "fetch", "--all", "--prune", "--progress" }, onOutput);

    public static Task<int> PullAsync(string repoDir, Action<string> onOutput) =>
        RunAsync(repoDir, new[] { "pull", "--progress" }, onOutput);

    public static Task<int> PushAsync(string repoDir, string? branch, Action<string> onOutput) =>
        RunAsync(repoDir, branch is null
            ? new[] { "push", "--progress" }
            : new[] { "push", "--progress", "-u", "origin", branch }, onOutput);

    public static Task<int> CloneAsync(string url, string targetDir, Action<string> onOutput) =>
        RunAsync(null, new[] { "clone", "--progress", "--", url, targetDir }, onOutput);
}
