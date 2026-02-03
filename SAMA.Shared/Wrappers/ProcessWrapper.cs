using System.Diagnostics;

namespace SAMA.Shared.Wrappers;

/// <summary>
/// Wrapper around System.Diagnostics.Process with virtual methods for testability.
/// </summary>
public class ProcessWrapper : IDisposable
{
    private Process? _process;
    private bool _disposedValue;

    /// <summary>
    /// Starts the process using the provided ProcessStartInfo.
    /// </summary>
    public virtual void Start(ProcessStartInfo startInfo)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);

        _process = new Process { StartInfo = startInfo };
        _process.Start();
    }

    /// <summary>
    /// Waits asynchronously for the process to exit.
    /// </summary>
    public virtual async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ArgumentNullException.ThrowIfNull(_process, nameof(_process));

        await _process.WaitForExitAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the exit code of the process.
    /// </summary>
    public virtual int ExitCode
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            ArgumentNullException.ThrowIfNull(_process, nameof(_process));

            return _process.ExitCode;
        }
    }

    /// <summary>
    /// Reads all text from the standard output stream asynchronously.
    /// </summary>
    public virtual async Task<string> ReadStandardOutputAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ArgumentNullException.ThrowIfNull(_process, nameof(_process));

        return await _process.StandardOutput.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Reads all text from the standard error stream asynchronously.
    /// </summary>
    public virtual async Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ArgumentNullException.ThrowIfNull(_process, nameof(_process));

        return await _process.StandardError.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Kills the process and its child processes.
    /// </summary>
    public virtual void Kill()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ArgumentNullException.ThrowIfNull(_process, nameof(_process));

        _process.Kill(entireProcessTree: true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _process?.Dispose();
                _process = null;
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
