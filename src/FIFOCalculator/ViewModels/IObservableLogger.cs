using DynamicData;
using Serilog.Events;

namespace FIFOCalculator.ViewModels;

public interface IObservableLogger
{
    IObservableList<LogEvent> Events { get; }
}