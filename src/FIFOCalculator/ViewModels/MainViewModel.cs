using System.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Zafiro.UI;

namespace FIFOCalculator.ViewModels;

public class MainViewModel : ReactiveObject
{
    public MainViewModel(IFilePicker storage, INotificationService notificationService)
    {
        var dataEntryViewModel = new DataEntryViewModel(notificationService, storage);
        DataEntry = dataEntryViewModel;
        
        Sections = new[]
        {
            new Section("Data entry", dataEntryViewModel),
            new Section("Simulate", new SimulationViewModel(() => dataEntryViewModel.Inputs.ToEntries(), () => dataEntryViewModel.Outputs.ToEntries(), notificationService))
        };

        ActiveSection = Sections.First();
    }

    public DataEntryViewModel DataEntry { get; set; }

    public Section[] Sections { get; set; }

    [Reactive]
    public Section ActiveSection { get; set; }
}