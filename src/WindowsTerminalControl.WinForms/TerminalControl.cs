using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using WindowsTerminalControl.Core;

namespace WindowsTerminalControl.WinForms;

/// <summary>
/// Hosts the native Windows Terminal renderer in a WinForms control.
/// </summary>
public sealed class TerminalControl : UserControl
{
    private readonly NativeMethods.ScrollCallback _scrollCallback;
    private readonly VScrollBar _scrollBar;
    private readonly System.Windows.Forms.Timer _caretBlinkTimer;
    private readonly NativeMethods.WriteCallback _writeCallback;
    private NativeTerminalWindow? _nativeWindow;
    private TerminalSafeHandle? _terminal;
    private IntPtr _terminalWindow;
    private int? _acceleratorVirtualKey;
    private bool _ignoreNextCharacter;
    private bool _terminalWindowDestructionBegun;
    private bool _updatingScrollBar;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalControl"/> class.
    /// </summary>
    public TerminalControl()
    {
        _writeCallback = OnNativeWrite;
        _scrollCallback = OnNativeScroll;

        BackColor = Color.FromArgb(12, 12, 12);
        ForeColor = Color.FromArgb(242, 242, 242);
        Font = new Font("Consolas", 11.0f, FontStyle.Regular, GraphicsUnit.Point);
        TabStop = true;

        _scrollBar = new VScrollBar
        {
            Dock = DockStyle.Right,
            LargeChange = 1,
            SmallChange = 1,
            TabStop = false
        };
        _scrollBar.ValueChanged += OnScrollBarValueChanged;
        Controls.Add(_scrollBar);

        _caretBlinkTimer = new System.Windows.Forms.Timer();
        var blinkTime = NativeMethods.GetCaretBlinkTime();
        if (blinkTime != uint.MaxValue)
        {
            _caretBlinkTimer.Interval = checked((int)Math.Clamp(blinkTime, 100u, 5000u));
            _caretBlinkTimer.Tick += OnCaretBlink;
        }
    }

    /// <summary>
    /// Occurs when the renderer produces input for the connected terminal session.
    /// </summary>
    public event EventHandler<TerminalDataEventArgs>? UserInput;

    /// <summary>
    /// Occurs when the renderer dimensions change.
    /// </summary>
    public event EventHandler<TerminalSizeEventArgs>? DimensionsChanged;

    /// <summary>
    /// Gets the current renderer dimensions.
    /// </summary>
    public TerminalSize Dimensions { get; private set; } = TerminalSize.Default;

