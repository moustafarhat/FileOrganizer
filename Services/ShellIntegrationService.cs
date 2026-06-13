using System;
using System.Runtime.Versioning;

namespace FileOrganizer.Services;

public static class ShellIntegrationService
{
    private const string KeyName = "OrganizeWithFileOrganizer";
    private const string MenuLabel = "Organize with File Organizer";

    public static bool IsSupported => OperatingSystem.IsWindows();

    public static bool IsRegistered()
    {
        if (!OperatingSystem.IsWindows()) return false;
        return WindowsCheckRegistered();
    }

    public static void Register()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Shell integration is currently Windows-only.");
        WindowsRegister();
    }

    public static void Unregister()
    {
        if (!OperatingSystem.IsWindows()) return;
        WindowsUnregister();
    }

    [SupportedOSPlatform("windows")]
    private static bool WindowsCheckRegistered()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Classes\Directory\shell\{KeyName}");
        return key is not null;
    }

    [SupportedOSPlatform("windows")]
    private static void WindowsRegister()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine executable path.");

        WriteVerb(@"Software\Classes\Directory\shell\" + KeyName, exePath, useV: false);
        WriteVerb(@"Software\Classes\Directory\Background\shell\" + KeyName, exePath, useV: true);
    }

    [SupportedOSPlatform("windows")]
    private static void WriteVerb(string subpath, string exePath, bool useV)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(subpath, writable: true);
        key.SetValue(string.Empty, MenuLabel);
        key.SetValue("Icon", exePath);
        using var cmd = key.CreateSubKey("command", writable: true);
        var placeholder = useV ? "%V" : "%1";
        cmd.SetValue(string.Empty, $"\"{exePath}\" \"{placeholder}\"");
    }

    [SupportedOSPlatform("windows")]
    private static void WindowsUnregister()
    {
        try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\" + KeyName, throwOnMissingSubKey: false); } catch { }
        try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\Background\shell\" + KeyName, throwOnMissingSubKey: false); } catch { }
    }
}
