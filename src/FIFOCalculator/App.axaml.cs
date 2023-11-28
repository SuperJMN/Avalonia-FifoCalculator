using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using FIFOCalculator.ViewModels;
using FIFOCalculator.Views;
using Zafiro.Avalonia.Mixins;

namespace FIFOCalculator
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var dynamicDataSink = new DynamicDataSink();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(dynamicDataSink)
                .CreateLogger();

            this.Connect(() => new MainView(), control => CompositionRoot.Create(TopLevel.GetTopLevel(control)!, dynamicDataSink), () => new MainWindow());

            base.OnFrameworkInitializationCompleted();
        }
    }
}