using System.Globalization;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using System.Text.Json;
using Microsoft.Win32;
using OpenWindowUtility.Core.Safety;

namespace OpenWindowUtility.Core.Operations;

public sealed class WindowsOperationExecutor : IOperationExecutor
{
    private readonly IProcessRunner _processRunner;
    private readonly RestorePointService _restorePoints;

    public WindowsOperationExecutor(IProcessRunner processRunner, RestorePointService restorePoints)
    {
        _processRunner = processRunner;
        _restorePoints = restorePoints;
    }

    public Task ExecuteAsync(Operation operation, IJobLog log, CancellationToken cancellationToken)
    {
        return operation switch
        {
            RegistrySetOperation op => ExecuteRegistrySetAsync(op, log),
            RegistryDeleteOperation op => ExecuteRegistryDeleteAsync(op, log),
            ServiceStartupOperation op => ExecuteServiceAsync(op, log),
            ScheduledTaskOperation op => ExecuteScheduledTaskAsync(op, log, cancellationToken),
            AppxRemoveOperation op => ExecuteAppxRemoveAsync(op, log, cancellationToken),
            ProcessOperation op => ExecuteProcessAsync(op, log, cancellationToken),
            DismFeatureOperation op => ExecuteDismAsync(op, log, cancellationToken),
            PowerPlanOperation op => ExecutePowerPlanAsync(op, log, cancellationToken),
            CreateRestorePointOperation op => ExecuteRestorePointAsync(op, log),
            _ => RejectUnknown(operation)
        };
    }

    private static Task RejectUnknown(Operation operation)
    {
        throw new InvalidOperationException($"Unsupported operation type '{operation.GetType().FullName}'.");
    }

    private static Task ExecuteRegistrySetAsync(RegistrySetOperation op, IJobLog log)
    {
        using var key = OpenOrCreate(op.Hive, op.Path);
        var kind = ParseKind(op.ValueKind);
        var value = ConvertValue(op.Value, kind);
        key.SetValue(op.Name, value, kind);
        log.Info($"Registry set {op.Hive}\\{op.Path}\\{op.Name}");
        return Task.CompletedTask;
    }

    private static Task ExecuteRegistryDeleteAsync(RegistryDeleteOperation op, IJobLog log)
    {
        var hive = OpenHive(op.Hive);
        using var key = hive.OpenSubKey(op.Path, writable: true);
        if (key is null)
        {
            log.Warn($"Registry path missing: {op.Hive}\\{op.Path}");
            return Task.CompletedTask;
        }

        key.DeleteValue(op.Name, throwOnMissingValue: false);
        log.Info($"Registry deleted {op.Hive}\\{op.Path}\\{op.Name}");
        return Task.CompletedTask;
    }

    private static Task ExecuteServiceAsync(ServiceStartupOperation op, IJobLog log)
    {
        var startValue = op.StartupType.ToLowerInvariant() switch
        {
            "automatic" => 2,
            "manual" => 3,
            "disabled" => 4,
            _ => throw new InvalidOperationException($"Unknown startup type '{op.StartupType}'.")
        };

        using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + op.ServiceName, writable: true)
            ?? throw new InvalidOperationException($"Service '{op.ServiceName}' was not found.");
        services.SetValue("Start", startValue, RegistryValueKind.DWord);

        using var controller = new ServiceController(op.ServiceName);
        if (op.Stop && controller.Status is not ServiceControllerStatus.Stopped and not ServiceControllerStatus.StopPending)
        {
            try
            {
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                log.Warn($"Could not stop {op.ServiceName}: {ex.Message}");
            }
        }

        if (op.Start && controller.Status is not ServiceControllerStatus.Running and not ServiceControllerStatus.StartPending)
        {
            try
            {
                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                log.Warn($"Could not start {op.ServiceName}: {ex.Message}");
            }
        }

