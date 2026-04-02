using ACdb.Model.Reporting;
using System.Text;

namespace ACdb.Services
{

    internal partial class Report
    {
        public JobReport JobReport { get; set; }

        public Report()
        {
            JobReport = new JobReport();
        }

        public string Summarize()
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine(new string('-', 50));
            summary.AppendLine($"Start Time: {JobReport.start_time}");
            summary.AppendLine($"End Time: {JobReport.end_time}");
            summary.AppendLine($"Duration: {Manager.Utils.HumanReadablerDuration(JobReport.start_time, JobReport.end_time)}");
            return summary.ToString();
        }

    }
}
