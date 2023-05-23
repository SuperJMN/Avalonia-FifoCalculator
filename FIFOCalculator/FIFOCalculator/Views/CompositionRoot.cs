using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using FIFOCalculator.ViewModels;
using Zafiro.Avalonia.Notifications;
using Zafiro.Avalonia.Storage;

namespace FIFOCalculator.Views;

public static class CompositionRoot
{
    public static MainViewModel Create(TopLevel topLevel)
    {
        return new MainViewModel(new AvaloniaStorage(topLevel.StorageProvider), new NotificationService(new WindowNotificationManager(topLevel)));
    }
}