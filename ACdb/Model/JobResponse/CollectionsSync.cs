#pragma warning disable IDE1006 // Disable naming warning
using System.Collections.Generic;

namespace ACdb.Model.JobResponse
{
    internal partial class Response
    {
        internal class CollectionsSync
        {
            public List<CollectionJob> collections { get; set; }
            public bool report_missing { get; set; } = true;
            public List<TagJob> tags { get; set; } 
        }
    }
}
