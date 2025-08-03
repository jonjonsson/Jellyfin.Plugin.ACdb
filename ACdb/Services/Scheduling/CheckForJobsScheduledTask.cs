using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace ACdb.Services.Scheduling;

public class CheckForJobsScheduledTask : IScheduledTask, IConfigurableScheduledTask
{
    public string Name { get; set; } = PluginConfig.ScheduledTaskName; // Needed for IScheduledTask
    public string Description { get; set; } = PluginConfig.ScheduledTaskDescription; // Needed for IScheduledTask
    public string Category { get; set; } = PluginConfig.ScheduledTaskCategory; // Needed for IScheduledTask
    public string Key { get; set; } = PluginConfig.ScheduledTaskKey; // Needed for IScheduledTask
    public bool IsEnabled => true; // Needed for IConfigurableScheduledTask
    public bool IsHidden => false; // Needed for IConfigurableScheduledTask
    public bool IsLogged => true; // Needed for IConfigurableScheduledTask

    public CheckForJobsScheduledTask()
    {
        LogManager.Info($"Scheduled task {Name} initialized.");
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        double currentProgress = 10;
        progress.Report(currentProgress);
        await Manager.PullDataAsync(progress, currentProgress);
        Manager.ResetScheduleInterval(); 
        progress.Report(100);

    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [];
    }

}

