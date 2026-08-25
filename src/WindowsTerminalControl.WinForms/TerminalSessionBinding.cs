using WindowsTerminalControl.Core;

namespace WindowsTerminalControl.WinForms;

/// <summary>
/// Connects a <see cref="TerminalControl"/> to an <see cref="ITerminalSession"/>.
/// </summary>
public sealed class TerminalSessionBinding : IDisposable
{
    private readonly ITerminalSession _session;
    private readonly TerminalControl _terminal;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalSessionBinding"/> class.
    /// </summary>
    /// <param name="terminal">The terminal renderer.</param>
    /// <param name="session">The terminal transport.</param>
    public TerminalSessionBinding(TerminalControl terminal, ITerminalSession session)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        ArgumentNullException.ThrowIfNull(session);

        _terminal = terminal;
        _session = session;

        _terminal.UserInput += OnTerminalUserInput;
        _terminal.DimensionsChanged += OnTerminalDimensionsChanged;
        _session.OutputReceived += OnSessionOutputReceived;
        _session.Failed += OnSessionFailed;
        _session.Exited += OnSessionExited;
    }

    /// <summary>
    /// Starts the terminal session using the current renderer dimensions.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels startup.</param>
    /// <returns>A task that completes when startup finishes.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var initialSize = _terminal.Dimensions.Columns == 0 || _terminal.Dimensions.Rows == 0 ? TerminalSize.Default : _terminal.Dimensions;
        await _session.StartAsync(initialSize, cancellationToken);
    }

    private async void OnTerminalUserInput(object? sender, TerminalDataEventArgs e)
    {
        try
        {
            await _session.WriteAsync(e.Data, CancellationToken.None);
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                ReportFailure(exception);
            }
        }
    }

    private async void OnTerminalDimensionsChanged(object? sender, TerminalSizeEventArgs e)
    {
        try
        {
            await _session.ResizeAsync(e.Size, CancellationToken.None);
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                ReportFailure(exception);
            }
        }
    }

    private void OnSessionOutputReceived(object? sender, TerminalDataEventArgs e)
    {
        _terminal.WriteOutput(e.Data);
    }

    private void OnSessionFailed(object? sender, TerminalErrorEventArgs e)
    {
        ReportFailure(e.Exception);
    }

    private void OnSessionExited(object? sender, EventArgs e)
    {
        _terminal.WriteOutput("\r\n\x1b[90m[process exited]\x1b[0m\r\n");
    }

    private void ReportFailure(Exception exception)
    {
        _terminal.WriteOutput($"\r\n\x1b[31m[terminal error] {exception.Message}\x1b[0m\r\n");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _terminal.UserInput -= OnTerminalUserInput;
        _terminal.DimensionsChanged -= OnTerminalDimensionsChanged;
        _session.OutputReceived -= OnSessionOutputReceived;
        _session.Failed -= OnSessionFailed;
        _session.Exited -= OnSessionExited;
        _session.Dispose();
    }
}
