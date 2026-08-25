namespace WindowsTerminalControl.Samples.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var smokeTest = args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        var commandArguments = args.Where(argument => !string.Equals(argument, "--smoke-test", StringComparison.OrdinalIgnoreCase)).ToArray();
        var commandLine = commandArguments.Length == 0 ? "pwsh.exe -NoLogo" : string.Join(' ', commandArguments);
        var mainForm = new MainForm(commandLine, smokeTest);
        Application.Run(mainForm);
        if (smokeTest && mainForm.StartupException is not null)
        {
            Console.Error.WriteLine(mainForm.StartupException);
        }

        Environment.ExitCode = mainForm.StartupException is null ? 0 : 1;
    }
}
