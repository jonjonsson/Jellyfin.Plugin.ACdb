using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services.Scheduling
{
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

        private IScheduledTaskWorker Task
        {
            get
            {
                List<IScheduledTaskWorker> tasks = _taskManager.ScheduledTasks
                    .Where(x => x.ScheduledTask.Key == _key)
                    .ToList();
                return tasks.Count > 0 ? tasks[0] : null;
            }
        }

        public bool HasTriggers()
        {
            if (Task is null)
            {
                LogManager.Error("Can not find task");
                return false;
            }
            TaskTriggerInfo[] triggers = Task.Triggers.ToArray();
            if (triggers is null || triggers.Length == 0)
            {
                LogManager.Info("No triggers found for task");
                return false;
            }
            return true;
        }

        public int? GetSecondsUntilNextRun()
        {
            if (Task is null)
            {
                LogManager.Error("Can not find task");
                return null;
            }

            TaskTriggerInfo[] triggers = Task.Triggers.ToArray();

            if (triggers is null || triggers.Length == 0)
            {
                return null;
            }

            if (triggers.Length > 1)
            {
                LogManager.Info("Triggers have been customized");
                return null;
            }

            TaskTriggerInfo trigger = triggers.First();
            long? intervalTicks = trigger.IntervalTicks;

            if (!intervalTicks.HasValue)
            {
                LogManager.Info("Triggers have been customized, use Reset Schedule button to back to default.");
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

        public void ResetToDefault(int minutes)
        {

            List<TaskTriggerInfo> newTriggers = new List<TaskTriggerInfo>
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromMinutes(minutes).Ticks,
                    MaxRuntimeTicks = TimeSpan.FromMinutes(60).Ticks,
                }
            };
            LogManager.Info("Setting default sync schedule.");
            Task.Triggers = newTriggers.ToArray();

            return;

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
            CancellationToken cancellationToken = new CancellationToken();
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
                        task.Triggers = new TaskTriggerInfo[0];
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
}
