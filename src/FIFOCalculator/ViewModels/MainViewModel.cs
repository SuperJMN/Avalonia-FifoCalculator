using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Zafiro.UI;

namespace FIFOCalculator.ViewModels;

public class MainViewModel : ReactiveObject
{
    private readonly ReadOnlyObservableCollection<LogEntry> logEntries;

    public MainViewModel(IFilePicker storage, INotificationService notificationService, IObservableLogger logger)
    {
        var dataEntryViewModel = new DataEntryViewModel(notificationService, storage);
        DataEntry = dataEntryViewModel;
        
        Sections = new[]
        {
            new Section("Data entry", dataEntryViewModel),
            new Section("Simulate", new SimulationViewModel(dataEntryViewModel.Inputs, dataEntryViewModel.Outputs, notificationService))
        };

        ActiveSection = Sections.First();

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

    public ReactiveCommand<Unit, Unit> CopyLog { get; set; }

    public DataEntryViewModel DataEntry { get; set; }

    public Section[] Sections { get; set; }

    [Reactive]
    public Section ActiveSection { get; set; }

    public ReadOnlyObservableCollection<LogEntry> LogEntries => logEntries;
}