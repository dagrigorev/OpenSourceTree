using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenSourceTree.Services;

public static class PlatformService
{
    /// <summary>
    /// Builds a start info with each argument passed separately; hand-quoting a path into a
    /// single argument string breaks on quotes and on paths the user did not type themselves.
    /// </summary>
    private static ProcessStartInfo Psi(string file, bool shellExecute, params string[] args)
    {
        var psi = new ProcessStartInfo(file) { UseShellExecute = shellExecute };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        return psi;
    }

    public static void OpenFileExplorer(string directory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(Psi("explorer.exe", true, directory));
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start(Psi("open", false, directory));
        else
            Process.Start(Psi("xdg-open", false, directory));
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
                    Process.Start(Psi(term, false, "-e", "ssh-keygen", "-t", "ed25519"));
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
            Process.Start(Psi("notepad.exe", true, filePath));
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start(Psi("open", false, "-t", filePath));
        else
            Process.Start(Psi("xdg-open", false, filePath));
    }

    public static void OpenTerminal(string directory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Prefer Windows Terminal, fall back to PowerShell.
            try
            {
                Process.Start(Psi("wt.exe", true, "-d", directory));
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
            Process.Start(Psi("open", false, "-a", "Terminal", directory));
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
