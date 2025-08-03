#pragma warning disable IDE1006 // Disable naming styles, can't use PascalCase for property names without complicating things due to having to using json emby interface

using System;
using System.Collections.Generic;


namespace ACdb.Model.Reporting;

internal class CollectionJobReport
{
    public DateTime start_time { get; set; }
    public DateTime end_time { get; set; }
    public string name { get; set; }
    public bool? is_new { get; set; }
    public bool? deleted { get; set; }
    public bool? paused { get; set; }
    public string cid { get; set; }
    public string collection_sid { get; set; }
    public int added_count { get; set; }
    public int removed_count { get; set; }
    public List<string> missing_imdbs { get; set; } = [];
    public List<string> errors { get; set; } = [];
}