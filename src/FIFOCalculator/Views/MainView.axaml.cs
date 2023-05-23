using System;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;

namespace FIFOCalculator.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (!Design.IsDesignMode)
            {
                DataContext = CompositionRoot.Create(TopLevel.GetTopLevel(this)!);
            }
        }
    }
}