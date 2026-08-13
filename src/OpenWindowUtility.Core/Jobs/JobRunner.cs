namespace OpenWindowUtility.Core.Jobs;

public sealed class JobLogEvent
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }

    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Level}: {Message}";
}

public sealed class JobResult
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
}

public interface IJobContext : Operations.IJobLog
{
    IProgress<double>? Progress { get; }
}

public sealed class JobRunner
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _current;

    public event EventHandler<JobLogEvent>? Logged;
    public bool IsBusy => _gate.CurrentCount == 0;

    public void Cancel() => _current?.Cancel();

    public async Task<JobResult> RunAsync(
        string title,
        Func<IJobContext, CancellationToken, Task> work,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _current = cts;
        var context = new Context(this, progress);
        try
        {
            context.Info($"Started: {title}");
            await work(context, cts.Token).ConfigureAwait(false);
            context.Info($"Finished: {title}");
            return new JobResult { Success = true };
        }
        catch (OperationCanceledException)
        {
            context.Warn($"Cancelled: {title}");
            return new JobResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            context.Error($"{title} failed: {ex.Message}");
            return new JobResult { Success = false, Error = ex.Message };
        }
        finally
        {
            _current = null;
            cts.Dispose();
            _gate.Release();
        }
    }

    private sealed class Context : IJobContext
    {
        private readonly JobRunner _owner;

        public Context(JobRunner owner, IProgress<double>? progress)
        {
            _owner = owner;
            Progress = progress;
        }

        public IProgress<double>? Progress { get; }

        public void Info(string message) => Emit("INFO", message);
        public void Warn(string message) => Emit("WARN", message);
        public void Error(string message) => Emit("ERROR", message);

        private void Emit(string level, string message)
        {
            var evt = new JobLogEvent
            {
                Timestamp = DateTimeOffset.Now,
                Level = level,
                Message = message
            };
            _owner.Logged?.Invoke(_owner, evt);
            try
            {
                AppPaths.EnsureCreated();
                var file = Path.Combine(AppPaths.Logs, $"owu-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, evt + Environment.NewLine);
            }
            catch (Exception)
            {
                // Logging must never fail the job.
            }
        }
    }
}
