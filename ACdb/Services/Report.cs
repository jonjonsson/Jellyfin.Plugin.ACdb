using ACdb.Model.Reporting;
using System.Text;

namespace ACdb.Services;


internal partial class Report // todo clean this up, loop in summarize does nothing after recent changes
{
    public JobReport JobReport { get; set; }

    public Report()
    {
        JobReport = new JobReport();
    }

    public void AddToLog(LogTypeEnum logType, string msg, ActivityLogEventArgs activityLogEvent)
    {
        LogMsg logMsg = new()
        {
            log_msg = msg,
            log_type = logType,
        };

        if (activityLogEvent.Description != null)
        {
            logMsg.log_msg = $"{msg}: {activityLogEvent.Description}";
        }

        JobReport.log_msgs.Add(logMsg);
        LogManager.LogEvent(logType, msg, activityLogEvent);
    }


    public void AddToLog(LogTypeEnum logType, string msg, CollectionJobReport collectionReport, ActivityLogEventArgs activityLogEvent = null)
    {
        LogMsg logMsg = new()
        {
            log_msg = msg,
            log_type = logType,
            cid = collectionReport.cid,
            collection_name = collectionReport.name,
            collection_sid = collectionReport.collection_sid
        };

        if (activityLogEvent != null && activityLogEvent.Description != null)
        {
            logMsg.log_msg = $"{msg}: {activityLogEvent.Description}";
        }

        JobReport.log_msgs.Add(logMsg);
        LogManager.LogEvent(logType, msg, activityLogEvent);
    }

    public string Summarize()
    {
        StringBuilder summary = new();

        summary.AppendLine(new string('-', 50));
        summary.AppendLine($"Error: {JobReport.error}"); // todo next this does not work, always false
        summary.AppendLine($"Start Time: {JobReport.start_time}");
        summary.AppendLine($"End Time: {JobReport.end_time}");
        summary.AppendLine(new string('-', 50));

        foreach (var collectionReport in JobReport.collection_reports)
        {
            summary.AppendLine($"Collection Report: {collectionReport.name}");
            summary.AppendLine($"  Start Time: {collectionReport.start_time}");
            double duration = (collectionReport.end_time - collectionReport.start_time).TotalSeconds;
            summary.AppendLine($"  Is New: {collectionReport.is_new}");
            summary.AppendLine($"  Deleted: {collectionReport.deleted}");
            summary.AppendLine($"  Sync paused: {collectionReport.paused}");
            summary.AppendLine($"  Added Count: {collectionReport.added_count}");
            summary.AppendLine($"  Removed Count: {collectionReport.removed_count}");
            summary.AppendLine($"  Missing IMDBs Count: {collectionReport.missing_imdbs.Count}");
            summary.AppendLine(new string('-', 50));
        }
        return summary.ToString();
        
    }

}
