#pragma warning disable IDE1006 // Disable naming warning
using System.Collections.Generic;

namespace ACdb.Model.JobResponse
{
    public enum ItemSorting
    {
        None = 0,           // Plugin takes no action
        SortName = 1,
        PremierDate = 2,
        DateAdded = 3,
    }

    public enum LimitType
    {
        None = 0,
        Random = 1,
        DateAdded = 2,
    }

    internal partial class Response
    {
        internal class CollectionJob
        {
            public string name { get; set; }
            public bool delete { get; set; }
            public string description { get; set; }
            public List<string> imdb_ids { get; set; }
            public string cid { get; set; }
            public string collection_sid { get; set; }
            public string sort_name { get; set; }
            public bool? sort_to_top { get; set; }
            public bool? set_poster { get; set; }
            public ItemSorting? item_sorting { get; set; }
            public List<string> excluded_libraries { get; set; }
            public int limit { get; set; } = 0;
            public LimitType? limit_type { get; set; }

        }

    }
}
