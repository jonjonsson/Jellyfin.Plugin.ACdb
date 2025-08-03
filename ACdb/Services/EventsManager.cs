using ACdb.Model.Reporting;
using System;
using System.Collections.Generic;

namespace ACdb.Services;

public enum EventType
{
    General, // todo next not in use now
    Progress
}

public abstract class BaseEventArgs
{
    public string Description { get; set; }
    public string Title { get; set; }
    public string HyperLink { get; set; }

    public LogTypeEnum LogType { get; set; }
}

public class ActivityLogEventArgs : BaseEventArgs
{
    public double? Progress { get; set; }
}

public static class EventsManager
{
    private static readonly Dictionary<EventType, Action<BaseEventArgs>> eventHandlers = [];

    public static void Initialize() {  }

    public static void RegisterEventHandler(EventType eventType, Action<BaseEventArgs> handler)
    {
        if (!eventHandlers.ContainsKey(eventType))
        {
            eventHandlers[eventType] = handler;
        }
        else
        {
            eventHandlers[eventType] += handler;
        }
    }

    public static void UnregisterEventHandler(EventType eventType, Action<BaseEventArgs> handler)
    {
        if (eventHandlers.ContainsKey(eventType))
        {
            eventHandlers[eventType] -= handler;
            if (eventHandlers[eventType] == null)
            {
                eventHandlers.Remove(eventType);
            }
        }
    }

    public static void TriggerEvent(EventType eventType, BaseEventArgs args)
    {
        if (eventHandlers.ContainsKey(eventType))
        {
            eventHandlers[eventType]?.Invoke(args);
        }
        else
        {
            LogManager.Warning($"No handlers registered for event '{eventType}'");
        }
    }
}
