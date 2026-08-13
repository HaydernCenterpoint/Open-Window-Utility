using OpenWindowUtility.Core.Catalog;
using OpenWindowUtility.Core.Jobs;
using OpenWindowUtility.Core.Operations;
using OpenWindowUtility.Core.Updates;

namespace OpenWindowUtility.Core.Features;

public sealed class FeatureEngine
{
    private readonly IOperationExecutor _executor;
    private readonly SystemInfoService _systemInfo;

    public FeatureEngine(IOperationExecutor executor, SystemInfoService systemInfo)
    {
        _executor = executor;
        _systemInfo = systemInfo;
    }

    public bool IsEditionSupported(FeatureEntry feature)
    {
        if (feature.Editions.Count == 0)
        {
            return true;
        }

        var edition = _systemInfo.GetSnapshot().EditionId;
        return feature.Editions.Any(x => x.Equals(edition, StringComparison.OrdinalIgnoreCase));
    }

    public async Task ApplyAsync(
        IReadOnlyList<FeatureEntry> features,
        IJobContext log,
        CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var feature in features)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            log.Progress?.Report(index / (double)features.Count);
            if (!IsEditionSupported(feature))
            {
                log.Warn($"Skipping {feature.Id}: not available on this Windows edition.");
                continue;
            }

            log.Info($"Applying {feature.Kind.ToString().ToLowerInvariant()} {feature.Id}");
            foreach (var operation in feature.Apply)
            {
                await _executor.ExecuteAsync(operation, log, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
