using Microsoft.Terminal.Wpf;
using WindowsTerminalControl.Core;

namespace WindowsTerminalControl.WinUI;

/// <summary>
/// Adapts an <see cref="ITerminalSession"/> to the connection contract used by the terminal renderer.
/// </summary>
public sealed class TerminalSessionConnection : ITerminalConnection, IDisposable
{
    private readonly ITerminalSession _session;
    private TerminalSize _initialSize = TerminalSize.Default;
    private int _disposed;
    private int _started;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalSessionConnection"/> class.
    /// </summary>
    /// <param name="session">The terminal transport to connect.</param>
    public TerminalSessionConnection(ITerminalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _session.OutputReceived += OnOutputReceived;
        _session.Failed += OnFailed;
        _session.Exited += OnExited;
    }

    /// <inheritdoc/>
    public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;

    /// <inheritdoc/>
    public void Start()
    {
        if (_disposed != 0 || Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        try
        {
            _session.StartAsync(_initialSize, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            ReportFailure(exception);
            Dispose();
        }
    }

    /// <inheritdoc/>
    public async void WriteInput(string data)
    {
        if (_disposed != 0 || _started == 0)
        {
            return;
        }

        try
        {
            await _session.WriteAsync(data, CancellationToken.None);
        }
        catch (Exception exception) when (_disposed == 0)
        {
            ReportFailure(exception);
        }
    }

    /// <inheritdoc/>
    public async void Resize(uint rows, uint columns)
    {
        if (_disposed != 0 || rows is 0 or > ushort.MaxValue || columns is 0 or > ushort.MaxValue)
        {
            return;
        }

        var size = new TerminalSize((ushort)columns, (ushort)rows);
        _initialSize = size;
        if (_started == 0)
        {
            return;
        }

        try
        {
            await _session.ResizeAsync(size, CancellationToken.None);
        }
        catch (Exception exception) when (_disposed == 0)
        {
            ReportFailure(exception);
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _session.OutputReceived -= OnOutputReceived;
        _session.Failed -= OnFailed;
        _session.Exited -= OnExited;
        _session.Dispose();
    }

    private void OnOutputReceived(object? sender, TerminalDataEventArgs e)
    {
        TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(e.Data));
    }

    private void OnFailed(object? sender, TerminalErrorEventArgs e)
    {
        ReportFailure(e.Exception);
    }

    private void OnExited(object? sender, EventArgs e)
    {
        TerminalOutput?.Invoke(this, new TerminalOutputEventArgs("\r\n\x1b[90m[process exited]\x1b[0m\r\n"));
    }

    private void ReportFailure(Exception exception)
    {
        TerminalOutput?.Invoke(this, new TerminalOutputEventArgs($"\r\n\x1b[31m[terminal error] {exception.Message}\x1b[0m\r\n"));
    }
}
