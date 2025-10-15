#pragma warning disable IDE1006 // Disable naming warning
using System.Collections.Generic;

namespace ACdb.Model.JobResponse;

public enum ItemSorting
{
    None = 0,           // Plugin takes no action
    SortName = 1,
    PremierDate = 2,
    DateAdded = 3,
}

internal partial class Response
{
    internal class Collection
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
        public bool? no_poster { get; set; } // todo might not use
        public string poster_id { get; set; }
        public ItemSorting? item_sorting { get; set; }
    }

}
