using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ACdb.Services;

public partial class ACdbUtils
{

    public readonly IDirectoryService MetadataRefreshOptionsParam;


    public void AddItemsBatch(List<BaseItem> result, List<string> batch)
    {
        Guid[] batchIds = batch.Select(id => Guid.Parse(id)).ToArray(); // Convert List<string> to Guid[] for Jellyfin compatibility

        InternalItemsQuery query = new()
        {
            ItemIds = batchIds,
            Recursive = false
        };
        IReadOnlyList<BaseItem> items = LibraryManager.QueryItems(query).Items;
        result.AddRange(items);
    }


    public List<string> GetItemIdsInCollection(BoxSet collection)
    {
        List<string> ids = collection.GetItems(new InternalItemsQuery(_adminUser)
        {
            Recursive = false,
        }).Items.Select(item => item.Id.ToString()).ToList();
        return ids;
    }

    public List<Guid> AllCollections()
    {
        IReadOnlyList<Guid> collections = LibraryManager.GetItemIds(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.BoxSet],
        });
        return collections.ToList();
    }


    public List<BaseItem> GetItems(IList<Guid> items)
    {
        List<string> stringIds = items.Select(id => id.ToString()).ToList();
        return GetItems(stringIds);
    }

    public List<BaseItem> GetItemsIdsWithImdbIdsBatch(List<string> batch, Guid[] topParentIdsArray = null)
    {
        HashSet<string> imdbSet = new(batch, StringComparer.OrdinalIgnoreCase);

        InternalItemsQuery query = new()
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            Recursive = true,
            IsVirtualItem = false,
            HasAnyProviderId = new Dictionary<string, string> { { "Imdb", "" } }
        };

        if (topParentIdsArray != null && topParentIdsArray.Length > 0)
        {
        }

        IReadOnlyList<BaseItem> items = LibraryManager.QueryItems(query).Items;

        if (topParentIdsArray != null && topParentIdsArray.Length > 0)
        {
            var allowedParents = new HashSet<Guid>(topParentIdsArray);
            items = items.Where(i => i.ParentId != Guid.Empty && allowedParents.Contains(i.ParentId)).ToList();
        }

        return items
            .Where(item => item.ProviderIds != null
                && item.ProviderIds.TryGetValue("Imdb", out string imdbId)
                && imdbSet.Contains(imdbId))
            .ToList();
    }


    public void UpdateItem(BaseItem item, ItemUpdateType updateReason)
    {
        LibraryManager.UpdateItemAsync(item, item.GetParent(), updateReason, new CancellationToken());
    }


    public List<BoxSet> GetItemsAllKnownCollections(BaseItem item)
    {
        List<BoxSet> result = [];
        List<Guid> allCollectionIds = AllCollections();

        foreach (Guid boxSetId in allCollectionIds)
        {
            if (LibraryManager.GetItemById(boxSetId) is not BoxSet boxSet)
                continue;

            IReadOnlyList<BaseItem> boxSetItems = boxSet.GetItems(new InternalItemsQuery
            {
                Recursive = false
            }).Items;

            if (boxSetItems.Any(i => i.Id == item.Id))
            {
                result.Add(boxSet);
            }
        }
        return result;
    }


}
