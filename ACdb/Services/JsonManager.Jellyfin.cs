using System.IO;
using System.Text.Json;

namespace ACdb.Services;

internal static class JsonManager // For Emby compatibility
{
    private static readonly JsonSerializerOptions CachedOptions = new() { WriteIndented = true };

    public static string SerializeToString<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, CachedOptions);
    }

    public static void SerializeToStream<T>(T obj, Stream stream)
    {
        JsonSerializer.Serialize(stream, obj, CachedOptions);
    }

    public static T DeserializeFromString<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json);
    }

    public static T DeserializeFromStream<T>(Stream stream)
    {
        return JsonSerializer.Deserialize<T>(stream);
    }
}
