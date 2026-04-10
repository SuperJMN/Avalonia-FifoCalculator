using Zafiro.UI;
using Zafiro.UI.Shell.Utils;

namespace FIFOCalculator.ViewModels;

[Section(icon: "fa-table", sortIndex: 0, FriendlyName = "Data Entry")]
public class DataEntryViewModel : ViewModelBase
{
    public DataEntryViewModel(INotificationService notificationService, IFileSystemPicker filePicker)
    {
        Inputs = new EntryEditorViewModel("Inputs");
        Outputs = new EntryEditorViewModel("Outputs");
        LoadStoreViewModel = new LoadStoreViewModel(this, filePicker, notificationService);
    }

    public LoadStoreViewModel LoadStoreViewModel { get; set; }

    public EntryEditorViewModel Inputs { get; }
    public EntryEditorViewModel Outputs { get; }
}