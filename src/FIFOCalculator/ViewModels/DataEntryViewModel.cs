using Zafiro.Avalonia.Interfaces;

namespace FIFOCalculator.ViewModels;

public class DataEntryViewModel : ViewModelBase
{
    public DataEntryViewModel(INotificationService notificationService, IStorage storage)
    {
        Inputs = new EntryEditorViewModel("Inputs");
        Outputs = new EntryEditorViewModel("Outputs");
        LoadStoreViewModel = new LoadStoreViewModel(this, storage, notificationService);
    }

    public LoadStoreViewModel LoadStoreViewModel { get; set; }

    public EntryEditorViewModel Inputs { get; }
    public EntryEditorViewModel Outputs { get; }
}