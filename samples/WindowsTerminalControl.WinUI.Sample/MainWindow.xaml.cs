using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using WindowsTerminalControl.Core;
using WindowsTerminalControl.WinUI;

namespace WindowsTerminalControl.Samples.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly string _commandLine;
    private readonly string _workingDirectory;
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
        _commandLine = commandArguments.Count == 0 ? "pwsh.exe -NoLogo" : string.Join(' ', commandArguments);
        _workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        HostModePicker.SelectedIndex = useLegacyHost ? 1 : 0;
        HostModePicker.SelectionChanged += OnHostModeChanged;
        HostToolbar.SizeChanged += OnHostToolbarSizeChanged;
        TerminalHost.Loaded += OnTerminalHostLoaded;
        Closed += OnClosed;
    }

    private void OnTerminalHostLoaded(object sender, RoutedEventArgs e)
    {
        TerminalHost.Loaded -= OnTerminalHostLoaded;
        _hostLoaded = true;
        SwitchHost(GetSelectedHostMode());
    }

    private void OnHostModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_hostLoaded && !_switchingHost)
        {
            SwitchHost(GetSelectedHostMode());
        }
    }

    private TerminalHostMode GetSelectedHostMode()
    {
        return HostModePicker.SelectedIndex == 1 ? TerminalHostMode.ChildWindow : TerminalHostMode.Composition;
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
                StartCompositionHost();
            }
            else
            {
                StartChildWindowHost();
            }
        }
        finally
        {
            _switchingHost = false;
        }
    }

    private void StartCompositionHost()
    {
        Title = "Windows Terminal control — WinUI 3";
        _terminalControl = new TerminalControl(TerminalHostMode.Composition)
        {
            WindowOriginProvider = () => new Windows.Foundation.Point(0, HostToolbar.ActualHeight)
        };
        _terminalControl.InitializationFailed += OnTerminalInitializationFailed;
        _terminalControl.Initialized += OnTerminalInitialized;
        _terminalControl.Connection = _connection;
        TerminalHost.Content = _terminalControl;
        DispatcherQueue.TryEnqueue(_terminalControl.UpdateHostBounds);
        //StatusText.Text = "ContentExternalOutputLink — initializing";
    }

    private void StartChildWindowHost()
    {
        if (!Title.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            Title = "Windows Terminal control — WinUI 3";
        }

        _terminalControl = new TerminalControl(TerminalHostMode.ChildWindow)
        {
            WindowOriginProvider = () => new Windows.Foundation.Point(0, HostToolbar.ActualHeight)
        };
        _terminalControl.InitializationFailed += OnTerminalInitializationFailed;
        _terminalControl.Initialized += OnTerminalInitialized;
        _terminalControl.Connection = _connection;
        TerminalHost.Content = _terminalControl;
        DispatcherQueue.TryEnqueue(_terminalControl.UpdateHostBounds);
        //StatusText.Text = "Child HWND host — initializing";
    }

    private void OnHostToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _terminalControl?.UpdateHostBounds();
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

            if (HostModePicker.SelectedIndex == 1)
            {
                SwitchHost(TerminalHostMode.ChildWindow);
                return;
            }

            HostModePicker.SelectedIndex = 1;
        });
    }

    private void OnTerminalInitialized(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _terminalControl))
        {
            return;
        }

        //StatusText.Text = _terminalControl.IsCompositionActive ? "ContentExternalOutputLink — active" : "Child HWND host — active";
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

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        _terminalControl?.CopySelection();
    }

    private async void OnPasteClicked(object sender, RoutedEventArgs e)
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text) || _connection is null)
        {
            return;
        }

        var text = await content.GetTextAsync();
        _connection.WriteInput(text.Replace("\r\n", "\r").Replace("\n", "\r"));
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        StopCurrentHost();
    }

}
