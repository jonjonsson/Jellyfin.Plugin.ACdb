#pragma warning disable IDE1006 // Disable naming warning
using System;

namespace ACdb.Model.Authentication;

internal class RegisterPluginRequest
{
    public string client_id { get => PluginConfig.ClientID; }
    public DateTime timestamp { get => DateTime.Now; }
    public string iv { get; set; }
    public string existing_api_key { get; set; }
    public Version plugin_version { get => PluginConfig.PluginVersion; }
    public string client_version { get => PluginConfig.ClientVersion.ToString(); }
    public string plugin_type { get => PluginConfig.PluginType; }
}
