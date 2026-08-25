using System.Windows;
using WindowsTerminalControl.Core;
using WindowsTerminalControl.Wpf;

namespace WindowsTerminalControl.Samples.Wpf;

public partial class MainWindow : Window
{
    private TerminalSessionConnection? _connection;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_connection is not null)
        {
            return;
        }

        var commandArguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var commandLine = commandArguments.Length == 0 ? "pwsh.exe -NoLogo" : string.Join(' ', commandArguments);
        var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _connection = new TerminalSessionConnection(new ConPtySession(commandLine, workingDirectory));
        Terminal.Connection = _connection;
        Terminal.Focus();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _connection?.Dispose();
        _connection = null;
    }
}
