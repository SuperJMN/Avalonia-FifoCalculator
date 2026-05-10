using System;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using FIFOCalculator.Sync.Zafiro;
using ReactiveUI.Avalonia;
#if DEBUG
using Zafiro.Avalonia.Mcp.AppHost;
#endif

namespace FIFOCalculator.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    public MainApplication(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.ConfigureHostServices = services => services.AddFifoZafiroSync();

        var appBuilder = base.CustomizeAppBuilder(builder)
            .WithInterFont();

#if DEBUG
        appBuilder = appBuilder.UseMcpDiagnostics();
#endif

        return appBuilder.UseReactiveUI(_ => { });
    }
}

[Activity(
    Label = "FIFO Calculator",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
