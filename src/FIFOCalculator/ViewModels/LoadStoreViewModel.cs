using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.UI;

namespace FIFOCalculator.ViewModels;

public class LoadStoreViewModel : ViewModelBase
{
    private readonly DataEntryViewModel dataEntryViewModel;
    public ReactiveCommand<Unit, Unit> New { get; set; }

    public ReactiveCommand<Unit, Unit> Open { get; }

    public ReactiveCommand<Unit, Unit> Save { get; }

    public LoadStoreViewModel(DataEntryViewModel dataEntryViewModel, IFileSystemPicker storage, INotificationService notificationService)
    {
        this.dataEntryViewModel = dataEntryViewModel;

        Open = ReactiveCommand.CreateFromTask(async () =>
        {
            var pickResult = await storage.PickForOpen();
            if (pickResult.IsFailure) return;

            var maybeSource = pickResult.Value;
            if (maybeSource.HasNoValue) return;

            var bytesResult = await maybeSource.Value.ReadAll();
            if (bytesResult.IsFailure)
            {
                await notificationService.Show(bytesResult.Error, Maybe<string>.None);
                return;
            }

            using var ms = new MemoryStream(bytesResult.Value);
            var catalogResult = await EntryStore.Load(ms);
            if (catalogResult.IsFailure)
            {
                await notificationService.Show(catalogResult.Error, Maybe<string>.None);
                return;
            }

            LoadCatalog(catalogResult.Value);
        });

        Save = ReactiveCommand.CreateFromTask(async () =>
        {
            var maybeDest = await storage.PickForSave("Accounts", Maybe.From(".txt"));
            if (maybeDest.HasNoValue) return;

            var stream = await ToStream();
            var byteSource = ByteSource.FromStream(stream);
            var result = await maybeDest.Value.SetContents(byteSource);
            if (result.IsFailure)
            {
                await notificationService.Show(result.Error, Maybe<string>.None);
            }
        });

        New = ReactiveCommand.Create(() =>
        {
            dataEntryViewModel.Inputs.Load(Enumerable.Empty<Entry>());
            dataEntryViewModel.Outputs.Load(Enumerable.Empty<Entry>());
        });
    }

    private void LoadCatalog(EntryCatalog catalog)
    {
        dataEntryViewModel.Inputs.Load(catalog.Inputs);
        dataEntryViewModel.Outputs.Load(catalog.Outputs);
    }

    private async Task<Stream> ToStream()
    {
        var memoryStream = new MemoryStream();
        var entryCatalog = new EntryCatalog(dataEntryViewModel.Inputs.ToEntries().ToList(), dataEntryViewModel.Outputs.ToEntries().ToList());
        await EntryStore.Save(memoryStream, entryCatalog);
        memoryStream.Position = 0;
        return memoryStream;
    }
}