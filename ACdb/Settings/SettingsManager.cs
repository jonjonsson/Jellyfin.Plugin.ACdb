using ACdb.Services;
using ACdb.Settings.Model;
using MediaBrowser.Controller;
using System;


namespace ACdb.Settings;

internal static class SettingsManager
{



    private static GeneralOptions _settings;


    private static string _name;

    public static void Initialize(IServerApplicationHost applicationHost, GeneralOptions configuration, string name)
    {
        _name = name;
        _settings = configuration;
    }

    private static void SaveSettings()
    {
        if (_settings == null)
        {
            LogManager.Error("Settings object is null.");
            return;
        }
        Plugin.Instance?.UpdateConfiguration(_settings);
    }

    public static void AddCollectionSidToGuid(string sid, Guid guid)
    {
        var list = _settings.CollectionSidToGui;
        int index = list.FindIndex(pair => pair.Key == sid);
        if (index >= 0)
        {
            list[index] = new CollectionSidGuidPair(sid, guid);
        }
        else
        {
            list.Add(new CollectionSidGuidPair(sid, guid));
        }
        SaveSettings();
    }

    public static Guid? GetCollectionGuidBySid(string sid)
    {
        if (_settings?.CollectionSidToGui != null)
        {
            var pair = _settings.CollectionSidToGui.Find(p => p.Key == sid);
            if (pair != null && pair.Key == sid)
            {
                return pair.Value;
            }
        }
        return null;
    }

    public static void RemoveCollectionSidToGuid(string sid)
    {
        var list = _settings.CollectionSidToGui;
        var index = list.FindIndex(pair => pair.Key == sid);
        if (index >= 0)
        {
            list.RemoveAt(index);
            SaveSettings();
        }
    }

    private static void RemoveCollectionSidToGuid(Guid id)
    {
        string sid = GetCollectionSidByGuid(id);
        if (sid == null)
        {
            return;
        }
        var list = _settings.CollectionSidToGui;
        var index = list.FindIndex(pair => pair.Key == sid);
        if (index >= 0)
        {
            list.RemoveAt(index);
            SaveSettings();
        }
    }

    public static string GetCollectionSidByGuid(Guid guid)
    {
        if (_settings?.CollectionSidToGui != null)
        {
            foreach (var pair in _settings.CollectionSidToGui)
            {
                if (pair.Value == guid)
                {
                    return pair.Key;
                }
            }
        }
        return null;
    }

    public static void CollectionRemovedCleanup(Guid guid)
    {
        RemoveCollectionWithDateAddedSortName(guid);
        RemoveCollectionSidToGuid(guid);
    }

    public static void CollectionRemovedCleanup(string collection_sid)
    {
        RemoveCollectionWithDateAddedSortName(collection_sid);
        RemoveCollectionSidToGuid(collection_sid);
    }

    public static void SetApiKey(string apiKey)
    {
        if (_settings == null)
        {
            LogManager.Error("Settings object is null.");
            return;
        }
        _settings.ApiKey = apiKey;
        SaveSettings();
    }

    public static string GetApiKey()
    {
        return _settings?.ApiKey ?? string.Empty;
    }

    public static void ResetSettings()
    {
        _settings = new GeneralOptions();
        SaveSettings();
    }

    public static bool IsCollectionWithDateAddedSortName(Guid id)
    {
        return IsCollectionSidWithDateAddedSortName(GetCollectionSidByGuid(id));
    }

    public static string GetLastLibraryHash()
    {
        return _settings?.LastLibraryHash;
    }

    public static void SetLastLibraryHash(string hash)
    {
        _settings.LastLibraryHash = hash;
        SaveSettings();
    }

    public static void AddCollectionSidWithDateAddedSortName(string sid)
    {
        _settings.CollectionsSidWithDateAddedSortNames.Add(sid);
        SaveSettings();
    }

    public static void RemoveCollectionWithDateAddedSortName(string sid)
    {
        if (sid == null || IsCollectionSidWithDateAddedSortName(sid) == false)
        {
            return;
        }

        if (_settings.CollectionsSidWithDateAddedSortNames.Remove(sid))
        {
            SaveSettings();
        }
    }

    public static void RemoveCollectionWithDateAddedSortName(Guid id)
    {
        string sid = GetCollectionSidByGuid(id);
        if (sid == null || IsCollectionSidWithDateAddedSortName(sid) == false)
        {
            return;
        }
        if (_settings.CollectionsSidWithDateAddedSortNames.Remove(GetCollectionSidByGuid(id)))
        {
            SaveSettings();
        }
    }

    public static bool IsCollectionSidWithDateAddedSortName(string sid)
    {
        if (sid == null)
        {
            LogManager.Error("Collection SID is null when asking if has date added sort name.");
            return false;
        }
        return _settings.CollectionsSidWithDateAddedSortNames.Contains(sid);
    }

    public static void AddSync(DateTime syncTime)
    {
        if (_settings == null)
        {
            LogManager.Error("Settings object is null.");
            return;
        }

        _settings.LastSynced ??= [];
        _settings.LastSynced.Insert(0, syncTime);

        if (_settings.LastSynced.Count > 10) // Keep last 10 syncs
        {
            _settings.LastSynced.RemoveAt(_settings.LastSynced.Count - 1);
        }

        SaveSettings();
    }

    public static DateTime? GetLastSync()
    {
        if (_settings?.LastSynced == null || _settings.LastSynced.Count == 0)
        {
            return null;
        }
        return _settings.LastSynced[0];
    }


}

