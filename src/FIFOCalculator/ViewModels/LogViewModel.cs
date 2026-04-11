using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using DynamicData;
using ReactiveUI;
using Zafiro.UI.Shell.Utils;

namespace FIFOCalculator.ViewModels;

[Section(icon: "fa-list", sortIndex: 3, FriendlyName = "Log")]
public partial class LogViewModel : ViewModelBase
{
    private readonly ReadOnlyObservableCollection<LogEntry> logEntries;

    public LogViewModel(IObservableLogger logger)
    {
        logger.Events
            .Connect()
            .Transform(x => new LogEntry(x))
            .Bind(out logEntries)
            .Subscribe();

        CopyLog = ReactiveCommand.CreateFromTask(async () =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            {
                var clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
                if (clipboard is not null)
                {
                    var text = string.Join(Environment.NewLine, logger.Events.Items.Select(x => x.RenderMessage()));
                    await clipboard.SetTextAsync(text);
                }
            }
        });
    }

    public ReactiveCommand<Unit, Unit> CopyLog { get; }

    public ReadOnlyObservableCollection<LogEntry> LogEntries => logEntries;
}