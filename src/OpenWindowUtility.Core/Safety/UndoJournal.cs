using System.Text.Json;
using OpenWindowUtility.Core.Operations;

namespace OpenWindowUtility.Core.Safety;

public sealed class JournalEntry
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required DateTimeOffset AppliedAt { get; init; }
    public List<string> ItemIds { get; init; } = [];
    public List<Operation> Undo { get; init; } = [];
}

public sealed class UndoJournal
{
    private readonly string _path;
    private readonly object _gate = new();

    public UndoJournal(string? path = null)
    {
        _path = path ?? AppPaths.JournalFile;
    }

    public void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            var all = Read();
            all.Add(entry);
            Write(all);
        }
    }

    public List<JournalEntry> Read()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<JournalEntry>>(json, JsonOptions.Default) ?? [];
        }
    }

    public List<JournalEntry> TakeBySource(string source)
    {
        lock (_gate)
        {
            var all = ReadUnlocked();
            var matched = all.Where(x => string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase)).ToList();
            var remaining = all.Where(x => !string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase)).ToList();
            WriteUnlocked(remaining);
            return matched;
        }
    }

    public List<JournalEntry> TakeByItemIds(IReadOnlyCollection<string> itemIds)
    {
        var set = itemIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            var all = ReadUnlocked();
            var matched = all.Where(x => x.ItemIds.Any(set.Contains)).ToList();
            var remaining = all.Except(matched).ToList();
            WriteUnlocked(remaining);
            return matched;
        }
    }

    private List<JournalEntry> ReadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<JournalEntry>>(json, JsonOptions.Default) ?? [];
    }

    private void Write(List<JournalEntry> entries) => WriteUnlocked(entries);

    private void WriteUnlocked(List<JournalEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(entries, JsonOptions.Default));
    }
}
