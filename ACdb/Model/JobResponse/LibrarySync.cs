using System.Collections.Generic;

namespace ACdb.Model.JobResponse
{

    public class LibraryImage
    {
        public string name { get; set; }
        public string poster_id { get; set; }
    }


    public class LibrarySync
    {
        public List<LibraryImage> images { get; set; }
    }
}



