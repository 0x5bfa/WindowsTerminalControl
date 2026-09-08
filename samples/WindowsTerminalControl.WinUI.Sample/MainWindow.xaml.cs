using Microsoft.UI.Xaml;
using Windows.Graphics;
using WindowsTerminalControl.Core;
using WindowsTerminalControl.WinUI;

namespace WindowsTerminalControl.Samples.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly string _commandLine;
    private readonly string _workingDirectory;
    private TerminalHostMode _hostMode;
    private TerminalControl? _terminalControl;
    private TerminalSessionConnection? _connection;
    private bool _hostLoaded;
    private bool _switchingHost;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(1100, 700));

        var commandArguments = Environment.GetCommandLineArgs().Skip(1).ToList();
        var useLegacyHost = commandArguments.Remove("--legacy-host");
        commandArguments.Remove("--composition-host");
        _commandLine = commandArguments.Count == 0 ? "C:\\WIndows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe" : string.Join(' ', commandArguments);
        _workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _hostMode = useLegacyHost ? TerminalHostMode.ChildWindow : TerminalHostMode.Composition;
        TerminalHost.Loaded += OnTerminalHostLoaded;
        Closed += OnClosed;
    }

    private void OnTerminalHostLoaded(object sender, RoutedEventArgs e)
    {
        TerminalHost.Loaded -= OnTerminalHostLoaded;
        _hostLoaded = true;
        SwitchHost(_hostMode);
    }

    private void SwitchHost(TerminalHostMode mode)
    {
        if (_switchingHost)
        {
            return;
        }

        _switchingHost = true;
        try
        {
            StopCurrentHost();
            _connection = new TerminalSessionConnection(new ConPtySession(_commandLine, _workingDirectory));

            if (mode == TerminalHostMode.Composition)
            {
                _terminalControl = new TerminalControl(TerminalHostMode.Composition)
                {
                    WindowOriginProvider = () => new Windows.Foundation.Point(0, 0)
                };
                _terminalControl.InitializationFailed += OnTerminalInitializationFailed;
                _terminalControl.Initialized += OnTerminalInitialized;
                _terminalControl.Connection = _connection;
                TerminalHost.Content = _terminalControl;
                DispatcherQueue.TryEnqueue(_terminalControl.UpdateHostBounds);
            }
            else
            {
                _terminalControl = new TerminalControl(TerminalHostMode.ChildWindow)
                {
                    WindowOriginProvider = () => new Windows.Foundation.Point(0, 0)
                };
                _terminalControl.InitializationFailed += OnTerminalInitializationFailed;
                _terminalControl.Initialized += OnTerminalInitialized;
                _terminalControl.Connection = _connection;
                TerminalHost.Content = _terminalControl;
                DispatcherQueue.TryEnqueue(_terminalControl.UpdateHostBounds);
            }
        }
        finally
        {
            _switchingHost = false;
        }
    }

    private void OnTerminalInitializationFailed(object? sender, Exception exception)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!ReferenceEquals(sender, _terminalControl))
            {
                return;
            }

            //StatusText.Text = $"Host failed: {exception.Message}";
            Title = $"Terminal host failed — {exception.Message}";
            if (!_terminalControl.UsesComposition)
            {
                return;
            }

            if (_hostMode is TerminalHostMode.ChildWindow)
            {
                SwitchHost(TerminalHostMode.ChildWindow);
                return;
            }

            _hostMode = TerminalHostMode.ChildWindow;
        });
    }

    private void OnTerminalInitialized(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _terminalControl))
        {
            return;
        }
    }

    private void StopCurrentHost()
    {
        TerminalHost.Content = null;
        if (_terminalControl is not null)
        {
            _terminalControl.InitializationFailed -= OnTerminalInitializationFailed;
            _terminalControl.Initialized -= OnTerminalInitialized;
            _terminalControl.Dispose();
            _terminalControl = null;
        }

        _connection?.Dispose();
        _connection = null;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        StopCurrentHost();
    }
}
