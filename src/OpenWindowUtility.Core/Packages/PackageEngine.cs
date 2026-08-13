using OpenWindowUtility.Core.Catalog;
using OpenWindowUtility.Core.Jobs;
using OpenWindowUtility.Core.Operations;

namespace OpenWindowUtility.Core.Packages;

public sealed class PackageEngine
{
    private readonly IProcessRunner _runner;

    public PackageEngine(IProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<bool> IsWingetAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner.RunAsync("winget", "--version", cancellationToken, TimeSpan.FromSeconds(20))
                .ConfigureAwait(false);
            return result.ExitCode == 0 && !result.TimedOut;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> IsChocolateyAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner.RunAsync("choco", "-v", cancellationToken, TimeSpan.FromSeconds(20))
                .ConfigureAwait(false);
            return result.ExitCode == 0 && !result.TimedOut;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<HashSet<string>> GetInstalledAppIdsAsync(
        IReadOnlyList<AppEntry> apps,
        CancellationToken cancellationToken)
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wingetList = "";
        var chocoList = "";

        if (await IsWingetAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            var result = await _runner.RunAsync(
                "winget",
                "list --accept-source-agreements --disable-interactivity",
                cancellationToken,
                TimeSpan.FromMinutes(2)).ConfigureAwait(false);
            wingetList = result.StandardOutput + Environment.NewLine + result.StandardError;
        }

        if (await IsChocolateyAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            var result = await _runner.RunAsync(
                "choco",
                "list --local-only --limit-output",
                cancellationToken,
                TimeSpan.FromMinutes(2)).ConfigureAwait(false);
            chocoList = result.StandardOutput;
        }

        foreach (var app in apps)
        {
            if (!string.IsNullOrWhiteSpace(app.Winget) && ContainsPackageId(wingetList, app.Winget))
            {
                installed.Add(app.Id);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(app.Chocolatey) && ContainsChocolateyId(chocoList, app.Chocolatey))
            {
                installed.Add(app.Id);
            }
        }

        return installed;
    }

    public async Task InstallAsync(
        IReadOnlyList<ResolvedPackage> packages,
        IJobContext log,
        CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var package in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            log.Progress?.Report(index / (double)packages.Count);
            log.Info($"Installing {package.PackageId} via {package.Manager} ({index}/{packages.Count})");

            ProcessResult result;
            if (package.Manager == "winget")
            {
                result = await _runner.RunAsync(
                    "winget",
                    $"install --id {package.PackageId} --exact --accept-package-agreements --accept-source-agreements --disable-interactivity",
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                result = await _runner.RunAsync(
                    "choco",
                    $"install {package.PackageId} -y --no-progress",
                    cancellationToken).ConfigureAwait(false);
            }

            WriteResult(log, result);
            if (result.ExitCode != 0 && result.ExitCode != -1978335189)
            {
                throw new InvalidOperationException($"Install failed for {package.PackageId} (exit {result.ExitCode}).");
            }
        }
    }

    public async Task UninstallAsync(
        IReadOnlyList<ResolvedPackage> packages,
        IJobContext log,
        CancellationToken cancellationToken)
    {
        foreach (var package in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            log.Info($"Uninstalling {package.PackageId} via {package.Manager}");
            ProcessResult result;
            if (package.Manager == "winget")
            {
                result = await _runner.RunAsync(
                    "winget",
                    $"uninstall --id {package.PackageId} --exact --disable-interactivity",
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                result = await _runner.RunAsync(
                    "choco",
                    $"uninstall {package.PackageId} -y --no-progress",
                    cancellationToken).ConfigureAwait(false);
            }

            WriteResult(log, result);
        }
    }

    public async Task UpgradeAllAsync(PackageManagerPreference preference, IJobContext log, CancellationToken cancellationToken)
    {
        var useChoco = preference == PackageManagerPreference.Chocolatey
            || (preference == PackageManagerPreference.Auto
                && !await IsWingetAvailableAsync(cancellationToken).ConfigureAwait(false));
        if (useChoco)
        {
            log.Info("Upgrading all Chocolatey packages");
            var result = await _runner.RunAsync("choco", "upgrade all -y --no-progress", cancellationToken)
                .ConfigureAwait(false);
            WriteResult(log, result);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Chocolatey upgrade all failed (exit {result.ExitCode}).");
            }

            return;
        }

        log.Info("Upgrading all WinGet packages");
        var winget = await _runner.RunAsync(
            "winget",
            "upgrade --all --accept-package-agreements --accept-source-agreements --disable-interactivity",
            cancellationToken).ConfigureAwait(false);
        WriteResult(log, winget);
        // ponytail: treat "nothing to update" as success; split codes if winget starts failing real upgrades
        if (winget.ExitCode != 0 && winget.ExitCode != -1978335189 && winget.ExitCode != -1978335212)
        {
            throw new InvalidOperationException($"WinGet upgrade all failed (exit {winget.ExitCode}).");
        }
    }

    public async Task<PackageDetails> GetDetailsAsync(string packageId, CancellationToken cancellationToken)
    {
        var fallback = PackageShowParser.PublisherFallback(packageId);
        if (!ProcessAllowlist.IsSafePackageId(packageId))
        {
            return new PackageDetails { Publisher = fallback };
        }

        try
        {
            var result = await _runner.RunAsync(
                "winget",
                $"show --id \"{packageId}\" --exact --disable-interactivity --accept-source-agreements",
                cancellationToken,
                TimeSpan.FromSeconds(40)).ConfigureAwait(false);
            var parsed = PackageShowParser.Parse(result.StandardOutput + Environment.NewLine + result.StandardError);
            return new PackageDetails
            {
                Publisher = string.IsNullOrWhiteSpace(parsed.Publisher) ? fallback : parsed.Publisher,
                Version = parsed.Version,
                ReleaseDate = parsed.ReleaseDate
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new PackageDetails { Publisher = fallback };
        }
    }

    private static void WriteResult(IJobContext log, ProcessResult result)
    {
        var text = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
        if (text.Length > 0)
        {
            log.Info(text.Length > 4000 ? text[..4000] + "…" : text);
        }
    }

    private static bool ContainsPackageId(string listOutput, string packageId)
    {
        using var reader = new StringReader(listOutput);
        while (reader.ReadLine() is { } line)
        {
            if (line.Contains(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsChocolateyId(string listOutput, string packageId)
    {
        using var reader = new StringReader(listOutput);
        while (reader.ReadLine() is { } line)
        {
            var id = line.Split('|')[0].Trim();
            if (id.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
