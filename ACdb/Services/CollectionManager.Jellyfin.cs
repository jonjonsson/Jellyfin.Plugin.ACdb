using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace ACdb.Services;

internal static class CollectionManager
{
    private static ICollectionManager _collectionManager;

    public static void Initialize(ICollectionManager collectionManager)
    {
        _collectionManager = collectionManager;
    }

    public static async Task AddToCollectionAsync(BaseItem collection, List<string> itemIds)
    {
        if (collection == null)
        {
            LogManager.Error("Collection should not be null.");
            return;
        }
        if (itemIds == null || itemIds.Count == 0)
        {
            LogManager.Info("No items to add to collection.");
        }
        
        Guid[] itemIdsGuids = itemIds.Select(id => Guid.Parse(id)).ToArray(); //Convert List<string> to Guid[] for Jellyfin compatibility
        await _collectionManager.AddToCollectionAsync(collection.Id, itemIdsGuids);
    }

    public static void RemoveFromCollection(BaseItem item, List<string> itemIds)
    {
        Guid[] itemIdsGuids = itemIds.Select(id => Guid.Parse(id)).ToArray(); // Convert List<string> to Guid[] for Jellyfin compatibility
        _collectionManager.RemoveFromCollectionAsync(item.Id, itemIdsGuids);
    }

    public static async Task<BaseItem> CreateCollectionAsync(string name, List<string> itemIdList)
    {
        if (itemIdList.Count == 0)
            return null;

        IReadOnlyList<string> strings = itemIdList.Select(id => id.ToString()).ToList(); // Convert long[] to IReadOnlyList<string> for Jellyfin compatibility

        var options = new CollectionCreationOptions
        {
            Name = name,
            ItemIdList = strings,
        };

        return await _collectionManager.CreateCollectionAsync(options);
    }

    public static bool IsCollection(BaseItem item)
    {
        return item.GetType() == typeof(BoxSet);
    }


}

