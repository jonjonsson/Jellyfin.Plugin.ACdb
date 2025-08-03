#pragma warning disable IDE1006 // Disable naming warning
namespace ACdb.Model.JobResponse;

internal partial class Response
{
    public string job_id { get; set; }
    public int status { get; set; } // HTTP Status code
    public string message { get; set; }
    public int api_version { get; set; }
    public string min_client_version { get; set; }
    public CollectionsSync collections_sync { get; set; }

}
