
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ACdb.Services;

public class CollectionOperationResult
{
    public List<string> MissingImdbIds { get; set; }
    public List<string> FoundImdbIds { get; set; }

    public List<string> FoundItemIds { get; set; }

    public int FoundCount
    {
        get { return FoundItemIds.Count; }
    }


    public CollectionOperationResult()
    {
        MissingImdbIds = [];
        FoundImdbIds = [];
        FoundItemIds = [];
    }
}


public partial class ACdbUtils
{
    public readonly ILibraryManager LibraryManager;
    public readonly IDirectoryService DirectoryService;
    public readonly IFileSystem FileSystem;
    private readonly User _adminUser;

    public ACdbUtils(ILibraryManager libraryManager, IFileSystem fileSystem, IDirectoryService directoryService, IUserManager userManager)
    {
        LibraryManager = libraryManager;
        DirectoryService = directoryService;
        FileSystem = fileSystem;

        MetadataRefreshOptionsParam = directoryService;

        _adminUser = userManager.Users
            .Where(i => i.HasPermission(PermissionKind.IsAdministrator))
            .First();
    }


    public List<BaseItem> GetItems(List<string> itemIds)
    {
        if (itemIds == null || itemIds.Count == 0)
        {
            return [];
        }

        const int batchSize = 500;
        List<BaseItem> result = [];

        for (int i = 0; i < itemIds.Count; i += batchSize)
        {
            try
            {
                List<string> batch = itemIds.Skip(i).Take(batchSize).ToList();
                AddItemsBatch(result, batch);
            }
            catch (Exception ex)
            {
                LogManager.Error($"Error processing item IDs in batch starting at index {i}: {ex.Message}");
            }
        }
        return result;
    }


    public List<BaseItem> GetItems(IList<long> items)
    {
        List<string> stringIds = items.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
        return GetItems(stringIds);
    }


    public List<BaseItem> GetItemsInCollection(BoxSet collection)
    {
        List<BaseItem> items = collection.GetItems(new InternalItemsQuery(_adminUser)
        { Recursive = false }).Items.ToList();
        return items;
    }


    public int CollectionItemCount(BoxSet collection)
    {
        return collection.GetItems(new InternalItemsQuery(_adminUser)
        {
            Recursive = false,
            Limit = 0
        }).TotalRecordCount;
    }


    public List<BaseItem> GetItems(Guid collectionId)
    {
        BoxSet collection = LibraryManager.GetItemById(collectionId) as BoxSet;
        return GetItems(collection);
    }


    public List<BaseItem> GetItems(BoxSet collection)
    {
        return GetItemsInCollection(collection);
    }


    public bool SetSortName(BaseItem item, string sortName)
    {
        try
        {

            item.SortName = sortName;
            item.ForcedSortName = sortName;
            UpdateItem(item, ItemUpdateType.MetadataEdit);
            return true;
        }
        catch (Exception ex)
        {
            LogManager.Error($"Failed to set SortName for {item?.Name}: {ex.Message}");
            return false;
        }
    }


    internal Dictionary<string, string> AllCollectionNames()
    {
        Dictionary<string, string> allCollectionsNames = [];
        List<Guid> allCollections = AllCollections();

        foreach (Guid collection in allCollections)
        {
            BaseItem item = LibraryManager.GetItemById(collection);
            if (item != null)
            {
                allCollectionsNames[item.Id.ToString()] = item.Name;
            }
        }
        return allCollectionsNames;
    }

    internal Dictionary<string, string> GetImdbIds(List<string> itemIdsStrings)
    {
        const int batchSize = 500;
        Dictionary<string, string> imdbIds = [];

        for (int i = 0; i < itemIdsStrings.Count; i += batchSize)
        {
            List<string> batch = itemIdsStrings.Skip(i).Take(batchSize).ToList();
            Dictionary<string, string> batchImdbIds = GetImdbIdsBatch(batch);
            foreach (KeyValuePair<string, string> kvp in batchImdbIds)
            {
                imdbIds[kvp.Key] = kvp.Value;
            }
        }
        return imdbIds;
    }

    
    public CollectionOperationResult GetItemsIdsWithImdbIds(List<string> ImdbIds)
    {
        CollectionOperationResult result = new();

        if (ImdbIds == null || ImdbIds.Count == 0)
        {
            return result;
        }

        const int batchSize = 500;
        List<string> foundImdbIds = [];
        List<string> missingImdbIds = [.. ImdbIds];

        for (int i = 0; i < ImdbIds.Count; i += batchSize)
        {
            List<string> batch = [.. ImdbIds.Skip(i).Take(batchSize).Select(id => id.ToLowerInvariant())];
            List<BaseItem> items = GetItemsIdsWithImdbIdsBatch(batch);

            result.FoundItemIds.AddRange(items.Select(item => item.Id.ToString()));
            result.FoundImdbIds.AddRange(items.Select(item => item.ProviderIds["Imdb"]));
        }

        result.MissingImdbIds = missingImdbIds.Except(result.FoundImdbIds).ToList();
        return result;
    }


}
