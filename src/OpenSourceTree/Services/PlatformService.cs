using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenSourceTree.Services;

public static class PlatformService
{
    public static void OpenFileExplorer(string directory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", $"\"{directory}\"");
        else
            Process.Start("xdg-open", $"\"{directory}\"");
    }

    public static void OpenSshFolder()
    {
        var ssh = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        System.IO.Directory.CreateDirectory(ssh);
        OpenFileExplorer(ssh);
    }

    /// <summary>Opens a terminal running ssh-keygen so the user can create a key interactively.</summary>
    public static void GenerateSshKey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/k ssh-keygen -t ed25519") { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("osascript",
                "-e \"tell application \\\"Terminal\\\" to do script \\\"ssh-keygen -t ed25519\\\"\" -e \"tell application \\\"Terminal\\\" to activate\"");
        }
        else
        {
            foreach (var term in new[] { "x-terminal-emulator", "gnome-terminal", "konsole", "xterm" })
            {
                try
                {
                    Process.Start(new ProcessStartInfo(term, "-e ssh-keygen -t ed25519") { UseShellExecute = false });
                    return;
                }
                catch
                {
                    // try next emulator
                }
            }
        }
    }

    /// <summary>Starts the OpenSSH agent (Windows service / background process elsewhere).</summary>
    public static void StartSshAgent()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("powershell.exe",
                "-NoProfile -Command \"Start-Service ssh-agent\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo("ssh-agent") { UseShellExecute = false });
        }
    }

    public static void OpenInEditor(string filePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{filePath}\"") { UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", $"-t \"{filePath}\"");
        else
            Process.Start("xdg-open", $"\"{filePath}\"");
    }

    public static void OpenTerminal(string directory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Prefer Windows Terminal, fall back to PowerShell.
            try
            {
                Process.Start(new ProcessStartInfo("wt.exe", $"-d \"{directory}\"") { UseShellExecute = true });
            }
            catch
            {
                Process.Start(new ProcessStartInfo("powershell.exe")
                {
                    WorkingDirectory = directory,
                    UseShellExecute = true
                });
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", $"-a Terminal \"{directory}\"");
        }
        else
        {
            foreach (var term in new[] { "x-terminal-emulator", "gnome-terminal", "konsole", "xterm" })
            {
                try
                {
                    Process.Start(new ProcessStartInfo(term) { WorkingDirectory = directory, UseShellExecute = false });
                    return;
                }
                catch
                {
                    // try next emulator
                }
            }
        }
    }
}
