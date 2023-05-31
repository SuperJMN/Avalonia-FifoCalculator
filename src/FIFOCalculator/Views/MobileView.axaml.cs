using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FIFOCalculator.Views
{
    public partial class MobileView : UserControl
    {
        public MobileView()
        {
            InitializeComponent();
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                topLevel.BackRequested += this.MainView_BackRequested;
            }
        }

        private void MainView_BackRequested(object? sender, RoutedEventArgs e)
        {
            
        }
    }
}
