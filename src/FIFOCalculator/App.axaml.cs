using Avalonia;
using Avalonia.Controls;
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
            _ =>
            {
                services.AddSingleton<IFileSystemPicker>(sp =>
                {
                    var topLevel = ApplicationUtils.TopLevel().Value;
                    return new AvaloniaFileSystemPicker(topLevel.StorageProvider);
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