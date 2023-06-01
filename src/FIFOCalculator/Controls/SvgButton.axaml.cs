using Avalonia;
using Avalonia.Controls;

namespace FIFOCalculator.Controls
{
    public class SvgButton : Button
    {
        public static readonly StyledProperty<string> SvgPathProperty = AvaloniaProperty.Register<SvgButton, string>(
            nameof(SvgPath));

        public string SvgPath
        {
            get => GetValue(SvgPathProperty);
            set => SetValue(SvgPathProperty, value);
        }

        public static readonly StyledProperty<Dock> ContentAlignmentProperty = AvaloniaProperty.Register<SvgButton, Dock>(
            nameof(ContentAlignment));

        public Dock ContentAlignment
        {
            get => GetValue(ContentAlignmentProperty);
            set => SetValue(ContentAlignmentProperty, value);
        }
    }
}
