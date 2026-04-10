using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using DynamicData;
using ReactiveUI;
using Zafiro.UI.Shell.Utils;

namespace FIFOCalculator.ViewModels;

[Section(icon: "fa-list", sortIndex: 2, FriendlyName = "Log")]
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
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { Clipboard: not null } window })
            {
                await window.Clipboard.SetTextAsync(string.Join(Environment.NewLine, logger.Events.Items.Select(x => x.RenderMessage())));
            }
        });
    }

    public ReactiveCommand<Unit, Unit> CopyLog { get; }

    public ReadOnlyObservableCollection<LogEntry> LogEntries => logEntries;
}