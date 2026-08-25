namespace WindowsTerminalControl.Core;

/// <summary>
/// Provides data emitted by a terminal session.
/// </summary>
public sealed class TerminalDataEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalDataEventArgs"/> class.
    /// </summary>
    /// <param name="data">The emitted terminal data.</param>
    public TerminalDataEventArgs(string data)
    {
        Data = data;
    }

    /// <summary>
    /// Gets the emitted terminal data.
    /// </summary>
    public string Data { get; }
}

/// <summary>
/// Provides an error raised by a terminal session.
/// </summary>
public sealed class TerminalErrorEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalErrorEventArgs"/> class.
    /// </summary>
    /// <param name="exception">The terminal failure.</param>
    public TerminalErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }

    /// <summary>
    /// Gets the terminal failure.
    /// </summary>
    public Exception Exception { get; }
}

/// <summary>
/// Provides updated terminal dimensions.
/// </summary>
public sealed class TerminalSizeEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalSizeEventArgs"/> class.
    /// </summary>
    /// <param name="size">The updated terminal dimensions.</param>
    public TerminalSizeEventArgs(TerminalSize size)
    {
        Size = size;
    }

    /// <summary>
    /// Gets the updated terminal dimensions.
    /// </summary>
    public TerminalSize Size { get; }
}
