using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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
            this.Connect(() => new MainView(), control => CompositionRoot.Create(TopLevel.GetTopLevel(control)!), () => new MainWindow());

            base.OnFrameworkInitializationCompleted();
        }
    }
}