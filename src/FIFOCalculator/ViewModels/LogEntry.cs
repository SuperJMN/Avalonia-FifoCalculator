using System;
using System.Globalization;
using Serilog.Events;

namespace FIFOCalculator.ViewModels;

public class LogEntry
{
    public LogEntry(LogEvent logEvent)
    {
        Message = logEvent.RenderMessage(CultureInfo.CurrentCulture);
        Timestamp = logEvent.Timestamp;
        Level = logEvent.Level;
    }

    public LogEventLevel Level { get; }

    public DateTimeOffset Timestamp { get; }

    public string Message { get; }
}