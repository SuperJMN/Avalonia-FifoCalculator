using System;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Sync;
using ReactiveUI;
using Serilog;
using Zafiro.UI.Shell;

namespace FIFOCalculator.ViewModels;

public sealed class AppViewModel : ViewModelBase
{
    private readonly IFifoSyncService syncService;
    private readonly SettingsViewModel settingsViewModel;
    private readonly ILogger logger;
    private bool isInitializing = true;
    private int initialized;

    public AppViewModel(IShell shell, IFifoSyncService syncService, SettingsViewModel settingsViewModel, ILogger? logger = null)
    {
        Shell = shell;
        this.syncService = syncService;
        this.settingsViewModel = settingsViewModel;
        this.logger = logger ?? Log.Logger;
        Initialize = ReactiveCommand.CreateFromTask(InitializeOnce);
    }

    public IShell Shell { get; }

    public bool IsInitializing
    {
        get => isInitializing;
        private set => this.RaiseAndSetIfChanged(ref isInitializing, value);
    }

    public ReactiveCommand<Unit, Result> Initialize { get; }

    private async Task<Result> InitializeOnce()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 1)
        {
            return Result.Success();
        }

        try
        {
            var sync = InitializeSync();
            var load = await settingsViewModel.LoadSavedData.Execute().ToTask();
            await sync;

            return load.IsFailure ? Result.Failure(load.Error) : Result.Success();
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private async Task InitializeSync()
    {
        try
        {
            var result = await syncService.Initialize();
            if (result.IsFailure)
            {
                logger.Warning("Failed to initialize FIFO sync: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to initialize FIFO sync.");
        }
    }
}
