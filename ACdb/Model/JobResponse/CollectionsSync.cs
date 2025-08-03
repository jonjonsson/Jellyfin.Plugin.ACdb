#pragma warning disable IDE1006 // Disable naming warning
using System.Collections.Generic;

namespace ACdb.Model.JobResponse;

internal partial class Response
{
    internal class CollectionsSync
    {
        public List<Collection> collections { get; set; }
    }
}
