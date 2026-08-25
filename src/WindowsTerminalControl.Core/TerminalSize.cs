namespace WindowsTerminalControl.Core;

/// <summary>
/// Represents terminal dimensions measured in character cells.
/// </summary>
/// <param name="Columns">The number of columns.</param>
/// <param name="Rows">The number of rows.</param>
public readonly record struct TerminalSize(ushort Columns, ushort Rows)
{
    /// <summary>
    /// Gets the default terminal dimensions.
    /// </summary>
    public static TerminalSize Default { get; } = new(80, 24);
}
