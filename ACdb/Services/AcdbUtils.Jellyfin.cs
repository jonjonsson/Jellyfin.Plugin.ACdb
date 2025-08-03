using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
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
        List<Guid> collections = LibraryManager.GetItemIds(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.BoxSet],
        });
        return collections;
    }

    public List<BaseItem> GetItemsIdsWithImdbIdsBatch(List<string> batch)
    {
        HashSet<string> imdbSet = new(batch, StringComparer.OrdinalIgnoreCase);

        InternalItemsQuery query = new()
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            Recursive = true,
            IsVirtualItem = false,
            HasAnyProviderId = new Dictionary<string, string> { { "Imdb", "" } }
        };

        IReadOnlyList<BaseItem> items = LibraryManager.QueryItems(query).Items;

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

    public ItemImageInfo GetImageInfo(BaseItem item, ImageType imageType, int index)
    {
        return item.GetImageInfo(imageType, index);
    }

    public Dictionary<string, string> GetImdbIdsBatch(List<string> batch)
    {
        Guid[] batchIds = batch.Select(id => Guid.Parse(id)).ToArray();

        InternalItemsQuery query = new()
        {
            ItemIds = batchIds,
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
        };

        IReadOnlyList<BaseItem> items = LibraryManager.QueryItems(query).Items;

        return items
            .Where(item => item.ProviderIds != null)
            .Select(item =>
            {
                item.ProviderIds.TryGetValue("imdb", out string imdbId);
                return new { item.Id, ImdbId = imdbId };
            })
            .Where(x => x.ImdbId != null)
            .ToDictionary(x => x.Id.ToString(), x => x.ImdbId);
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
