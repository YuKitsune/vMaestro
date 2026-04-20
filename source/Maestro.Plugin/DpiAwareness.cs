using Microsoft.Win32;

namespace Maestro.Plugin;

public static class DpiAwareness
{
    public static void EnsureDpiAwareness()
    {
        if (!TryGetVatSysExecutablePath(out var vatSysExecutablePath))
            return;

        const string registryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        const string dpiValue = "DPIUNAWARE";

        using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: false);
        var existingValue = key?.GetValue(vatSysExecutablePath) as string;

        // If already set, exit early
        if (existingValue != null && existingValue.Contains(dpiValue))
            return;

        // Set the registry key
        using var writableKey = Registry.CurrentUser.OpenSubKey(registryPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(registryPath);

        writableKey.SetValue(vatSysExecutablePath, dpiValue, RegistryValueKind.String);

        // Restart vatSys to apply the DPI setting
        RestartVatSys();
    }

    static void RestartVatSys()
    {
        if (!TryGetVatSysExecutablePath(out var vatSysExecutablePath))
            return;

        System.Diagnostics.Process.Start(vatSysExecutablePath);
        Environment.Exit(0);
    }

    static bool TryGetVatSysInstallationPath(out string? installationPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Sawbe\vatSys");
        installationPath = key?.GetValue("Path") as string;
        return !string.IsNullOrEmpty(installationPath) && Directory.Exists(installationPath);
    }

    static bool TryGetVatSysExecutablePath(out string? executablePath)
    {
        try
        {
            if (!TryGetVatSysInstallationPath(out var installationPath))
            {
                executablePath = null;
                return false;
            }

            executablePath = Path.Combine(installationPath, "bin", "vatSys.exe");
            return File.Exists(executablePath);
        }
        catch
        {
            executablePath = null;
            return false;
        }
    }
}
