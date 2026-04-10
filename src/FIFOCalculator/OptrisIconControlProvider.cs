using Avalonia.Controls;
using Zafiro.Avalonia.Controls;
using Zafiro.Avalonia.Icons;
using OptrisIcon = Optris.Icons.Avalonia.Icon;

namespace FIFOCalculator;

/// <summary>
/// Icon provider that renders <see cref="Zafiro.UI.IIcon"/> using Optris.Icons.Avalonia.Icon.
/// Temporary local class until Zafiro.Avalonia.Icons.Optris is published on NuGet.
/// </summary>
public class OptrisIconControlProvider : IIconControlProvider
{
    public string Prefix => "optris";

    public Control? Create(Zafiro.UI.IIcon icon, string valueWithoutPrefix)
    {
        var source = icon.Source;
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var optrisIcon = new OptrisIcon { Value = source };
        optrisIcon[!OptrisIcon.ForegroundProperty] = optrisIcon[!IconOptions.FillProperty];
        return optrisIcon;
    }
}