        log.Info($"Service {op.ServiceName} startup -> {op.StartupType}");
        return Task.CompletedTask;
    }

    private async Task ExecuteScheduledTaskAsync(ScheduledTaskOperation op, IJobLog log, CancellationToken cancellationToken)
    {
        var flag = op.State.ToLowerInvariant() switch
        {
            "disable" => "/disable",
            "enable" => "/enable",
            _ => throw new InvalidOperationException($"Unknown task state '{op.State}'.")
        };

        var result = await _processRunner.RunAsync(
            "schtasks.exe",
            $"/change /tn \"{op.TaskPath}\" {flag}",
            cancellationToken).ConfigureAwait(false);
        LogProcess(log, "schtasks", result);
    }

    private async Task ExecuteAppxRemoveAsync(AppxRemoveOperation op, IJobLog log, CancellationToken cancellationToken)
    {
        if (!ProcessAllowlist.IsSafeAppxName(op.PackageName))
        {
            throw new InvalidOperationException($"Unsafe AppX package name '{op.PackageName}'.");
        }

        var command =
            $"Get-AppxPackage -Name '{op.PackageName}' -AllUsers | Remove-AppxPackage -AllUsers";
        var result = await _processRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"",
            cancellationToken).ConfigureAwait(false);
        LogProcess(log, "appx", result);
    }

    private async Task ExecuteProcessAsync(ProcessOperation op, IJobLog log, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(
            op.FileName,
            op.Arguments,
            cancellationToken,
            useShellExecute: op.UseShellExecute).ConfigureAwait(false);
        if (op.WaitForExit)
        {
            LogProcess(log, op.FileName, result);
        }
        else
        {
            log.Info($"Started {op.FileName} {op.Arguments}");
        }
    }

    private async Task ExecuteDismAsync(DismFeatureOperation op, IJobLog log, CancellationToken cancellationToken)
    {
        var action = op.Enable ? "/Enable-Feature" : "/Disable-Feature";
        var extra = op.Enable ? " /All" : "";
        var args = $"/Online {action} /FeatureName:{op.FeatureName}{extra} /NoRestart";
        var result = await _processRunner.RunAsync("dism.exe", args, cancellationToken).ConfigureAwait(false);
        LogProcess(log, "dism", result);
        if (result.ExitCode != 0 && result.ExitCode != 3010)
        {
            throw new InvalidOperationException($"DISM failed for '{op.FeatureName}' (exit {result.ExitCode}).");
        }
    }

    private async Task ExecutePowerPlanAsync(PowerPlanOperation op, IJobLog log, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync("powercfg.exe", $"/setactive {op.SchemeGuid}", cancellationToken)
            .ConfigureAwait(false);
        LogProcess(log, "powercfg", result);
    }

    private Task ExecuteRestorePointAsync(CreateRestorePointOperation op, IJobLog log)
    {
        if (_restorePoints.TryCreate(op.Description, out var message))
        {
            log.Info(message);
            return Task.CompletedTask;
        }

        throw new InvalidOperationException(message);
    }

    private static void LogProcess(IJobLog log, string name, ProcessResult result)
    {
        var output = result.StandardOutput.Trim();
        if (output.Length > 0)
        {
            log.Info(TrimLog(output));
        }

        var error = result.StandardError.Trim();
        if (error.Length > 0)
        {
            log.Warn(TrimLog(error));
        }

        if (result.TimedOut)
        {
            log.Error($"{name} timed out.");
            return;
        }

        if (result.ExitCode == 0 || result.ExitCode == 3010)
        {
            log.Info($"{name} finished (exit {result.ExitCode}).");
        }
        else
        {
            log.Error($"{name} failed (exit {result.ExitCode}).");
        }
    }

    private static string TrimLog(string text)
    {
        const int max = 4000;
        return text.Length <= max ? text : text[..max] + "…";
    }

    private static RegistryKey OpenHive(string hive)
    {
        return hive.ToUpperInvariant() switch
        {
            "HKLM" => Registry.LocalMachine,
            "HKCU" => Registry.CurrentUser,
            _ => throw new InvalidOperationException($"Unknown hive '{hive}'.")
        };
    }

    private static RegistryKey OpenOrCreate(string hive, string path)
    {
        var root = OpenHive(hive);
        return root.CreateSubKey(path, writable: true)
            ?? throw new InvalidOperationException($"Could not open {hive}\\{path}.");
    }

    private static RegistryValueKind ParseKind(string valueKind)
    {
        return valueKind.ToLowerInvariant() switch
        {
            "dword" => RegistryValueKind.DWord,
            "qword" => RegistryValueKind.QWord,
            "string" => RegistryValueKind.String,
            "expandstring" => RegistryValueKind.ExpandString,
            "multistring" => RegistryValueKind.MultiString,
            _ => throw new InvalidOperationException($"Unknown value kind '{valueKind}'.")
        };
    }

    private static object ConvertValue(JsonElement value, RegistryValueKind kind)
    {
        switch (kind)
        {
            case RegistryValueKind.DWord:
                return value.ValueKind == JsonValueKind.String
                    ? int.Parse(value.GetString()!, CultureInfo.InvariantCulture)
                    : value.GetInt32();
            case RegistryValueKind.QWord:
                return value.ValueKind == JsonValueKind.String
                    ? long.Parse(value.GetString()!, CultureInfo.InvariantCulture)
                    : value.GetInt64();
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
            case RegistryValueKind.MultiString:
                return value.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
            default:
                throw new SwitchExpressionException(kind);
        }
    }
}
