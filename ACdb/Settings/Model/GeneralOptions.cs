using MediaBrowser.Model.Plugins;
using System;
using System.Collections.Generic;

namespace ACdb.Settings.Model;

public class CollectionSidGuidPair
{
    public string Key { get; set; }
    public Guid Value { get; set; }

    public CollectionSidGuidPair() { }
    public CollectionSidGuidPair(string key, Guid value)
    {
        Key = key;
        Value = value;
    }
}

public class GeneralOptions
     : BasePluginConfiguration
{
    public string ApiKey { get; set; }
    public GeneralOptions() 
    {
    }

    public string TestKey { get; set; }

    public List<CollectionSidGuidPair> CollectionSidToGui { get; set; } = [];

    public HashSet<Guid> CollectionsWithPosters { get; set; } = [];

    public HashSet<string> CollectionsSidWithDateAddedSortNames { get; set; } = [];

    public List<DateTime> LastSynced { get; set; }

}
