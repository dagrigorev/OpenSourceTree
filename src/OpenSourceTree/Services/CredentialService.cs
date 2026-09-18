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
/// (via `secret-tool`) on Linux. If none is available, falls back to a file next to
/// settings.json — DPAPI-encrypted on Windows, owner-only and base64-encoded elsewhere.
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
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) RunQuiet("security", "delete-generic-password", "-s", Service, "-a", key);
            else RunQuiet("secret-tool", "clear", "service", Service, "account", key);
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

    /// <summary>
    /// Writes through `security`, feeding the secret on standard input. Passing it as `-w &lt;secret&gt;`
    /// would expose the token to every local user through the process list.
    /// </summary>
    private static void MacWrite(string key, string secret)
    {
        // `-w` without a value makes security read the password data from stdin.
        // security asks for the password twice when it reads interactively; the extra copy is
        // harmless when it only reads once, and stdin is closed straight after.
        if (!RunWithSecretOnStdin("security", secret + "\n" + secret + "\n",
                "add-generic-password", "-U", "-s", Service, "-a", key, "-w"))
            throw new InvalidOperationException("security add-generic-password failed.");
    }

    private static string? MacRead(string key)
    {
        var output = RunCapture("security", "find-generic-password", "-s", Service, "-a", key, "-w");
        return string.IsNullOrEmpty(output) ? null : output.TrimEnd('\n', '\r');
    }

    // ---------- Linux libsecret ----------

    private static bool LinuxWrite(string key, string secret) =>
        RunWithSecretOnStdin("secret-tool", secret,
            "store", "--label=" + Service, "service", Service, "account", key);

    private static string? LinuxRead(string key)
    {
        var output = RunCapture("secret-tool", "lookup", "service", Service, "account", key);
        return string.IsNullOrEmpty(output) ? null : output.TrimEnd('\n', '\r');
    }

    // ---------- process helpers ----------

    // Arguments always go through ArgumentList: the runtime escapes each one for the
    // platform, so a key or secret containing quotes cannot break out into extra arguments.
    private static ProcessStartInfo Psi(string file, string[] args)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        return psi;
    }

    private const int ProcessTimeoutMs = 10000;

    private static void RunQuiet(string file, params string[] args)
    {
        using var p = Process.Start(Psi(file, args))!;
        if (!p.WaitForExit(ProcessTimeoutMs))
            TryKill(p);
    }

    /// <summary>Runs a helper, feeding <paramref name="stdin"/> to it; true when it exited with 0.</summary>
    private static bool RunWithSecretOnStdin(string file, string stdin, params string[] args)
    {
        var psi = Psi(file, args);
        psi.RedirectStandardInput = true;
        try
        {
            using var p = Process.Start(psi)!;
            p.StandardInput.Write(stdin);
            p.StandardInput.Close();
            if (!p.WaitForExit(ProcessTimeoutMs))
            {
                TryKill(p);
                return false;
            }
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryKill(Process p)
    {
        try { p.Kill(entireProcessTree: true); }
        catch { /* already gone */ }
    }

    private static string? RunCapture(string file, params string[] args)
    {
        try
        {
            using var p = Process.Start(Psi(file, args))!;
            string output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(ProcessTimeoutMs))
            {
                TryKill(p);
                return null;
            }
            return p.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    // ---------- file fallback (DPAPI on Windows, owner-only file elsewhere) ----------

    /// <summary>Marks a value that was encrypted with DPAPI rather than merely base64-encoded.</summary>
    private const string DpapiPrefix = "dpapi:";

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DATA_BLOB input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DATA_BLOB output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DATA_BLOB output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public uint cbData;
        public IntPtr pbData;
    }

    private static byte[]? DpapiTransform(byte[] data, bool protect)
    {
        var handle = Marshal.AllocHGlobal(data.Length);
        var input = new DATA_BLOB { cbData = (uint)data.Length, pbData = handle };
        var output = default(DATA_BLOB);
        try
        {
            Marshal.Copy(data, 0, handle, data.Length);
            bool ok = protect
                ? CryptProtectData(ref input, Service, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output);
            if (!ok)
                return null;
            var result = new byte[output.cbData];
            Marshal.Copy(output.pbData, result, 0, result.Length);
            return result;
        }
        catch
        {
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(handle);
            if (output.pbData != IntPtr.Zero)
                LocalFree(output.pbData);
        }
    }

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
        RestrictToOwner(FallbackFile);
    }

    /// <summary>Keeps the fallback store readable by its owner only (no-op on Windows, where ACLs apply).</summary>
    private static void RestrictToOwner(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // best-effort
        }
    }

    private static void FileWrite(string key, string secret)
    {
        var map = FileLoad();
        var plain = System.Text.Encoding.UTF8.GetBytes(secret);
        string? stored = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var sealed_ = DpapiTransform(plain, protect: true);
            if (sealed_ is not null)
                stored = DpapiPrefix + Convert.ToBase64String(sealed_);
        }
        map[key] = stored ?? Convert.ToBase64String(plain);
        FileSave(map);
    }

    private static string? FileRead(string key)
    {
        var map = FileLoad();
        if (!map.TryGetValue(key, out var value))
            return null;
        try
        {
            if (value.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                var plain = DpapiTransform(Convert.FromBase64String(value[DpapiPrefix.Length..]), protect: false);
                return plain is null ? null : System.Text.Encoding.UTF8.GetString(plain);
            }
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
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
