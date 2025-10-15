#pragma warning disable IDE1006 // Disable naming styles, can't use PascalCase for property names without complicating things (not able to install newtonsoft.json I assume?)
using MediaBrowser.Model.Entities;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ACdb.Model.JobResponse
{
    public class ACdbImageInfo
    {
        [JsonConverter(typeof(JsonStringEnumConverter))] // Case insensitive enum converter because in Jellyfin the enum values start with capital letters
        public ImageType type { get; set; }
        public string url { get; set; }
        public bool remove { get; set; }
    }

    public class ImagesResponse
    {
        public List<ACdbImageInfo> images { get; set; } = new List<ACdbImageInfo>();
    }
}