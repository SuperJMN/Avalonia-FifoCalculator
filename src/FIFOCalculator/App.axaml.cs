using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FIFOCalculator.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.Avalonia.Icons;
using Zafiro.Avalonia.Misc;
using Zafiro.Avalonia.Services;
using Zafiro.Avalonia.Storage;
using Zafiro.UI;
using Zafiro.UI.Shell;

namespace FIFOCalculator;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IconControlProviderRegistry.Register(new ProjektankerIconControlProvider(), asDefault: true);

        var dynamicDataSink = new DynamicDataSink();
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Sink(dynamicDataSink)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddZafiroShell(logger: Log.Logger);
        services.AddAllSectionsFromAttributes(Log.Logger);

        services.AddSingleton<INotificationService>(new NotificationService());
        services.AddSingleton<IObservableLogger>(dynamicDataSink);
        services.AddSingleton<DataEntryViewModel>();

        this.Connect(
            () => new ShellView(),
            view =>
            {
                services.AddSingleton<IFileSystemPicker>(_ =>
                {
                    // Resolve StorageProvider from the main window via ApplicationLifetime.
                    // TopLevel.GetTopLevel(view) is null here because the view isn't attached yet.
                    var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
                    return new AvaloniaFileSystemPicker(desktop.MainWindow!.StorageProvider);
                });

                var provider = services.BuildServiceProvider();
                return provider.GetRequiredService<IShell>();
            },
            () => new Window
            {
                Title = "Avalonia FIFO Calculator",
                Width = 700,
                Height = 500
            });

        base.OnFrameworkInitializationCompleted();
    }
}