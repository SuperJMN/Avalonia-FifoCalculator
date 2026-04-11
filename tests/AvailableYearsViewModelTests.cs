using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.ViewModels;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Mutable;
using Zafiro.UI;

namespace TestProject1;

public class AvailableYearsViewModelTests
{
    [Fact]
    public void Simulation_includes_years_even_when_outputs_are_empty()
    {
        var dataEntry = CreateDataEntryViewModel();
        var sut = new SimulationViewModel(dataEntry, new SilentNotificationService());

        dataEntry.Inputs.Load([new Entry(new DateTime(2024, 1, 10), 1, 100m)]);
        dataEntry.Outputs.Load([]);

        SpinWait.SpinUntil(() => sut.AvailableYears.Count > 0, TimeSpan.FromSeconds(1));
        sut.AvailableYears.Should().Equal(2024);
    }

    [Fact]
    public void Simulation_includes_years_when_both_inputs_and_outputs_have_entries()
    {
        var dataEntry = CreateDataEntryViewModel();
        var sut = new SimulationViewModel(dataEntry, new SilentNotificationService());

        dataEntry.Inputs.Load([new Entry(new DateTime(2023, 1, 10), 1, 100m)]);
        dataEntry.Outputs.Load([new Entry(new DateTime(2024, 1, 10), 1, 100m)]);

        SpinWait.SpinUntil(() => sut.AvailableYears.Count > 0, TimeSpan.FromSeconds(1));
        sut.AvailableYears.Should().Equal(2023, 2024);
    }

    [Fact]
    public void Fiscal_year_includes_years_even_when_outputs_are_empty()
    {
        var dataEntry = CreateDataEntryViewModel();
        var sut = new FiscalYearViewModel(dataEntry, new SilentNotificationService());

        dataEntry.Inputs.Load([new Entry(new DateTime(2023, 2, 1), 1, 100m)]);
        dataEntry.Outputs.Load([]);

        SpinWait.SpinUntil(() => sut.AvailableYears.Count > 0, TimeSpan.FromSeconds(1));
        sut.AvailableYears.Should().Equal(2023);
    }

    private static DataEntryViewModel CreateDataEntryViewModel() =>
        new(new SilentNotificationService(), new DummyFileSystemPicker());

    private sealed class SilentNotificationService : INotificationService
    {
        public Task Show(string message, Maybe<string> title) => Task.CompletedTask;
    }

    private sealed class DummyFileSystemPicker : IFileSystemPicker
    {
        public Task<Result<Maybe<INamedByteSource>>> PickForOpen(params FileTypeFilter[] filters) =>
            throw new NotSupportedException();

        public Task<Result<IEnumerable<INamedByteSource>>> PickForOpenMultiple(params FileTypeFilter[] filters) =>
            throw new NotSupportedException();

        public Task<Maybe<IMutableFile>> PickForSave(string desiredName, Maybe<string> defaultExtension, params FileTypeFilter[] filters) =>
            throw new NotSupportedException();

        public Task<Maybe<IMutableDirectory>> PickFolder() =>
            throw new NotSupportedException();
    }
}
