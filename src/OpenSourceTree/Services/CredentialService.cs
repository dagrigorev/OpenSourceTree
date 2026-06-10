using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OpenSourceTree.Services;

/// <summary>
/// Stores hosting-account tokens in the operating system's credential store:
/// Windows Credential Manager, the macOS keychain (via `security`) or libsecret
/// (via `secret-tool`) on Linux. If none is available, falls back to an
/// obfuscated file next to settings.json.
/// </summary>
public static class CredentialService
{
    private const string Service = "OpenSourceTree";

    public static void Store(string key, string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            Delete(key);
            return;
        }
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { WinWrite($"{Service}:{key}", secret); return; }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { MacWrite(key, secret); return; }
            if (LinuxWrite(key, secret)) return;
        }
        catch
        {
            // fall through to the file store
        }
        FileWrite(key, secret);
    }

    public static string? Retrieve(string key)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return WinRead($"{Service}:{key}") ?? FileRead(key);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return MacRead(key) ?? FileRead(key);
            return LinuxRead(key) ?? FileRead(key);
        }
        catch
        {
            return FileRead(key);
        }
    }

    public static void Delete(string key)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) CredDeleteW($"{Service}:{key}", CredTypeGeneric, 0);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) RunQuiet("security", $"delete-generic-password -s \"{Service}\" -a \"{key}\"");
            else RunQuiet("secret-tool", $"clear service \"{Service}\" account \"{key}\"");
        }
        catch
        {
            // best-effort
        }
        FileDelete(key);
    }

    // ---------- Windows Credential Manager ----------

    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIALW
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWriteW(ref CREDENTIALW credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    private static void WinWrite(string target, string secret)
    {
        var targetPtr = Marshal.StringToCoTaskMemUni(target);
        var blobPtr = Marshal.StringToCoTaskMemUni(secret);
        try
        {
            var cred = new CREDENTIALW
            {
                Type = CredTypeGeneric,
                TargetName = targetPtr,
                CredentialBlob = blobPtr,
                CredentialBlobSize = (uint)(secret.Length * 2),
                Persist = CredPersistLocalMachine
            };
            if (!CredWriteW(ref cred, 0))
                throw new InvalidOperationException($"CredWrite failed ({Marshal.GetLastWin32Error()}).");
        }
        finally
        {
            Marshal.FreeCoTaskMem(targetPtr);
            Marshal.FreeCoTaskMem(blobPtr);
        }
    }

    private static string? WinRead(string target)
    {
        if (!CredReadW(target, CredTypeGeneric, 0, out var ptr))
            return null;
        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIALW>(ptr);
            return cred.CredentialBlobSize == 0
                ? ""
                : Marshal.PtrToStringUni(cred.CredentialBlob, (int)(cred.CredentialBlobSize / 2));
        }
        finally
        {
            CredFree(ptr);
        }
    }

    // ---------- macOS keychain ----------

    private static void MacWrite(string key, string secret) =>
        RunQuiet("security", $"add-generic-password -U -s \"{Service}\" -a \"{key}\" -w \"{secret}\"", throwOnError: true);

    private static string? MacRead(string key)
    {
        var output = RunCapture("security", $"find-generic-password -s \"{Service}\" -a \"{key}\" -w");
        return string.IsNullOrEmpty(output) ? null : output.TrimEnd('\n', '\r');
    }

    // ---------- Linux libsecret ----------

    private static bool LinuxWrite(string key, string secret)
    {
        var psi = new ProcessStartInfo("secret-tool",
            $"store --label=\"{Service}\" service \"{Service}\" account \"{key}\"")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        try
        {
            using var p = Process.Start(psi)!;
            p.StandardInput.Write(secret);
            p.StandardInput.Close();
            p.WaitForExit(10000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? LinuxRead(string key)
    {
        var output = RunCapture("secret-tool", $"lookup service \"{Service}\" account \"{key}\"");
        return string.IsNullOrEmpty(output) ? null : output.TrimEnd('\n', '\r');
    }

    // ---------- process helpers ----------

    private static void RunQuiet(string file, string args, bool throwOnError = false)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit(10000);
        if (throwOnError && p.ExitCode != 0)
            throw new InvalidOperationException($"{file} exited with {p.ExitCode}.");
    }

    private static string? RunCapture(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(10000);
            return p.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    // ---------- file fallback (base64-obfuscated, local only) ----------

    private static string FallbackFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenSourceTree", "credentials.json");

    private static Dictionary<string, string> FileLoad()
    {
        try
        {
            if (File.Exists(FallbackFile))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FallbackFile)) ?? new();
        }
        catch
        {
            // corrupt store: start over
        }
        return new();
    }

    private static void FileSave(Dictionary<string, string> map)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FallbackFile)!);
        File.WriteAllText(FallbackFile, JsonSerializer.Serialize(map));
    }

    private static void FileWrite(string key, string secret)
    {
        var map = FileLoad();
        map[key] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secret));
        FileSave(map);
    }

    private static string? FileRead(string key)
    {
        var map = FileLoad();
        if (!map.TryGetValue(key, out var b64))
            return null;
        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        }
        catch
        {
            return null;
        }
    }

    private static void FileDelete(string key)
    {
        var map = FileLoad();
        if (map.Remove(key))
            FileSave(map);
    }
}
