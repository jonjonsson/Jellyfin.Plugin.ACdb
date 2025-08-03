using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Movies;
using ACdb.Model.JobResponse;
using ACdb.Model.Reporting;
using ACdb.Settings;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services.Collections;

internal class ProcessCollection
{
    private readonly ILibraryManager _libraryManager;
    private readonly Report _reporting;
    private readonly CollectionJobReport _collectionReport;
    private readonly IFileSystem _fileSystem;
    private readonly ACdbUtils _utils;
    public static event EventHandler<BaseItem> ACdbCollectionCreated;
    private static DateAddedSorting _dateAddedSorting;


    public ProcessCollection(ILibraryManager libraryManager, IFileSystem fileSystem, Report reporting, ACdbUtils utils)
    {
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _reporting = reporting;
        _collectionReport = new CollectionJobReport();
        _utils = utils;
        _dateAddedSorting = new DateAddedSorting(_utils, _libraryManager);
    }

    public CollectionJobReport GetCollectionReport()
    {
        return _collectionReport;
    }

    public async Task ProcessCollectionAsync(Response.Collection collection)
    {

        if (collection is null)
        {
            _reporting.AddToLog(LogTypeEnum.error, "Collection sync requested but no collection provided.", _collectionReport);
            return;
        }

        _collectionReport.name = collection.name;
        _collectionReport.cid = collection.cid;
        _collectionReport.collection_sid = collection.collection_sid;

        if (collection.delete)
        {
            if (string.IsNullOrEmpty(collection.cid))
            {
                LogManager.Error($"Deleting collection request for: {collection.collection_sid} {collection.name} but ID is not available.");
                return;
            }

            try
            {
                DeleteCollection(collection.cid);
            }
            catch (Exception ex)
            {
                _reporting.AddToLog(LogTypeEnum.error, $"Error deleting collection: {collection.name}", _collectionReport, new ActivityLogEventArgs { Description = ex.Message });
                LogManager.Error($"Error deleting collection: {collection.name}: ex.Message");
            }
            return;
        }

        Guid? collectionId = null;

        if (collection.imdb_ids == null)
        {
            LogManager.Error($"Error collection.imdb_ids is null in {collection.collection_sid}");
            return;
        }

        if (collection.imdb_ids.Count > 0)
        {
            _dateAddedSorting.ProcessExistingCollections(collection.collection_sid, collection.item_sorting);

            LogManager.Info($"IMDB IDs found, syncing collection: {collection.name}");
            try
            {
                collectionId = await CreateOrUpdateAsync(collection.cid, collection.name, collection.collection_sid, collection.imdb_ids);
            }
            catch (Exception ex)
            {
                _reporting.AddToLog(LogTypeEnum.error, $"Error creating or updating collection: {collection.name}", _collectionReport, new ActivityLogEventArgs { Description = ex.Message });
                LogManager.Error($"Error creating or updating collection: {collection.name}: {ex.Message}");
            }
        }

        if (!collectionId.HasValue)
        {
            SettingsManager.CollectionRemovedCleanup(collection.collection_sid);
            return;
        }

        BoxSet collectionItem = _libraryManager.GetItemById(collectionId.Value.ToString()) as BoxSet;

        try
        {
            UpdateNameDescription(collectionItem, collection.name, collection.description);
        }
        catch (Exception ex)
        {
            _reporting.AddToLog(LogTypeEnum.error, $"Error updating name or description for collection: {collection.name}", _collectionReport, new ActivityLogEventArgs { Description = ex.Message });
            LogManager.Error($"Error updating name or description for collection: {collection.name}: {ex.Message}");
        }

        try
        {
            bool updatedCollectionSortName = await UpdateCollectionSortNameAsync(collectionItem, collection.sort_name, collection.sort_to_top);
            if (updatedCollectionSortName)
            {
                LogManager.Info($"Updated sort name for collection: {collection.name}");
            }
        }
        catch (Exception ex)
        {
            _reporting.AddToLog(LogTypeEnum.error, $"Error updating sort name for collection: {collection.name}", _collectionReport, new ActivityLogEventArgs { Description = ex.Message });
            LogManager.Error($"Error updating sort name for collection: {collection.name}: {ex.Message}");
        }

        HandleItemDisplayOrder(collectionItem, collection.item_sorting);
        await HandlePoster(collectionItem, collection);
    }


    private void HandleItemDisplayOrder(BoxSet collection, ItemSorting? itemSorting)
    {
        if (itemSorting == null || itemSorting == ItemSorting.None)
        {
            return;
        }

        ItemSortBy displayOrder;
        if (itemSorting == ItemSorting.PremierDate)
        {
            displayOrder = ItemSortBy.PremiereDate;
        }
        else
        {
            displayOrder = ItemSortBy.SortName;
        }
        collection.DisplayOrder = displayOrder.ToString();
        _utils.UpdateItem(collection, ItemUpdateType.MetadataEdit);
    }

