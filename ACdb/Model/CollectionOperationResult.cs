using MediaBrowser.Controller.Entities;
using System.Collections.Generic;


namespace ACdb.Model
{
    public class CollectionOperationResult
    {
        public List<string> MissingImdbIds { get; set; }
        public List<string> FoundImdbIds { get; set; }
        public List<string> FoundItemIds { get; set; }
        public List<BaseItem> FoundItems { get; set; }

        public CollectionOperationResult()
        {
            MissingImdbIds = new List<string>();
            FoundImdbIds = new List<string>();
            FoundItemIds = new List<string>();
            FoundItems = new List<BaseItem>();
        }
    }
}
