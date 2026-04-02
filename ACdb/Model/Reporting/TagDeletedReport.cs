#pragma warning disable IDE1006 // Disable naming warning
using ACdb.Model.JobResponse;

namespace ACdb.Model.Reporting
{
    internal class TagDeletedReport
    {
        public string collection_sid { get; set; }
        public string tag { get; set; }
        public int type { get; set; } // Int of TagTypeEnum
    }
}



