#pragma warning disable IDE1006 // Disable naming warning
namespace ACdb.Model.Authentication;

internal class RegisterResponse
{
    public int status { get; set; }
    public string api_key { get; set; }
    public string username { get; set; }
    public string client_id { get; set; }
    public string message { get; set; }
    public string client_version { get; set; }
    public string plugin_min_version { get; set; }
    public string client_min_version { get; set; }
}
