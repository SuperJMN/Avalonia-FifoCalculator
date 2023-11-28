using DynamicData;
using Serilog.Core;
using Serilog.Events;

namespace FIFOCalculator.ViewModels;

public class DynamicDataSink : ILogEventSink, IObservableLogger
{
    private readonly SourceList<LogEvent> events = new();

    public IObservableList<LogEvent> Events => events.AsObservableList();

    public void Emit(LogEvent logEvent)
    {
        events.Add(logEvent);
    }
}