    private bool DeleteCollection(string collectionID)
    {
        BaseItem collectionItem = _libraryManager.GetItemById(collectionID);
        _collectionReport.deleted = true;

        if (collectionItem == null || collectionItem.GetType() != typeof(BoxSet))
        {
            string errorMessage = collectionItem == null
                ? "Collection sync requested to delete a collection that does not exist."
                : "Collection sync requested to delete a collection but collection ID provided is not a collection, can not process collection.";
            _reporting.AddToLog(LogTypeEnum.error, errorMessage, _collectionReport);
            return false;
        }

        try
        {
            LogManager.LogEvent(LogTypeEnum.info, $"Deleting collection: {collectionItem.Name}");

            var deleteOptions = new DeleteOptions
            {
            };
            _libraryManager.DeleteItem(collectionItem, deleteOptions);

            if (_libraryManager.GetItemById(collectionID.ToString()) != null)
            {
                _reporting.AddToLog(LogTypeEnum.error, "Collection sync requested to delete a collection but it was unsuccessful.", _collectionReport);
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            _reporting.AddToLog(LogTypeEnum.error, $"Error deleting collection", _collectionReport, new ActivityLogEventArgs { Description = e.Message });
            return false;
        }
    }


    private async Task<Guid?> CreateOrUpdateAsync(string collectionID, string name, string collection_sid, List<string> imdbIDs)
    {
        if (string.IsNullOrEmpty(collectionID) && string.IsNullOrEmpty(name))
        {
            _reporting.AddToLog(LogTypeEnum.error, "Collection sync requested but no collection ID or name provided, can not process collection.", _collectionReport);
            _collectionReport.paused = true;
            return null;
        }

        imdbIDs = imdbIDs.Distinct().ToList();

        collectionID = ValidateExistingCollection(collectionID, name);

        if (string.IsNullOrEmpty(collectionID))
        {
            Dictionary<string, string> allCollectionNames = _utils.AllCollectionNames();

            if (!allCollectionNames.ContainsValue(name))
            {
                return await CreateCollectionFromImdbsAsync(name, imdbIDs, collection_sid);
            }

            _reporting.AddToLog(LogTypeEnum.info, $"Collection {name} already exists. Will merge.", _collectionReport, new ActivityLogEventArgs { Description = "Job asked to create collection but a collection with the same name already exists." });
            collectionID = allCollectionNames.FirstOrDefault(x => x.Value == name).Key;
        }

        if (string.IsNullOrEmpty(collectionID))
        {
            _reporting.AddToLog(LogTypeEnum.error, "Collection ID is null after attempting to find or create collection.", _collectionReport);
            return null;
        }

        BaseItem collectionItem = _libraryManager.GetItemById(collectionID);
        if (collectionItem is BoxSet collection)
        {
            SettingsManager.AddCollectionSidToGuid(collection_sid, collection.Id);
            return await UpdateCollectionAsync(collection, imdbIDs);
        } 
        
        _reporting.AddToLog(LogTypeEnum.error, $"Collection ID {collectionID} is not a BoxSet, cannot process collection.", _collectionReport);
        return null;
    }

    private static int RemoveRedunantImdbIdsFromCollection(BaseItem collection, Dictionary<string, string> ImdbIdsInCollection, List<string> listImdbIds)
    {
        int removed = 0;
        List<string> itemsToRemoveFromCollection = [];
        foreach (KeyValuePair<string, string> itemAndImdb in ImdbIdsInCollection)
        {
            if (listImdbIds.Contains(itemAndImdb.Value) is false)
            {
                itemsToRemoveFromCollection.Add(itemAndImdb.Key);
                removed++;
            }
        }
        CollectionManager.RemoveFromCollection(collection, itemsToRemoveFromCollection);
        return removed;
    }

    private async Task<CollectionOperationResult> AddImdbIdsToCollectionAsync(Guid collectionGuid, List<string> ImdbIds)
    {
        CollectionOperationResult result = new();
        if (ImdbIds is null || ImdbIds.Count == 0)
            return result;
        result = _utils.GetItemsIdsWithImdbIds(ImdbIds);
        BaseItem collection = _libraryManager.GetItemById(collectionGuid);
        await CollectionManager.AddToCollectionAsync(collection, result.FoundItemIds);
        return result;
    }


    private async Task<Guid?> CreateCollectionFromImdbsAsync(string name, List<string> listImdbIds, string collection_sid)
    {
        if (string.IsNullOrEmpty(name))
        {
            _reporting.AddToLog(LogTypeEnum.error, "No collection name provided, can't create collection without name", _collectionReport);
            return null;
        }

        try
        {
            CollectionOperationResult getItemsWithImdbResult = _utils.GetItemsIdsWithImdbIds(listImdbIds);

            if (getItemsWithImdbResult.FoundCount == 0)
            {
                _reporting.AddToLog(LogTypeEnum.error, "Can't create collection as you do not own any of the items.", _collectionReport);
                return null;
            }

            LogManager.LogEvent(LogTypeEnum.info, $"Creating {name}", new ActivityLogEventArgs { Description = $"Collections contains {listImdbIds.Count} items." });
            BaseItem newCollection = await CollectionManager.CreateCollectionAsync(name, getItemsWithImdbResult.FoundItemIds);
            SettingsManager.AddCollectionSidToGuid(collection_sid, newCollection.Id);
            ACdbCollectionCreated?.Invoke(this, newCollection);

            string url = $"{PluginConfig.CollectionIdUrl}{_collectionReport.collection_sid}";
            LogManager.LogEvent(LogTypeEnum.info, $"Created collection: {name}", new ActivityLogEventArgs { Description = $"Added: {getItemsWithImdbResult.FoundCount}. Missing: {getItemsWithImdbResult.MissingImdbIds.Count}. Click to visit on {PluginConfig.WebSiteUrl}", HyperLink = url });

            _collectionReport.is_new = true;
            _collectionReport.added_count = getItemsWithImdbResult.FoundCount;
            _collectionReport.cid = newCollection.Id.ToString();
            _collectionReport.missing_imdbs = getItemsWithImdbResult.MissingImdbIds;
            return newCollection.Id;
        }
        catch (Exception e)
        {
            _reporting.AddToLog(LogTypeEnum.error, $"Error creating collection", _collectionReport, new ActivityLogEventArgs { Description = e.Message });
            return null;
        }
    }

    private async Task<Guid?> UpdateCollectionAsync(BoxSet collection, List<string> listImdbIds)
    {
        try
        {
            List<string> itemsIdsInCollection = _utils.GetItemIdsInCollection(collection);
            Dictionary<string, string> ImdbIdsInCollection = _utils.GetImdbIds(itemsIdsInCollection);
            List<string> ImdbIdsInCollectionList = ImdbIdsInCollection.Values.ToList();
            List<string> ImdbIdsToAdd = listImdbIds.Except(ImdbIdsInCollectionList).ToList();

            int removedCount = RemoveRedunantImdbIdsFromCollection(collection, ImdbIdsInCollection, listImdbIds);

            CollectionOperationResult addImdbIdsResult = await AddImdbIdsToCollectionAsync(collection.Id, ImdbIdsToAdd);
            _collectionReport.cid = collection.Id.ToString();
            _collectionReport.is_new = false;
            _collectionReport.added_count = addImdbIdsResult.FoundImdbIds.Count;
            _collectionReport.removed_count = removedCount;
            _collectionReport.missing_imdbs = addImdbIdsResult.MissingImdbIds;
            LogManager.LogEvent(LogTypeEnum.info, $"Synced: {collection.Name}", new ActivityLogEventArgs { Description = $"Added: {addImdbIdsResult.FoundImdbIds.Count}. Removed: {removedCount}. Missing: {addImdbIdsResult.MissingImdbIds.Count}. Click to view", HyperLink = $"{PluginConfig.CollectionIdUrl}{_collectionReport.collection_sid}" });
            return collection.Id;
        }
        catch (Exception e)
        {
            _reporting.AddToLog(LogTypeEnum.error, $"Error updating collection", _collectionReport, new ActivityLogEventArgs { Description = e.Message });
            return collection.Id;
        }
        
    }

    private bool UpdateNameDescription(BoxSet collectionItem, string name, string description)
    {
        if (collectionItem.Overview == description && collectionItem.Name == name)
        {
            return true;
        }

        collectionItem.Overview = description;
        collectionItem.Name = name;

        if (collectionItem.IsLocked)
        {
            LogManager.LogEvent(LogTypeEnum.error, $"{collectionItem.Name} is locked, can't update name and/or description for {name}", new ActivityLogEventArgs { });
            return false;
        }

        try
        {
            _utils.UpdateItem(collectionItem, ItemUpdateType.None);
            LogManager.LogEvent(LogTypeEnum.info, $"Updated name and/or description for collection: {name}", new ActivityLogEventArgs { Description = description });
            return true;
        }
        catch (Exception e)
        {
            LogManager.LogEvent(LogTypeEnum.error, $"Error updating name and/or description for collection: {collectionItem.Name}", new ActivityLogEventArgs { Description = e.Message });
            return false;
        }
    }

    private async Task<bool> UpdateCollectionSortNameAsync(BoxSet collectionItem, string sortName, bool? sort_to_top)
    {
        string existingSortName = collectionItem.SortName;
        string newSortName = null;

        if (sort_to_top == null && sortName == null)
        {
            return true;
        }

        if (sort_to_top == true)
        {
            if (_collectionReport.added_count == 0)
            {
                return true;
            }
            newSortName = SortingUtils.GetSortToTopSortName(collectionItem.Name);
            LogManager.LogEvent(LogTypeEnum.info, $"Moving {collectionItem.Name} to the top of collections", new ActivityLogEventArgs { Description = $"Click to configure.", HyperLink = $"{PluginConfig.CollectionIdUrl}{_collectionReport.collection_sid}" });
        }
        else if (sort_to_top == false) // Sort name was reset
        {
            newSortName = SortingUtils.GetDefaultSortName(collectionItem.Name);
        }
        else if (sortName != null)
        {
            newSortName = sortName;
        }

        if (newSortName == null || existingSortName == newSortName)
        {
            return true;
        }

        await Task.Delay(2000);
        return _utils.SetSortName(collectionItem, newSortName);
    }


    public async Task HandlePoster(BaseItem collectionItem, Response.Collection collectionResponse)
    {
        try
        {
            if (collectionResponse.no_poster == true)
            {
                SettingsManager.RemoveCollectionWithPoster(collectionItem.Id);
                if (collectionResponse.set_poster == true)
                {
                    RemoveBoxSetPoster(collectionItem);
                    await RefreshImageMetadata(collectionItem);
                }
            }
            else if (collectionResponse.poster_id != null)
            {
                SettingsManager.AddCollectionWithPoster(collectionItem.Id);
                if (collectionResponse.set_poster == true)
                {
                    LogManager.LogEvent(LogTypeEnum.info, $"Setting poster for collection: {collectionItem.Name}");
                    await Task.Delay(2000); // Wait for a second to ensure the collection is ready
                    await RefreshImageMetadata(collectionItem);
                }
            }
        }
        catch (Exception e)
        {
            LogManager.LogEvent(LogTypeEnum.error, $"Error handling poster for collection: {collectionItem.Name}", new ActivityLogEventArgs { Description = e.Message });
        }
    }


    private async Task RefreshImageMetadata(BaseItem collectionItem)
    {
        MetadataRefreshOptions options = new(_utils.MetadataRefreshOptionsParam)
        {
            ImageRefreshMode = MetadataRefreshMode.FullRefresh,
            MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
            ReplaceAllImages = true,
        };

        try
        {
            LogManager.LogEvent(LogTypeEnum.info, $"Refreshing metadata images for collection: {collectionItem.Name}");
            await collectionItem.RefreshMetadata(options, new CancellationToken());
        }
        catch
        {
            LogManager.LogEvent(LogTypeEnum.error, $"Error refreshing metadata for collection: {collectionItem.Name}", new ActivityLogEventArgs { Description = "Refresh failed" });
        }
    }


    public bool RemoveBoxSetPoster(BaseItem collectionItem)
    {
        if (collectionItem == null || CollectionManager.IsCollection(collectionItem) is false)
        {
            LogManager.Error($"Collection sync requested to remove poster from a collection that does not exist or is not a collection.");
            return false;
        }

        try
        {
            ItemImageInfo imageInfo = _utils.GetImageInfo(collectionItem, ImageType.Primary, 0);
            if (imageInfo == null)
            {
                LogManager.Error($"Collection sync requested to remove poster from a collection that does not have a poster.");
                return false;
            }

            collectionItem.RemoveImage(imageInfo);
            _utils.UpdateItem(collectionItem, ItemUpdateType.MetadataEdit); // Not sure if required, just in case
            LogManager.LogEvent(LogTypeEnum.info, $"Removed poster from collection: {collectionItem.Name}", new ActivityLogEventArgs { Description = imageInfo.Path });
            return true;
        }
        catch (Exception e)
        {
            LogManager.LogEvent(LogTypeEnum.error, $"Error removing poster from collection: {collectionItem.Name}", new ActivityLogEventArgs { Description = e.Message });
            return false;
        }
    }

    private string ValidateExistingCollection(string collectionID, string name)
    {
        if (string.IsNullOrEmpty(collectionID))
            return collectionID;

        BaseItem existingCollection = _libraryManager.GetItemById(collectionID);
        string url = $"{PluginConfig.CollectionIdUrl}{_collectionReport.collection_sid}";
        if (existingCollection == null)
        {
            LogManager.LogEvent(LogTypeEnum.warning, $"Collection {name} was not found. It will be recreated", new ActivityLogEventArgs { Description = $"Click {url} to pause or delete it.", HyperLink = url });
            return null;
        }

        if (existingCollection.GetType() != typeof(BoxSet))
        {
            LogManager.LogEvent(LogTypeEnum.warning, $"Collection ID for {name} was found but it's not a Collection. It will be recreated", new ActivityLogEventArgs { Description = $"Click {url} to pause or delete it.", HyperLink = url });
            return null;
        }
        return collectionID;
    }
}

