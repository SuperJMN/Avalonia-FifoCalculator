using System;
using System.Reactive;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using ReactiveUI;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Shell.Utils;

namespace FIFOCalculator.ViewModels;

[Section("Settings", icon: "fa-gear", sortIndex: 4)]
public class SettingsViewModel : ViewModelBase
{
    private static readonly FileTypeFilter JsonFiles = new("JSON files", "*.json");
    private readonly DataEntryViewModel dataEntryViewModel;
    private bool autosaveEnabled;
    private bool replacingCatalog;

    public SettingsViewModel(
        DataEntryViewModel dataEntryViewModel,
        IEntryCatalogRepository repository,
        IDialog dialog,
        IFileSystemPicker fileSystemPicker,
        INotificationService notificationService,
        TimeSpan? autosaveInterval = null)
    {
        this.dataEntryViewModel = dataEntryViewModel;

        LoadSavedData = ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await repository.Load();
            return result.Tap(catalog =>
            {
                ReplaceCatalog(catalog);
                autosaveEnabled = true;
            }).Map(_ => Unit.Default);
        });
        LoadSavedData.HandleErrorsWith(notificationService);

        ClearData = ReactiveCommand.CreateFromTask(async () =>
        {
            var confirmation = await dialog.ShowConfirmation(
                "Clear data",
                "This will delete all existing FIFO entries.",
                "Clear data",
                "Cancel",
                yesIsPrimary: false,
                tone: DialogTone.Warning);

            if (confirmation.GetValueOrDefault() != true)
            {
                return Result.Success();
            }

            ReplaceCatalog(EmptyCatalog());
            autosaveEnabled = true;
            return await repository.Save(dataEntryViewModel.ToCatalog());
        });
        ClearData.HandleErrorsWith(notificationService);

        ImportDataFile = ReactiveCommand.CreateFromTask(async () =>
        {
            var fileResult = await fileSystemPicker.PickForOpen(JsonFiles);
            if (fileResult.IsFailure)
            {
                return Result.Failure(fileResult.Error);
            }

            var file = fileResult.Value;
            if (file.HasNoValue)
            {
                return Result.Success();
            }

            var confirmation = await dialog.ShowConfirmation(
                "Import data file",
                $"Importing '{file.Value.Name}' will replace all existing FIFO entries.",
                "Import data",
                "Cancel",
                yesIsPrimary: false,
                tone: DialogTone.Warning);

            if (confirmation.GetValueOrDefault() != true)
            {
                return Result.Success();
            }

            var imported = await repository.Load(file.Value);
            return await imported
                .Tap(catalog =>
                {
                    ReplaceCatalog(catalog);
                    autosaveEnabled = true;
                })
                .Bind(_ => repository.Save(dataEntryViewModel.ToCatalog()));
        });
        ImportDataFile.HandleErrorsWith(notificationService);

        var saveCatalog = ReactiveCommand.CreateFromTask<EntryCatalog, Result>(repository.Save);
        saveCatalog.HandleErrorsWith(notificationService);

        var autosaveChanges = dataEntryViewModel.CatalogChanges
            .Where(_ => autosaveEnabled && !replacingCatalog)
            .Publish()
            .RefCount();

        var interval = autosaveInterval ?? TimeSpan.FromSeconds(2);
        if (interval > TimeSpan.Zero)
        {
            autosaveChanges = autosaveChanges.Throttle(interval);
        }

        autosaveChanges.InvokeCommand(saveCatalog);
    }

    public ReactiveCommand<Unit, Result<Unit>> LoadSavedData { get; }
    public ReactiveCommand<Unit, Result> ClearData { get; }
    public ReactiveCommand<Unit, Result> ImportDataFile { get; }

    private void ReplaceCatalog(EntryCatalog catalog)
    {
        replacingCatalog = true;
        try
        {
            dataEntryViewModel.LoadCatalog(catalog);
        }
        finally
        {
            replacingCatalog = false;
        }
    }

    private static EntryCatalog EmptyCatalog() => new([], []);
}
