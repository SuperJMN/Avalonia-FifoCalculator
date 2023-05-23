using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FIFOCalculator.ViewModels;
using FIFOCalculator.Views;
using Zafiro.Avalonia;

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
            this.Connect(() => new MainView(), control => CompositionRoot.Create(TopLevel.GetTopLevel(control)!));

            base.OnFrameworkInitializationCompleted();
        }
    }
}