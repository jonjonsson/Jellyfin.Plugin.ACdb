#pragma warning disable IDE1006 // Disable naming warning
using ACdb.Model.Reporting;
using System;

namespace ACdb.Model.Reporting;

internal class LogMsg
{
    public LogTypeEnum log_type { get; set; }
    public string log_msg { get; set; }
    public string cid { get; set; }
    public string collection_name { get; set; }
    public string collection_sid { get; set; }
}
