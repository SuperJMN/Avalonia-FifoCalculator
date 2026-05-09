using FIFOCalculator.Sync;

namespace FIFOCalculator.ViewModels;

public sealed class FifoSyncConflictDialogViewModel(FifoSyncConflict conflict) : ViewModelBase
{
    public string LocalSummary => conflict.LocalSummary;

    public string RemoteSummary => conflict.RemoteSummary;
}
