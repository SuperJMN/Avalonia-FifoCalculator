using Zafiro.UI;

namespace FIFOCalculator.ViewModels;

public class DataEntryViewModel : ViewModelBase
{
    public DataEntryViewModel(INotificationService notificationService, IFilePicker filePicker)
    {
        Inputs = new EntryEditorViewModel("Inputs");
        Outputs = new EntryEditorViewModel("Outputs");
        LoadStoreViewModel = new LoadStoreViewModel(this, filePicker, notificationService);
    }

    public LoadStoreViewModel LoadStoreViewModel { get; set; }

    public EntryEditorViewModel Inputs { get; }
    public EntryEditorViewModel Outputs { get; }
}