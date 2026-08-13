using OpenWindowUtility.Core.Catalog;
using OpenWindowUtility.Core.Jobs;
using OpenWindowUtility.Core.Operations;
using OpenWindowUtility.Core.Safety;

namespace OpenWindowUtility.Core.Tweaks;

public sealed class TweakEngine
{
    private readonly IOperationExecutor _executor;
    private readonly UndoJournal _journal;
    private readonly RestorePointService _restorePoints;

    public TweakEngine(IOperationExecutor executor, UndoJournal journal, RestorePointService restorePoints)
    {
        _executor = executor;
        _journal = journal;
        _restorePoints = restorePoints;
    }

    public async Task ApplyAsync(
        IReadOnlyList<TweakEntry> tweaks,
        bool createRestorePoint,
        IJobContext log,
        CancellationToken cancellationToken)
    {
        if (tweaks.Count == 0)
        {
            return;
        }

        if (createRestorePoint)
        {
            if (!_restorePoints.TryCreate("Open Window Utility tweaks", out var message))
            {
                throw new InvalidOperationException(message);
            }

            log.Info(message);
        }
        else
        {
            log.Warn("Restore point skipped by user setting.");
        }

        var undo = new List<Operation>();
        var ids = new List<string>();
        var index = 0;
        foreach (var tweak in tweaks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            log.Progress?.Report(index / (double)tweaks.Count);
            log.Info($"Applying tweak {tweak.Id}");
            foreach (var operation in tweak.Apply)
            {
                await _executor.ExecuteAsync(operation, log, cancellationToken).ConfigureAwait(false);
            }

            undo.AddRange(tweak.Undo);
            ids.Add(tweak.Id);
        }

        _journal.Append(new JournalEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Source = "tweaks",
            AppliedAt = DateTimeOffset.Now,
            ItemIds = ids,
            Undo = undo
        });
    }

    public async Task UndoAsync(
        IReadOnlyList<string> tweakIds,
        IJobContext log,
        CancellationToken cancellationToken)
    {
        var entries = _journal.TakeByItemIds(tweakIds);
        if (entries.Count == 0)
        {
            log.Warn("No journaled undo operations for the selected tweaks.");
            return;
        }

        foreach (var entry in entries)
        {
            foreach (var operation in entry.Undo)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _executor.ExecuteAsync(operation, log, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
