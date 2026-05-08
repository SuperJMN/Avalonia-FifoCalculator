using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FIFOCalculator.Persistence;
using FIFOCalculator.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;
using Serilog;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.Avalonia.Icons;
using Zafiro.Avalonia.Misc;
using Zafiro.Avalonia.Services;
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
        IconProvider.Current.Register<FontAwesomeIconProvider>();
        IconControlProviderRegistry.Register(new OptrisIconControlProvider(), asDefault: true);

        var dynamicDataSink = new DynamicDataSink();
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Sink(dynamicDataSink)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddZafiroShell(logger: Log.Logger);
        services.AddAllSectionsFromAttributes(Log.Logger);

        services.AddSingleton<INotificationService>(new NotificationService());
        services.AddSingleton<IObservableLogger>(dynamicDataSink);
        services.AddSingleton<IEntryCatalogRepository>(_ => new JsonEntryCatalogRepository(logger: Log.Logger));
        services.AddSingleton<DataEntryViewModel>();

        this.Connect(
            () => new ShellView(),
            _ =>
            {
                var provider = services.BuildServiceProvider();
                provider.GetRequiredService<DataEntryViewModel>().LoadStoreViewModel.Open.Execute().Subscribe(_ => { });
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
