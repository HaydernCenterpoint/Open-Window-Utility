using System.Diagnostics;
using System.IO;
using OpenWindowUtility.Core;

namespace OpenWindowUtility.App.Services;

internal static class AppUpdateApplier
{
    public static void ApplyAndRestart(string downloadedExe)
    {
        var current = Environment.ProcessPath
                      ?? Path.Combine(AppContext.BaseDirectory, "OpenWindowUtility.exe");
        var script = Path.Combine(AppPaths.Updates, "apply.cmd");
        var pid = Environment.ProcessId;
        File.WriteAllText(script, $"""
            @echo off
            ping 127.0.0.1 -n 2 >nul
            :wait
            tasklist /FI "PID eq {pid}" | find "{pid}" >nul
            if not errorlevel 1 (
              ping 127.0.0.1 -n 2 >nul
              goto wait
            )
            copy /Y "{downloadedExe}" "{current}"
            start "" "{current}"
            del "%~f0"
            """);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        System.Windows.Application.Current.Shutdown();
    }
}
