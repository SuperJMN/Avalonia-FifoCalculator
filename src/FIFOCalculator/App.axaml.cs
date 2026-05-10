using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FIFOCalculator.Persistence;
using FIFOCalculator.Sync;
using FIFOCalculator.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;
using Optris.Icons.Avalonia.MaterialDesign;
using Serilog;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Icons;
using Zafiro.Avalonia.Misc;
using Zafiro.Avalonia.Services;
using Zafiro.Avalonia.Storage;
using Zafiro.UI;
using Zafiro.UI.Shell;
using Zafiro.UserStorage;

namespace FIFOCalculator;

public partial class App : Application
{
    public static Action<IServiceCollection>? ConfigureHostServices { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
        IconProvider.Current.Register<MaterialDesignIconProvider>();
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
        services.AddSingleton<IUserStorage>(_ => global::System.OperatingSystem.IsBrowser()
            ? new BrowserLocalStorageUserStorage("FIFOCalculator")
            : LocalUserStorage.ForApplication("FIFOCalculator"));
        services.AddSingleton<IEntryCatalogRepository>(provider =>
            new JsonEntryCatalogRepository(provider.GetRequiredService<IUserStorage>(), Log.Logger));
        services.AddSingleton<IFifoSyncService, NoOpFifoSyncService>();
        services.AddSingleton(DialogService.Create());
        services.AddSingleton<DataEntryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        ConfigureHostServices?.Invoke(services);

        this.Connect(
            () => new ShellView(),
            view =>
            {
                services.AddSingleton<IFileSystemPicker>(_ =>
                    new AvaloniaFileSystemPicker(() => TopLevel.GetTopLevel(view)!.StorageProvider));

                var provider = services.BuildServiceProvider();
                FifoSyncStartup.Start(provider.GetRequiredService<IFifoSyncService>(), Log.Logger);
                System.ObservableExtensions.Subscribe(
                    provider.GetRequiredService<SettingsViewModel>().LoadSavedData.Execute(),
                    _ => { });
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
