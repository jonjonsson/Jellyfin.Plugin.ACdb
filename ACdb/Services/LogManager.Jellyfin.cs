using ACdb.Model.Reporting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace ACdb.Services;
public class Event(LogTypeEnum logType, string message) // For Emby compatibility
{
    public LogTypeEnum LogType { get; set; } = logType;
    public string Message { get; set; } = message;
}

public class EventList // For Emby compatibility
{
    private readonly List<Event> _events = [];

    public void Add(Event evt)
    {
        _events.Add(evt);
    }

    public void Reset()
    {
        _events.Clear();
    }

    public IReadOnlyList<Event> GetAll()
    {
        var eventsCopy = new List<Event>(_events);
        _events.Clear();
        return eventsCopy.AsReadOnly();
    }

    public int Count => _events.Count;
}


internal static class LogManager // For Emby compatibility
{
    private static ILogger<Plugin> logging;
    public static EventList EventList = new();

    public static void Initialize(ILogger<Plugin> logger)
    {
        logging = logger;
    }


    public static void Log(LogTypeEnum logTypeEnum, string message)
    {
        AddToLog(logTypeEnum, message);
    }

    public static void Info(string message)
    {
        AddToLog(LogTypeEnum.info, message);
    }

    public static void Warning(string message)
    {
        AddToLog(LogTypeEnum.warning, message);
    }

    public static void Error(string message)
    {
        LogEvent(LogTypeEnum.error, message);
    }

    public static void Error(string message, ActivityLogEventArgs progressEvent)
    {
        LogEvent(LogTypeEnum.error, message, progressEvent);
    }

    public static void LogEvent(LogTypeEnum logTypeEnum, string message)
    {
        LogEvent(logTypeEnum, message, null);
    }

    public static void LogEvent(LogTypeEnum logTypeEnum, string message, ActivityLogEventArgs progressEvent)
    {
        progressEvent ??= new ActivityLogEventArgs();

        progressEvent.LogType = logTypeEnum;

        string msgToLog = message;
        if (progressEvent.Description != null)
        {
            msgToLog = $"{message}: {progressEvent.Description}";
        }

        AddToLog(logTypeEnum, msgToLog);

        progressEvent.Title ??= message;

        EventsManager.TriggerEvent(EventType.Progress, progressEvent);
        EventList.Add(new Event(logTypeEnum, message));
    }

    private static void AddToLog(LogTypeEnum logTypeEnum, string message)  // For Emby compatibility
    {
        switch (logTypeEnum)
        {
            case LogTypeEnum.error:
                logging.LogError(message);
                break;
            case LogTypeEnum.info:
                logging.LogInformation(message);
                break;
            case LogTypeEnum.warning:
                logging.LogWarning(message);
                break;
            case LogTypeEnum.debug:
                logging.LogDebug(message);
                break;
            case LogTypeEnum.fatal:
                logging.LogCritical(message);
                break;
            default:
                logging.LogInformation($"[{logTypeEnum}] {message}");
                break;
        }
    }

}
