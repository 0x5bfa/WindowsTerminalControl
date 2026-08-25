namespace WindowsTerminalControl.WinUI;

/// <summary>
/// Specifies how the native terminal renderer is hosted in WinUI 3.
/// </summary>
public enum TerminalHostMode
{
    /// <summary>
    /// Renders through ContentExternalOutputLink so XAML can appear above the terminal surface.
    /// </summary>
    Composition,

    /// <summary>
    /// Hosts the renderer as a direct child HWND.
    /// </summary>
    ChildWindow
}
