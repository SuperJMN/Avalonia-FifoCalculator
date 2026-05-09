using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using FIFOCalculator.Sync;
using ReactiveUI;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Shell.Utils;
using DialogOption = Zafiro.Avalonia.Dialogs.Option;

namespace FIFOCalculator.ViewModels;

[Section("Settings", icon: "fa-gear", sortIndex: 4)]
public class SettingsViewModel : ViewModelBase
{
    private static readonly FileTypeFilter JsonFiles = new("JSON files", "*.json");
    private readonly DataEntryViewModel dataEntryViewModel;
    private readonly IFifoSyncService syncService;
    private bool autosaveEnabled;
    private bool replacingCatalog;
    private string syncStatusLine = "Sync is not configured.";
    private bool isSyncSupported;
    private bool canCreateSyncIdentity;
    private bool canUnlockSync;
    private bool canExportSyncIdentity;
    private bool canSyncNow;
    private bool canDisconnectSync;
    private bool hasSyncConflict;

    public SettingsViewModel(
        DataEntryViewModel dataEntryViewModel,
        IEntryCatalogRepository repository,
        IFifoSyncService syncService,
        IDialog dialog,
        IFileSystemPicker fileSystemPicker,
        INotificationService notificationService,
        TimeSpan? autosaveInterval = null)
    {
        this.dataEntryViewModel = dataEntryViewModel;
        this.syncService = syncService;

        syncService.StatusChanged.Subscribe(ApplySyncStatus);
        ApplySyncStatus(syncService.Status);

        async Task<Result> SaveCatalog(EntryCatalog catalog)
        {
            var save = await repository.Save(catalog);
            return save.IsFailure ? save : await syncService.SaveLocalChange(catalog);
        }

        async Task<Result> CompleteSync(Result result)
        {
            if (result.IsFailure)
            {
                return result;
            }

            if (syncService.Status.Conflict is null)
            {
                var loaded = await repository.Load();
                if (loaded.IsFailure)
                {
                    return Result.Failure(loaded.Error);
                }

                ReplaceCatalog(loaded.Value);
            }

            return await ResolveConflictIfNeeded(dialog);
        }

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
            return await SaveCatalog(dataEntryViewModel.ToCatalog());
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
                .Bind(_ => SaveCatalog(dataEntryViewModel.ToCatalog()));
        });
        ImportDataFile.HandleErrorsWith(notificationService);

        CreateSyncIdentity = ReactiveCommand.CreateFromTask(async () =>
        {
            var password = await AskPassword(dialog, "Create sync identity", "Choose a password for this sync identity.");
            if (password.HasNoValue)
            {
                return Result.Success();
            }

            return await CompleteSync(await syncService.CreateIdentity(password.Value, dataEntryViewModel.ToCatalog()));
        });
        CreateSyncIdentity.HandleErrorsWith(notificationService);

        UnlockSync = ReactiveCommand.CreateFromTask(async () =>
        {
            var password = await AskPassword(dialog, "Unlock sync", "Enter the password for this sync identity.");
            if (password.HasNoValue)
            {
                return Result.Success();
            }

            return await CompleteSync(await syncService.Unlock(password.Value, dataEntryViewModel.ToCatalog()));
        });
        UnlockSync.HandleErrorsWith(notificationService);

        ImportSyncIdentity = ReactiveCommand.CreateFromTask(async () =>
        {
            var fileResult = await fileSystemPicker.PickForOpen(JsonFiles);
            if (fileResult.IsFailure)
            {
                return Result.Failure(fileResult.Error);
            }

            if (fileResult.Value.HasNoValue)
            {
                return Result.Success();
            }

            var password = await AskPassword(dialog, "Import sync identity", "Enter the password for the selected sync identity.");
            if (password.HasNoValue)
            {
                return Result.Success();
            }

            var bytes = await fileResult.Value.Value.ReadAll();
            if (bytes.IsFailure)
            {
                return Result.Failure(bytes.Error);
            }

            return await CompleteSync(await syncService.ImportIdentity(bytes.Value, password.Value, dataEntryViewModel.ToCatalog()));
        });
        ImportSyncIdentity.HandleErrorsWith(notificationService);

        ExportSyncIdentity = ReactiveCommand.CreateFromTask(async () =>
        {
            var password = await AskPassword(dialog, "Export sync identity", "Enter the password for this sync identity.");
            if (password.HasNoValue)
            {
                return Result.Success();
            }

            var identity = await syncService.ExportIdentity(password.Value);
            if (identity.IsFailure)
            {
                return Result.Failure(identity.Error);
            }

            var file = await fileSystemPicker.PickForSave("fifo-calculator.identity.json", Maybe.From("json"), JsonFiles);
            return file.HasNoValue
                ? Result.Success()
                : await file.Value.SetContents(ByteSource.FromBytes(identity.Value));
        });
        ExportSyncIdentity.HandleErrorsWith(notificationService);

        SyncNow = ReactiveCommand.CreateFromTask(async () =>
        {
            return await CompleteSync(await syncService.SyncNow(dataEntryViewModel.ToCatalog()));
        });
        SyncNow.HandleErrorsWith(notificationService);

        DisconnectSync = ReactiveCommand.CreateFromTask(async () =>
        {
            var confirmation = await dialog.ShowConfirmation(
                "Disconnect sync",
                "This removes the local sync identity from this device. FIFO data stays on this device.",
                "Disconnect",
                "Cancel",
                yesIsPrimary: false,
                tone: DialogTone.Warning);

            return confirmation.GetValueOrDefault() == true
                ? await syncService.Disconnect()
                : Result.Success();
        });
        DisconnectSync.HandleErrorsWith(notificationService);

        ResolveSyncConflict = ReactiveCommand.CreateFromTask(() => ResolveConflictIfNeeded(dialog));
        ResolveSyncConflict.HandleErrorsWith(notificationService);

        var saveCatalog = ReactiveCommand.CreateFromTask<EntryCatalog, Result>(SaveCatalog);
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
    public ReactiveCommand<Unit, Result> CreateSyncIdentity { get; }
    public ReactiveCommand<Unit, Result> UnlockSync { get; }
    public ReactiveCommand<Unit, Result> ImportSyncIdentity { get; }
    public ReactiveCommand<Unit, Result> ExportSyncIdentity { get; }
    public ReactiveCommand<Unit, Result> SyncNow { get; }
    public ReactiveCommand<Unit, Result> DisconnectSync { get; }
    public ReactiveCommand<Unit, Result> ResolveSyncConflict { get; }

    public string SyncStatusLine
    {
        get => syncStatusLine;
        private set => this.RaiseAndSetIfChanged(ref syncStatusLine, value);
    }

    public bool IsSyncSupported
    {
        get => isSyncSupported;
        private set => this.RaiseAndSetIfChanged(ref isSyncSupported, value);
    }

    public bool CanCreateSyncIdentity
    {
        get => canCreateSyncIdentity;
        private set => this.RaiseAndSetIfChanged(ref canCreateSyncIdentity, value);
    }

    public bool CanUnlockSync
    {
        get => canUnlockSync;
        private set => this.RaiseAndSetIfChanged(ref canUnlockSync, value);
    }

    public bool CanExportSyncIdentity
    {
        get => canExportSyncIdentity;
        private set => this.RaiseAndSetIfChanged(ref canExportSyncIdentity, value);
    }

    public bool CanSyncNow
    {
        get => canSyncNow;
        private set => this.RaiseAndSetIfChanged(ref canSyncNow, value);
    }

    public bool CanDisconnectSync
    {
        get => canDisconnectSync;
        private set => this.RaiseAndSetIfChanged(ref canDisconnectSync, value);
    }

    public bool HasSyncConflict
    {
        get => hasSyncConflict;
        private set => this.RaiseAndSetIfChanged(ref hasSyncConflict, value);
    }

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

    private static Task<Maybe<string>> AskPassword(IDialog dialog, string title, string message)
    {
        var viewModel = new PasswordPromptViewModel(message);
        return dialog.ShowAndGetResult(
            viewModel,
            title,
            model => model.CanSubmit,
            model => model.Password,
            size: DialogSize.Compact);
    }

    private async Task<Result> ResolveConflictIfNeeded(IDialog dialog)
    {
        var conflict = syncService.Status.Conflict;
        if (conflict is null)
        {
            return Result.Success();
        }

        var choice = FifoSyncConflictChoice.Cancel;
        var viewModel = new FifoSyncConflictDialogViewModel(conflict);
        var shown = await dialog.Show(
            viewModel,
            "Sync conflict",
            (_, closeable) =>
            [
                new DialogOption("Use remote", ReactiveCommand.Create(() =>
                {
                    choice = FifoSyncConflictChoice.UseRemote;
                    closeable.Close();
                }).Enhance(), new Settings { Role = OptionRole.Primary }),
                new DialogOption("Keep local", ReactiveCommand.Create(() =>
                {
                    choice = FifoSyncConflictChoice.KeepLocal;
                    closeable.Close();
                }).Enhance(), new Settings { Role = OptionRole.Secondary }),
                new DialogOption("Cancel", ReactiveCommand.Create(closeable.Dismiss).Enhance(), new Settings { Role = OptionRole.Cancel, IsCancel = true })
            ],
            tone: DialogTone.Warning,
            size: DialogSize.Wide);

        if (!shown || choice == FifoSyncConflictChoice.Cancel)
        {
            return Result.Success();
        }

        var result = await syncService.ResolveConflict(choice, dataEntryViewModel.ToCatalog());
        if (result.IsFailure)
        {
            return Result.Failure(result.Error);
        }

        if (result.Value.HasValue)
        {
            ReplaceCatalog(result.Value.Value);
        }

        return Result.Success();
    }

    private void ApplySyncStatus(FifoSyncStatus status)
    {
        SyncStatusLine = status.Message;
        IsSyncSupported = status.IsSupported;
        CanCreateSyncIdentity = status.IsSupported && !status.HasIdentity;
        CanUnlockSync = status.IsSupported && status.HasIdentity && !status.IsUnlocked;
        CanExportSyncIdentity = status.IsSupported && status.HasIdentity;
        CanSyncNow = status.IsSupported && status.IsUnlocked && !status.HasConflict;
        CanDisconnectSync = status.IsSupported && status.HasIdentity;
        HasSyncConflict = status.IsSupported && status.HasConflict;
    }
}
