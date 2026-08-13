using OpenWindowUtility.Core.Operations;

namespace OpenWindowUtility.Core.Cleanup;

public sealed class CleanupEngine
{
    private const int SampleLimit = 8;

    private readonly CleanupEnvironment _env;
    private readonly IRecycleBinQuery _recycle;

    public CleanupEngine(CleanupEnvironment? env = null, IRecycleBinQuery? recycle = null)
    {
        _env = env ?? CleanupEnvironment.Live();
        _recycle = recycle ?? new ShellRecycleBin();
    }

    public IReadOnlyList<JunkCategoryInfo> Categories => JunkCatalog.Categories;

    public async Task<IReadOnlyList<JunkScanHit>> ScanAsync(IJobLog log, CancellationToken cancellationToken)
    {
        var hits = new List<JunkScanHit>(JunkCatalog.Categories.Count);
        foreach (var category in JunkCatalog.Categories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            log.Info($"Scanning {category.Id}...");
            hits.Add(await Task.Run(() => ScanCategory(category.Kind, cancellationToken), cancellationToken)
                .ConfigureAwait(false));
        }

        return hits;
    }

    public async Task<JunkCleanResult> CleanAsync(
        IReadOnlyList<JunkKind> kinds,
        IJobLog log,
        CancellationToken cancellationToken)
    {
        var deleted = 0;
        var skipped = 0;
        var bytes = 0L;
        foreach (var kind in kinds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = JunkCatalog.Categories.Single(x => x.Kind == kind).Id;
            log.Info($"Cleaning {id}...");
            var result = await Task.Run(() => CleanCategory(kind, log, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            deleted += result.DeletedFiles;
            skipped += result.SkippedFiles;
            bytes += result.DeletedBytes;
        }

        log.Info($"Removed {deleted} files ({CleanupFormatter.FormatBytes(bytes)}); skipped {skipped} in use.");
        return new JunkCleanResult
        {
            DeletedFiles = deleted,
            SkippedFiles = skipped,
            DeletedBytes = bytes
        };
    }

    private JunkScanHit ScanCategory(JunkKind kind, CancellationToken cancellationToken)
    {
        if (kind == JunkKind.RecycleBin)
        {
            var (recycleBytes, recycleCount) = _recycle.Query();
            return new JunkScanHit
            {
                Kind = kind,
                Bytes = Math.Max(0, recycleBytes),
                FileCount = Math.Max(0, recycleCount),
                Samples = []
            };
        }

        long bytes = 0;
        var count = 0;
        var samples = new List<string>(SampleLimit);
        foreach (var file in EnumerateFiles(kind, cancellationToken))
        {
            try
            {
                bytes += new FileInfo(file).Length;
                count++;
                if (samples.Count < SampleLimit)
                {
                    samples.Add(file);
                }
            }
            catch (Exception)
            {
                // Size/count must ignore files that vanish mid-scan.
            }
        }

        return new JunkScanHit
        {
            Kind = kind,
            Bytes = bytes,
            FileCount = count,
            Samples = samples
        };
    }

    private JunkCleanResult CleanCategory(JunkKind kind, IJobLog log, CancellationToken cancellationToken)
    {
        if (kind == JunkKind.RecycleBin)
        {
            try
            {
                var before = _recycle.Query();
                _recycle.Empty();
                return new JunkCleanResult
                {
                    DeletedFiles = Math.Max(0, before.Count),
                    SkippedFiles = 0,
                    DeletedBytes = Math.Max(0, before.Bytes)
                };
            }
            catch (Exception ex)
            {
                log.Warn($"Recycle Bin: {ex.Message}");
                return new JunkCleanResult { DeletedFiles = 0, SkippedFiles = 0, DeletedBytes = 0 };
            }
        }

        var deleted = 0;
        var skipped = 0;
        var bytes = 0L;
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in EnumerateFiles(kind, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!JunkPathGuard.CanDelete(file, kind, _env))
            {
                skipped++;
                continue;
            }

            try
            {
                var info = new FileInfo(file);
                var length = info.Exists ? info.Length : 0;
                if (info.Exists)
                {
                    info.Attributes = FileAttributes.Normal;
                    info.Delete();
                }

                deleted++;
                bytes += length;
                var dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    directories.Add(dir);
                }
            }
            catch (Exception)
            {
                skipped++;
            }
        }

        if (kind == JunkKind.WindowsOld)
        {
            TryDeleteWindowsOld(log, ref deleted, ref skipped);
        }
        else
        {
            RemoveEmptyDirectories(kind, directories);
        }

        return new JunkCleanResult
        {
            DeletedFiles = deleted,
            SkippedFiles = skipped,
            DeletedBytes = bytes
        };
    }

    private IEnumerable<string> EnumerateFiles(JunkKind kind, CancellationToken cancellationToken)
    {
        foreach (var root in JunkCatalog.RootsFor(kind, _env))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(root.Path))
            {
                if (JunkPathGuard.CanDelete(root.Path, kind, _env))
                {
                    yield return root.Path;
                }

                continue;
            }

            if (!Directory.Exists(root.Path) || CleanupEnvironment.IsReparsePoint(root.Path))
            {
                continue;
            }

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = root.Recurse,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                MaxRecursionDepth = root.Recurse ? 16 : 0
            };

            foreach (var pattern in root.Patterns)
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(root.Path, pattern, options);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (JunkPathGuard.CanDelete(file, kind, _env))
                    {
                        yield return file;
                    }
                }
            }
        }
    }

    private void TryDeleteWindowsOld(IJobLog log, ref int deleted, ref int skipped)
    {
        var path = Path.Combine(_env.SystemDrive, "Windows.old");
        if (!Directory.Exists(path) || !JunkPathGuard.CanDelete(path, JunkKind.WindowsOld, _env))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
            deleted++;
        }
        catch (Exception ex)
        {
            skipped++;
            log.Warn($"Windows.old still present: {ex.Message}");
        }
    }

    private void RemoveEmptyDirectories(JunkKind kind, HashSet<string> directories)
    {
        foreach (var dir in directories.OrderByDescending(x => x.Length))
        {
            if (!JunkPathGuard.CanDelete(dir, kind, _env))
            {
                continue;
            }

            try
            {
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir, recursive: false);
                }
            }
            catch (Exception)
            {
                // Empty-dir cleanup is best-effort.
            }
        }
    }
}
