namespace WindowsTerminalControl.Core;

/// <summary>
/// Defines the transport used by a terminal renderer.
/// </summary>
public interface ITerminalSession : IDisposable
{
    /// <summary>
    /// Occurs when terminal output is available.
    /// </summary>
    event EventHandler<TerminalDataEventArgs>? OutputReceived;

    /// <summary>
    /// Occurs when the terminal session fails.
    /// </summary>
    event EventHandler<TerminalErrorEventArgs>? Failed;

    /// <summary>
    /// Occurs when the hosted process exits.
    /// </summary>
    event EventHandler? Exited;

    /// <summary>
    /// Starts the terminal session.
    /// </summary>
    /// <param name="initialSize">The initial terminal dimensions.</param>
    /// <param name="cancellationToken">A token that cancels startup.</param>
    /// <returns>A task that completes when startup finishes.</returns>
    Task StartAsync(TerminalSize initialSize, CancellationToken cancellationToken);

    /// <summary>
    /// Writes input to the terminal session.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the write finishes.</returns>
    ValueTask WriteAsync(string data, CancellationToken cancellationToken);

    /// <summary>
    /// Resizes the terminal session.
    /// </summary>
    /// <param name="size">The new terminal dimensions.</param>
    /// <param name="cancellationToken">A token that cancels the resize.</param>
    /// <returns>A task that completes when the resize finishes.</returns>
    ValueTask ResizeAsync(TerminalSize size, CancellationToken cancellationToken);
}
