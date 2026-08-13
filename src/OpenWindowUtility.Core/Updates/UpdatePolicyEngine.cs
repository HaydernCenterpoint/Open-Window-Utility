using Microsoft.Win32;
using OpenWindowUtility.Core.Operations;
using OpenWindowUtility.Core.Safety;

namespace OpenWindowUtility.Core.Updates;

public enum UpdatePolicyKind
{
    Default,
    Security,
    DisableAll
}

public sealed class UpdatePolicyEngine
{
    public const string JournalSource = "update-policy";
    private const string PolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    private const string AuPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

    private readonly IOperationExecutor _executor;
    private readonly UndoJournal _journal;

    public UpdatePolicyEngine(IOperationExecutor executor, UndoJournal journal)
    {
        _executor = executor;
        _journal = journal;
    }

    public IReadOnlyList<Operation> BuildApply(UpdatePolicyKind kind)
    {
        return kind switch
        {
            UpdatePolicyKind.Security => BuildSecurity(),
            UpdatePolicyKind.DisableAll => BuildDisableAll(),
            UpdatePolicyKind.Default => [],
            _ => throw new InvalidOperationException($"Unknown update policy '{kind}'.")
        };
    }

    public IReadOnlyList<Operation> BuildUndo(UpdatePolicyKind kind)
    {
        return kind switch
        {
            UpdatePolicyKind.Security => BuildSecurityUndo(),
            UpdatePolicyKind.DisableAll => BuildDisableAllUndo(),
            UpdatePolicyKind.Default => [],
            _ => throw new InvalidOperationException($"Unknown update policy '{kind}'.")
        };
    }

    public async Task ApplyAsync(
        UpdatePolicyKind kind,
        Operations.IJobLog log,
        CancellationToken cancellationToken)
    {
        if (kind == UpdatePolicyKind.Default)
        {
            var previous = _journal.TakeBySource(JournalSource);
            if (previous.Count == 0)
            {
                foreach (var operation in BuildSecurityUndo().Concat(BuildDisableAllUndo()))
                {
                    await _executor.ExecuteAsync(operation, log, cancellationToken).ConfigureAwait(false);
                }

                log.Info("Restored Windows Update defaults for keys owned by Open Window Utility.");
                return;
            }

            foreach (var entry in previous)
            {
                foreach (var operation in entry.Undo)
                {
                    await _executor.ExecuteAsync(operation, log, cancellationToken).ConfigureAwait(false);
                }
            }

            return;
        }

        var apply = BuildApply(kind);
        foreach (var operation in apply)
        {
            await _executor.ExecuteAsync(operation, log, cancellationToken).ConfigureAwait(false);
        }

        _journal.Append(new JournalEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Source = JournalSource,
            AppliedAt = DateTimeOffset.Now,
            ItemIds = [kind.ToString()],
            Undo = BuildUndo(kind).ToList()
        });
    }

    public string ReadCurrentLabel()
    {
        using var policy = Registry.LocalMachine.OpenSubKey(PolicyPath);
        using var au = Registry.LocalMachine.OpenSubKey(AuPath);
        var noAuto = GetDword(au, "NoAutoUpdate");
        if (noAuto == 1)
        {
            return "Disabled";
        }

        var deferFeature = GetDword(policy, "DeferFeatureUpdatesPeriodInDays");
        var deferQuality = GetDword(policy, "DeferQualityUpdatesPeriodInDays");
        if (deferFeature == 365 && deferQuality == 4)
        {
            return "Security";
        }

        return "Default";
    }

    private static int? GetDword(RegistryKey? key, string name)
    {
        if (key?.GetValue(name) is int value)
        {
            return value;
        }

        return null;
    }

    private static List<Operation> BuildSecurity() =>
    [
        Dword(PolicyPath, "ExcludeWUDriversInQualityUpdate", 1),
        Dword(PolicyPath, "DeferFeatureUpdates", 1),
        Dword(PolicyPath, "DeferFeatureUpdatesPeriodInDays", 365),
        Dword(PolicyPath, "DeferQualityUpdates", 1),
        Dword(PolicyPath, "DeferQualityUpdatesPeriodInDays", 4),
        Dword(AuPath, "NoAutoRebootWithLoggedOnUsers", 1),
        Dword(AuPath, "NoAutoUpdate", 0)
    ];

    private static List<Operation> BuildSecurityUndo() =>
    [
        Delete(PolicyPath, "ExcludeWUDriversInQualityUpdate"),
        Delete(PolicyPath, "DeferFeatureUpdates"),
        Delete(PolicyPath, "DeferFeatureUpdatesPeriodInDays"),
        Delete(PolicyPath, "DeferQualityUpdates"),
        Delete(PolicyPath, "DeferQualityUpdatesPeriodInDays"),
        Delete(AuPath, "NoAutoRebootWithLoggedOnUsers")
    ];

    private static List<Operation> BuildDisableAll() =>
    [
        Dword(AuPath, "NoAutoUpdate", 1),
        new ServiceStartupOperation
        {
            ServiceName = "wuauserv",
            StartupType = "disabled",
            Stop = true
        }
    ];

    private static List<Operation> BuildDisableAllUndo() =>
    [
        Delete(AuPath, "NoAutoUpdate"),
        new ServiceStartupOperation
        {
            ServiceName = "wuauserv",
            StartupType = "manual",
            Start = true
        }
    ];

    private static RegistrySetOperation Dword(string path, string name, int value) =>
        new()
        {
            Hive = "HKLM",
            Path = path,
            Name = name,
            ValueKind = "dword",
            Value = System.Text.Json.JsonSerializer.SerializeToElement(value)
        };

    private static RegistryDeleteOperation Delete(string path, string name) =>
        new()
        {
            Hive = "HKLM",
            Path = path,
            Name = name
        };
}
