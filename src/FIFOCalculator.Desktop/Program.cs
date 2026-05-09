using Avalonia;
using FIFOCalculator.Desktop.Sync;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;
using System;
using Zafiro.Avalonia.Mcp.AppHost;

namespace FIFOCalculator.Desktop;

internal class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        App.ConfigureHostServices = services => services.AddFifoDesktopSync();

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseMcpDiagnostics()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .UseReactiveUI(_ => { });
    }
}
