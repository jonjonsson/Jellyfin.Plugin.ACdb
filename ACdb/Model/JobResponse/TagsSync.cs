#pragma warning disable IDE1006 // Disable naming warning

using System.Collections.Generic;

namespace ACdb.Model.JobResponse
{
    public enum TagTypeEnum
    {
        None = 0,
        MoviesAndSeries = 1,
        Collection = 2,
    }


    internal partial class Response
    {
        public class TagJob
        {
            public string tag { get; set; }
            public bool delete { get; set; } = false;
            public string collection_sid { get; set; }
            public TagTypeEnum tag_type { get; set; } = TagTypeEnum.None;
            public List<string> imdb_ids { get; set; }
            public List<string> excluded_libraries { get; set; }
            public int limit { get; set; } = 0;
            public LimitType? limit_type { get; set; }

            public TagJob()
            {
                imdb_ids = null;
            }
        }

    }
}