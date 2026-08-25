<h1 align="center">Windows Terminal Control</h1>
<p align="center">Windows Terminal controls for WinForms, WPF, and WinUI 3 applications.</p>

## Installing the packages

Install the package for your UI framework with NuGet Package Manager or the .NET CLI.

### WinForms

<a href="https://www.nuget.org/packages/WindowsTerminalControl.WinForms"><img src="https://img.shields.io/nuget/v/WindowsTerminalControl.WinForms" alt="NuGet badge" /></a>

```console
dotnet add package WindowsTerminalControl.WinForms
```

### WPF

<a href="https://www.nuget.org/packages/WindowsTerminalControl.Wpf"><img src="https://img.shields.io/nuget/v/WindowsTerminalControl.Wpf" alt="NuGet badge" /></a>

```console
dotnet add package WindowsTerminalControl.Wpf
```

### WinUI (Windows App SDK/WinUI 3)

<a href="https://www.nuget.org/packages/WindowsTerminalControl.WinUI"><img src="https://img.shields.io/nuget/v/WindowsTerminalControl.WinUI" alt="NuGet badge" /></a>

```console
dotnet add package WindowsTerminalControl.WinUI
```

## Usage

The sample applications show how to connect each control to a local PowerShell session through ConPTY:

- [WinForms sample](samples/WindowsTerminalControl.WinForms.Sample)
- [WPF sample](samples/WindowsTerminalControl.Wpf.Sample)
- [WinUI sample](samples/WindowsTerminalControl.WinUI.Sample)

The WinUI control supports both `ContentExternalOutputLink` composition hosting and child-window hosting.

## Building from source

1. Install Visual Studio 2026 with the .NET desktop and WinUI development tools.
2. Install the .NET 10 SDK.
3. Open `WindowsTerminalControl.slnx`.
4. Select `x64`, then build the solution.

Use **Pack** on a library project in Visual Studio to create its NuGet package under `artifacts/packages`.
