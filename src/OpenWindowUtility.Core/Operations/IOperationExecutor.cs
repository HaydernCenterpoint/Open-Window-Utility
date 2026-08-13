namespace OpenWindowUtility.Core.Operations;

public interface IJobLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

public sealed class ProcessResult
{
    public required int ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
    public bool TimedOut { get; init; }
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null,
        bool useShellExecute = false);
}

public interface IOperationExecutor
{
    Task ExecuteAsync(Operation operation, IJobLog log, CancellationToken cancellationToken);
}