    /// <summary>
    /// Writes terminal output to the renderer.
    /// </summary>
    /// <param name="data">The VT-formatted output.</param>
    public void WriteOutput(string data)
    {
        if (string.IsNullOrEmpty(data) || IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                Invoke(() => SendOutput(data));
            }
            catch (InvalidOperationException) when (IsDisposed || !IsHandleCreated)
            {
            }
        }
        else
        {
            SendOutput(data);
        }
    }

    private bool CanUseTerminal => !IsDisposed && _terminal is not null && !_terminalWindowDestructionBegun;

    private void CreateTerminal()
    {
        _terminalWindowDestructionBegun = false;
        var result = NativeMethods.CreateTerminal(Handle, out _terminalWindow, out _terminal);
        Marshal.ThrowExceptionForHR(result);

        NativeMethods.TerminalRegisterWriteCallback(_terminal, _writeCallback);
        NativeMethods.TerminalRegisterScrollCallback(_terminal, _scrollCallback);
        _nativeWindow = new NativeTerminalWindow(this, _terminalWindow);

        ApplyTheme();
        ResizeTerminal();
    }

    private void ApplyTheme()
    {
        if (!CanUseTerminal)
        {
            return;
        }

        var theme = new NativeMethods.TerminalTheme
        {
            DefaultBackground = ToColorRef(BackColor),
            DefaultForeground = ToColorRef(ForeColor),
            DefaultSelectionBackground = ToColorRef(Color.FromArgb(128, 128, 128)),
            SelectionBackgroundAlpha = 0.5f,
            CursorStyle = NativeMethods.CaretStyle.BlinkingBar,
            ColorTable = CreateColorTable()
        };

        var dpi = DeviceDpi == 0 ? 96 : DeviceDpi;
        NativeMethods.TerminalSetTheme(_terminal!, theme, Font.FontFamily.Name, checked((short)Math.Round(Font.Size)), dpi);
        ResizeTerminal();
    }

    private void ResizeTerminal()
    {
        if (!CanUseTerminal || ClientSize.Width <= _scrollBar.Width || ClientSize.Height <= 0)
        {
            return;
        }

        var result = NativeMethods.TerminalTriggerResize(_terminal!, ClientSize.Width - _scrollBar.Width, ClientSize.Height, out var nativeSize);
        Marshal.ThrowExceptionForHR(result);

        var newSize = new TerminalSize(checked((ushort)nativeSize.X), checked((ushort)nativeSize.Y));
        if (newSize != Dimensions)
        {
            Dimensions = newSize;
            DimensionsChanged?.Invoke(this, new TerminalSizeEventArgs(newSize));
        }
    }

    private void SendOutput(string data)
    {
        if (!CanUseTerminal)
        {
            return;
        }

        NativeMethods.TerminalSendOutput(_terminal!, data);
    }

    private void OnNativeWrite(string data)
    {
        UserInput?.Invoke(this, new TerminalDataEventArgs(data));
    }

    private void OnNativeScroll(int viewTop, int viewHeight, int bufferSize)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ApplyScrollPosition(viewTop, viewHeight, bufferSize));
        }
        else
        {
            ApplyScrollPosition(viewTop, viewHeight, bufferSize);
        }
    }

    private void ApplyScrollPosition(int viewTop, int viewHeight, int bufferSize)
    {
        _updatingScrollBar = true;
        try
        {
            _scrollBar.Minimum = 0;
            _scrollBar.Maximum = Math.Max(0, bufferSize - viewHeight);
            _scrollBar.Value = Math.Clamp(viewTop, _scrollBar.Minimum, _scrollBar.Maximum);
        }
        finally
        {
            _updatingScrollBar = false;
        }
    }

    private void OnScrollBarValueChanged(object? sender, EventArgs e)
    {
        if (!_updatingScrollBar && CanUseTerminal)
        {
            NativeMethods.TerminalUserScroll(_terminal!, _scrollBar.Value);
        }
    }

    private void OnCaretBlink(object? sender, EventArgs e)
    {
        if (CanUseTerminal)
        {
            NativeMethods.TerminalBlinkCursor(_terminal!);
        }
    }

    private bool ProcessNativeWindowMessage(ref Message message)
    {
        if (!CanUseTerminal)
        {
            return false;
        }

        var messageId = message.Msg;
        switch (messageId)
        {
            case NativeMethods.WmSetFocus:
                NativeMethods.TerminalSetFocus(_terminal!);
                _caretBlinkTimer.Start();
                return true;

            case NativeMethods.WmKillFocus:
                NativeMethods.TerminalKillFocus(_terminal!);
                _caretBlinkTimer.Stop();
                NativeMethods.TerminalSetCursorVisible(_terminal!, false);
                return true;

            case NativeMethods.WmMouseActivate:
                Focus();
                NativeMethods.TerminalSetFocus(_terminal!);
                return true;

            case NativeMethods.WmKeyDown:
            case NativeMethods.WmSysKeyDown:
                return ProcessKeyDown(ref message);

            case NativeMethods.WmKeyUp:
            case NativeMethods.WmSysKeyUp:
                return ProcessKeyUp(ref message);

            case NativeMethods.WmChar:
                return ProcessCharacter(ref message);

            case NativeMethods.WmMouseWheel:
                return ProcessMouseWheel(ref message);

            default:
                return false;
        }
    }

    private bool ProcessKeyDown(ref Message message)
    {
        var key = unchecked((Keys)(int)message.WParam);
        if (Control.ModifierKeys == (Keys.Control | Keys.Shift) && key == Keys.C && NativeMethods.TerminalIsSelectionActive(_terminal!))
        {
            CopySelection();
            _acceleratorVirtualKey = (int)key;
            _ignoreNextCharacter = true;
            return true;
        }

        if (Control.ModifierKeys == (Keys.Control | Keys.Shift) && key == Keys.V)
        {
            PasteClipboard();
            _acceleratorVirtualKey = (int)key;
            _ignoreNextCharacter = true;
            return true;
        }

        var keyParameters = new NativeMethods.KeyMessageParameters(message);
        NativeMethods.TerminalSetCursorVisible(_terminal!, true);
        _caretBlinkTimer.Start();
        NativeMethods.TerminalSendKeyEvent(_terminal!, keyParameters.VirtualKey, keyParameters.ScanCode, keyParameters.Flags, true);
        return true;
    }

    private bool ProcessKeyUp(ref Message message)
    {
        var keyParameters = new NativeMethods.KeyMessageParameters(message);
        if (_acceleratorVirtualKey == keyParameters.VirtualKey)
        {
            _acceleratorVirtualKey = null;
            return true;
        }

        NativeMethods.TerminalSendKeyEvent(_terminal!, keyParameters.VirtualKey, keyParameters.ScanCode, keyParameters.Flags, false);
        return true;
    }

    private bool ProcessCharacter(ref Message message)
    {
        if (_ignoreNextCharacter)
        {
            _ignoreNextCharacter = false;
            return true;
        }

        var keyParameters = new NativeMethods.KeyMessageParameters(message);
        NativeMethods.TerminalSendCharEvent(_terminal!, unchecked((char)keyParameters.VirtualKey), keyParameters.ScanCode, keyParameters.Flags);
        return true;
    }

    private bool ProcessMouseWheel(ref Message message)
    {
        var delta = unchecked((short)((message.WParam.ToInt64() >> 16) & 0xffff));
        if (Control.ModifierKeys.HasFlag(Keys.Control))
        {
            var newSize = delta > 0 ? Math.Min(Font.Size + 1.0f, 36.0f) : Math.Max(Font.Size - 1.0f, 6.0f);
            var previousFont = Font;
            Font = new Font(previousFont.FontFamily, newSize, previousFont.Style, GraphicsUnit.Point);
            previousFont.Dispose();
        }
        else
        {
            var lines = Math.Max(SystemInformation.MouseWheelScrollLines, 1) * delta / 120;
            _scrollBar.Value = Math.Clamp(_scrollBar.Value - lines, _scrollBar.Minimum, _scrollBar.Maximum);
        }

        return true;
    }

    private void CopySelection()
    {
        var selection = NativeMethods.TerminalGetSelection(_terminal!);
        if (!string.IsNullOrEmpty(selection))
        {
            Clipboard.SetText(selection);
        }

        NativeMethods.TerminalClearSelection(_terminal!);
    }

    private void PasteClipboard()
    {
        if (!Clipboard.ContainsText())
        {
            return;
        }

        var text = Clipboard.GetText().Replace("\r\n", "\r").Replace("\n", "\r");
        UserInput?.Invoke(this, new TerminalDataEventArgs(text));
    }

    private static uint[] CreateColorTable()
    {
        return
        [
            ToColorRef(Color.FromArgb(12, 12, 12)),
            ToColorRef(Color.FromArgb(197, 15, 31)),
            ToColorRef(Color.FromArgb(19, 161, 14)),
            ToColorRef(Color.FromArgb(193, 156, 0)),
            ToColorRef(Color.FromArgb(0, 55, 218)),
            ToColorRef(Color.FromArgb(136, 23, 152)),
            ToColorRef(Color.FromArgb(58, 150, 221)),
            ToColorRef(Color.FromArgb(204, 204, 204)),
            ToColorRef(Color.FromArgb(118, 118, 118)),
            ToColorRef(Color.FromArgb(231, 72, 86)),
            ToColorRef(Color.FromArgb(22, 198, 12)),
            ToColorRef(Color.FromArgb(249, 241, 165)),
            ToColorRef(Color.FromArgb(59, 120, 255)),
            ToColorRef(Color.FromArgb(180, 0, 158)),
            ToColorRef(Color.FromArgb(97, 214, 214)),
            ToColorRef(Color.FromArgb(242, 242, 242))
        ];
    }

    private static uint ToColorRef(Color color)
    {
        return unchecked((uint)ColorTranslator.ToWin32(color));
    }

    /// <inheritdoc/>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!DesignMode)
        {
            CreateTerminal();
        }
    }

    /// <inheritdoc/>
    protected override void DestroyHandle()
    {
        _caretBlinkTimer.Stop();
        _nativeWindow?.Dispose();
        _nativeWindow = null;
        _terminal?.Dispose();
        _terminal = null;
        _terminalWindow = IntPtr.Zero;
        base.DestroyHandle();
    }

    /// <inheritdoc/>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ResizeTerminal();
    }

    /// <inheritdoc/>
    protected override void OnGotFocus(EventArgs e)
    {
        if (_terminalWindow != IntPtr.Zero)
        {
            NativeMethods.SetFocus(_terminalWindow);
        }

        base.OnGotFocus(e);
    }

    /// <inheritdoc/>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyTheme();
    }

    /// <inheritdoc/>
    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        ApplyTheme();
    }

    /// <inheritdoc/>
    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        ApplyTheme();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _caretBlinkTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class NativeTerminalWindow : NativeWindow, IDisposable
    {
        private readonly TerminalControl _owner;

        internal NativeTerminalWindow(TerminalControl owner, IntPtr handle)
        {
            _owner = owner;
            AssignHandle(handle);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WmDestroy)
            {
                _owner._terminalWindowDestructionBegun = true;
                base.WndProc(ref message);
                return;
            }

            if (!_owner.ProcessNativeWindowMessage(ref message))
            {
                base.WndProc(ref message);
            }
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                ReleaseHandle();
            }
        }
    }

    private sealed class TerminalSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private TerminalSafeHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.DestroyTerminal(handle);
            return true;
        }
    }

    private static class NativeMethods
    {
        private const string TerminalControlLibrary = "Microsoft.Terminal.Control.dll";

        internal const int WmDestroy = 0x0002;
        internal const int WmSetFocus = 0x0007;
        internal const int WmKillFocus = 0x0008;
        internal const int WmMouseActivate = 0x0021;
        internal const int WmKeyDown = 0x0100;
        internal const int WmKeyUp = 0x0101;
        internal const int WmChar = 0x0102;
        internal const int WmSysKeyDown = 0x0104;
        internal const int WmSysKeyUp = 0x0105;
        internal const int WmMouseWheel = 0x020A;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void WriteCallback([In, MarshalAs(UnmanagedType.LPWStr)] string data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void ScrollCallback(int viewTop, int viewHeight, int bufferSize);

        internal enum CaretStyle
        {
            BlinkingBlock = 1,
            SteadyBlock = 2,
            BlinkingUnderline = 3,
            SteadyUnderline = 4,
            BlinkingBar = 5,
            SteadyBar = 6
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TerminalTheme
        {
            internal uint DefaultBackground;
            internal uint DefaultForeground;
            internal uint DefaultSelectionBackground;
            internal float SelectionBackgroundAlpha;
            internal CaretStyle CursorStyle;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = 16)]
            internal uint[] ColorTable;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TerminalSizeNative
        {
            internal int X;
            internal int Y;
        }

        internal readonly struct KeyMessageParameters
        {
            internal KeyMessageParameters(Message message)
            {
                var scanCodeAndFlags = unchecked((ulong)message.LParam.ToInt64()) >> 16;
                ScanCode = unchecked((ushort)(scanCodeAndFlags & 0x00ff));
                Flags = unchecked((ushort)(scanCodeAndFlags & 0xff00));
                VirtualKey = unchecked((ushort)message.WParam.ToInt64());
            }

            internal ushort ScanCode { get; }

            internal ushort Flags { get; }

            internal ushort VirtualKey { get; }
        }

        [DllImport(TerminalControlLibrary, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        internal static extern int CreateTerminal(IntPtr parent, out IntPtr window, out TerminalSafeHandle terminal);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void DestroyTerminal(IntPtr terminal);

        [DllImport(TerminalControlLibrary, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalRegisterWriteCallback(TerminalSafeHandle terminal, [MarshalAs(UnmanagedType.FunctionPtr)] WriteCallback callback);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalRegisterScrollCallback(TerminalSafeHandle terminal, [MarshalAs(UnmanagedType.FunctionPtr)] ScrollCallback callback);

        [DllImport(TerminalControlLibrary, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalSendOutput(TerminalSafeHandle terminal, string data);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalSendKeyEvent(TerminalSafeHandle terminal, ushort virtualKey, ushort scanCode, ushort flags, [MarshalAs(UnmanagedType.Bool)] bool keyDown);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalSendCharEvent(TerminalSafeHandle terminal, char character, ushort scanCode, ushort flags);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern int TerminalTriggerResize(TerminalSafeHandle terminal, int width, int height, out TerminalSizeNative dimensions);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalUserScroll(TerminalSafeHandle terminal, int viewTop);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalClearSelection(TerminalSafeHandle terminal);

        [DllImport(TerminalControlLibrary, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        internal static extern string TerminalGetSelection(TerminalSafeHandle terminal);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool TerminalIsSelectionActive(TerminalSafeHandle terminal);

        [DllImport(TerminalControlLibrary, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalSetTheme(TerminalSafeHandle terminal, [MarshalAs(UnmanagedType.Struct)] TerminalTheme theme, string fontFamily, short fontSize, int dpi);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalBlinkCursor(TerminalSafeHandle terminal);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalSetCursorVisible(TerminalSafeHandle terminal, [MarshalAs(UnmanagedType.Bool)] bool visible);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalSetFocus(TerminalSafeHandle terminal);

        [DllImport(TerminalControlLibrary, CallingConvention = CallingConvention.StdCall)]
        internal static extern void TerminalKillFocus(TerminalSafeHandle terminal);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetFocus(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern uint GetCaretBlinkTime();
    }
}
