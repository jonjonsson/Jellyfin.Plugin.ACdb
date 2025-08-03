using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services.Scheduling;

internal class SchedulingManager
{
    private readonly ITaskManager _taskManager;
    private readonly string _key = PluginConfig.ScheduledTaskKey;


    public SchedulingManager(ITaskManager taskManager)
    {
        _taskManager = taskManager;
    }

    public int? GetSecondsSinceLastScheduledRun()
    {
        IScheduledTaskWorker task = _taskManager.ScheduledTasks.FirstOrDefault(x => x.ScheduledTask.Key == _key);
        if (task == null)
        {
            return null;
        }

        if (task.LastExecutionResult == null)
        {
            return null;
        }

        DateTimeOffset? lastRun = task.LastExecutionResult.EndTimeUtc;
        if (!lastRun.HasValue)
        {
            return null;
        }

        int secondsSinceRan = (int)(DateTimeOffset.UtcNow - lastRun.Value).TotalSeconds;
        return secondsSinceRan;
    }

    public int? GetSecondsUntilNextRun()
    {
        IScheduledTaskWorker task = _taskManager.ScheduledTasks.FirstOrDefault(x => x.ScheduledTask.Key == _key);
        if (task == null)
        {
            LogManager.Warning("Can not find task");
            return null;
        }

        if (task.Triggers is null || task.Triggers.Count == 0)
        {
            return null;
        }

        TaskTriggerInfo trigger = task.Triggers[0];
        long? intervalTicks = trigger.IntervalTicks;

        if (!intervalTicks.HasValue)
        {
            LogManager.Error("IntervalTicks is null");
            return null;
        }

        int? lastRan = GetSecondsSinceLastScheduledRun();
        if (lastRan == null)
        {
            return null;
        }

        int? nextRun = (int)TimeSpan.FromTicks(intervalTicks.Value).TotalSeconds - lastRan;
        return nextRun;
    }

    public string ConvertSecondsToHumanReadable(int sec)
    {

        int hours = sec / 3600;
        sec %= 3600;
        int minutes = sec / 60;
        sec %= 60;

        string hourString = hours == 1 ? "hour" : "hours";
        string minuteString = minutes == 1 ? "minute" : "minutes";
        string secondString = sec == 1 ? "second" : "seconds";

        if (hours > 0)
        {
            return $"{hours} {hourString}, {minutes} {minuteString} and {sec} {secondString}";
        }
        else if (minutes > 0)
        {
            return $"{minutes} {minuteString} and {sec} {secondString}";
        }
        else
        {
            return $"{sec} {secondString}";
        }
    }

    public void SetInterval(int minutes)
    {
        List<IScheduledTaskWorker> allTasks = _taskManager.ScheduledTasks.ToList();

        if (allTasks.Count == 0)
        {
            LogManager.Error("No tasks found, can not set interval");
            return;
        }

        foreach (IScheduledTaskWorker task in allTasks)
        {
            if (task.ScheduledTask.Key == _key)
            {
                List<TaskTriggerInfo> newTriggers =
                [
                    new TaskTriggerInfo
                    {
                        Type = TaskTriggerInfo.TriggerInterval,
                        IntervalTicks = TimeSpan.FromMinutes(minutes).Ticks,
                        MaxRuntimeTicks = TimeSpan.FromMinutes(60).Ticks,
                    },
                    new TaskTriggerInfo
                    {
                        Type = TaskTriggerInfo.TriggerStartup,
                        MaxRuntimeTicks = TimeSpan.FromMinutes(60).Ticks,
                    }
                ];

                if (task.Triggers.Count == 2 &&
                    task.Triggers.Any(t => t.Type == TaskTriggerInfo.TriggerInterval && t.IntervalTicks == newTriggers[0].IntervalTicks) &&
                    task.Triggers.Any(t => t.Type == TaskTriggerInfo.TriggerStartup))
                {
                    return;
                }

                LogManager.Warning("Triggers are not default, resetting them");
                task.Triggers = newTriggers.ToArray();
            }
        }
    }


    internal async Task ExecuteJobTaskAsync()
    {
        IScheduledTaskWorker task = _taskManager.ScheduledTasks.FirstOrDefault(x => x.ScheduledTask.Key == _key);

        if (task is null)
        {
            LogManager.Error($"Cannot execute the task because it was not found (key: {_key})");
            return;
        }

        IProgress<double> progress = new Progress<double>();
        CancellationToken cancellationToken = new();
        await task.ScheduledTask.ExecuteAsync(progress, cancellationToken);

    }

    public void RemoveAllScheduledJobs()
    {
        try
        {
            List<IScheduledTaskWorker> allTasks = _taskManager.ScheduledTasks.ToList();

            if (allTasks.Count == 0)
            {
                LogManager.Warning("No tasks found to remove");
                return;
            }

            foreach (IScheduledTaskWorker task in allTasks)
            {
                if (task.ScheduledTask.Key == _key)
                {
                    task.Triggers = []; 
                    LogManager.Info($"All triggers for task with key {_key} have been removed.");
                }
            }
        }
        catch (Exception ex)
        {
            LogManager.Error($"An error occurred while removing scheduled jobs: {ex.Message}");
        }
    }



}
