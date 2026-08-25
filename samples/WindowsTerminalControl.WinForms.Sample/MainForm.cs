using WindowsTerminalControl.Core;
using WindowsTerminalControl.WinForms;

namespace WindowsTerminalControl.Samples.WinForms;

internal sealed class MainForm : Form
{
    private const string SmokeTestMarker = "Windows Terminal control smoke test";

    private readonly string _commandLine;
    private readonly object _smokeOutputLock = new();
    private readonly bool _smokeTest;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly TerminalControl _terminal;
    private TerminalSessionBinding? _binding;
    private ITerminalSession? _session;
    private string _smokeOutputTail = string.Empty;
    private int _smokeTestCompletionStarted;

    internal MainForm(string commandLine, bool smokeTest)
    {
        _commandLine = commandLine;
        _smokeTest = smokeTest;

        Text = "Windows Terminal control on WinForms";
        ClientSize = new Size(1100, 700);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterScreen;

        _terminal = new TerminalControl
        {
            Dock = DockStyle.Fill
        };

        _statusLabel = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Starting..."
        };

        var statusStrip = new StatusStrip
        {
            SizingGrip = false
        };
        statusStrip.Items.Add(_statusLabel);

        Controls.Add(_terminal);
        Controls.Add(statusStrip);

        Shown += OnShown;
    }

    internal Exception? StartupException { get; private set; }

    private async void OnShown(object? sender, EventArgs e)
    {
        try
        {
            var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var session = new ConPtySession(_commandLine, workingDirectory);
            _session = session;
            session.Exited += OnSessionExited;
            session.Failed += OnSessionFailed;
            if (_smokeTest)
            {
                session.OutputReceived += OnSmokeTestOutputReceived;
            }

            _binding = new TerminalSessionBinding(_terminal, session);
            await _binding.StartAsync(CancellationToken.None);
            SetStatus("Connected to local ConPTY");
            _terminal.Focus();

            if (_smokeTest)
            {
                await RunSmokeTestAsync(session);
            }
        }
        catch (Exception exception)
        {
            if (_binding is not null)
            {
                _binding.Dispose();
                _binding = null;
            }
            else
            {
                _session?.Dispose();
            }

            _session = null;
            StartupException = exception;
            _terminal.WriteOutput($"\r\nFailed to start the terminal session:\r\n{exception}\r\n");
            SetStatus("Failed to start");
        }
    }

    private void OnSessionExited(object? sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus("Process exited"));
        }
        else
        {
            SetStatus("Process exited");
        }
    }

    private void OnSessionFailed(object? sender, TerminalErrorEventArgs e)
    {
        StartupException = e.Exception;
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus("Terminal error"));
        }
        else
        {
            SetStatus("Terminal error");
        }
    }

    private void SetStatus(string status)
    {
        _statusLabel.Text = $"{status} — {_terminal.Dimensions.Columns} x {_terminal.Dimensions.Rows}";
        if (!_smokeTest)
        {
            return;
        }

        if (status == "Process exited")
        {
            _ = CompleteSmokeTestAsync();
        }
        else if (status == "Terminal error" || status == "Failed to start")
        {
            BeginInvoke(Close);
        }
    }

    private void OnSmokeTestOutputReceived(object? sender, TerminalDataEventArgs e)
    {
        lock (_smokeOutputLock)
        {
            _smokeOutputTail += e.Data;
            if (_smokeOutputTail.Length > 4096)
            {
                _smokeOutputTail = _smokeOutputTail[^4096..];
            }
        }
    }

    private async Task RunSmokeTestAsync(ConPtySession session)
    {
        await session.WriteAsync("Write-Output 'before vertical resize'\r", CancellationToken.None);
        await Task.Delay(300);

        var originalSize = ClientSize;
        ClientSize = new Size(originalSize.Width, originalSize.Height - 180);
        await Task.Delay(300);
        ClientSize = originalSize;
        await Task.Delay(300);

        await session.WriteAsync($"Write-Output '{SmokeTestMarker}'; exit\r", CancellationToken.None);
    }

    private async Task CompleteSmokeTestAsync()
    {
        if (Interlocked.Exchange(ref _smokeTestCompletionStarted, 1) != 0)
        {
            return;
        }

        await Task.Delay(500);
        lock (_smokeOutputLock)
        {
            if (!_smokeOutputTail.Contains(SmokeTestMarker, StringComparison.Ordinal))
            {
                StartupException = new InvalidOperationException("The smoke-test marker was not received from ConPTY.");
            }
        }

        if (!IsDisposed)
        {
            BeginInvoke(Close);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_binding is not null)
        {
            _binding.Dispose();
            _binding = null;
        }

        _session = null;

        base.OnFormClosed(e);
    }
}
