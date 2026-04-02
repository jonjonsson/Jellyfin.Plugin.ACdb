#pragma warning disable IDE1006 // Disable naming warning

using System;
using System.Collections.Generic;


namespace ACdb.Model.Reporting
{
    internal class JobReport
    {
        public string api_key { get; set; }
        public Version plugin_version { get; set; }
        public string job_id { get; set; }
        public int api_version { get; set; }
        public string client_version { get; set; }
        public DateTime start_time { get; set; }
        public DateTime end_time { get; set; }
    }

}
