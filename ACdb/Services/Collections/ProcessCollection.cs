using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Movies;
using ACdb.Model.JobResponse;
using ACdb.Model.Reporting;
using ACdb.Settings;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ACdb.Model;

namespace ACdb.Services.Collections
{
    internal class ProcessCollection
    {
        private readonly ILibraryManager _libraryManager;
        private readonly CollectionJobReport _collectionReport;
        public static event EventHandler<BoxSet> ACdbCollectionCreated;
        private static DateAddedSorting _dateAddedSorting;
        private readonly Response.CollectionJob _collectionRules;
        private CollectionOperationResult _collectionOperationResult;


        public ProcessCollection(ILibraryManager libraryManager, Response.CollectionJob collectionRules)
        {
            _libraryManager = libraryManager;
            _collectionReport = new CollectionJobReport();
            _collectionRules = collectionRules;
            _dateAddedSorting = new DateAddedSorting(_libraryManager);
        }

        public CollectionJobReport GetCollectionReport()
        {
            return _collectionReport;
        }

        public CollectionOperationResult GetCollectionOperationResult()
        {
            return _collectionOperationResult;
        }

        private Guid[] IncludeLibraries
        {
            get
            {
                if (ExcludedLibraries == null || ExcludedLibraries.Count == 0)
                {
                    return null; // Include all libraries
                }

                var libraries = Manager.Utils.GetAllLibrariesExcluding(ExcludedLibraries);
                return libraries.Length > 0 ? libraries : null;
            }
        }

        private List<string> ExcludedLibraries
        {
            get
            {
                if (_collectionRules.excluded_libraries == null)
                {
                    return new List<string>();
                }
                return _collectionRules.excluded_libraries;
            }
        }

        public async Task ProcessCollectionAsync()
        {

            if (_collectionRules is null)
            {
                return;
            }

            _collectionReport.name = _collectionRules.name;
            _collectionReport.cid = _collectionRules.cid;
            _collectionReport.collection_sid = _collectionRules.collection_sid;
            _collectionReport.start_time = DateTime.Now;

            if (_collectionRules.delete)
            {

                _collectionReport.deleted = true; // Set to true regardless of success so this does not gets sent endlessly to client

                if (string.IsNullOrEmpty(_collectionRules.cid))
                {
                    LogManager.Error($"Deleting collection request for: {_collectionRules.collection_sid} {_collectionRules.name} but ID is not available.");
                    return;
                }

                try
                {
                    DeleteCollection(_collectionRules.cid);
                }
                catch (Exception ex)
                {
                    LogManager.Error($"Error deleting collection: {_collectionRules.name}", new ActivityLogEventArgs { Description = ex.Message });
                }
                return;
            }

            BoxSet collection = null;

            if (_collectionRules.imdb_ids == null)
            {
                LogManager.Error($"Error collection.imdb_ids is null in {_collectionRules.collection_sid}");
                return;
            }

            if (_collectionRules.imdb_ids.Count > 0)
            {
                _dateAddedSorting.ProcessExistingCollections(_collectionRules.collection_sid, _collectionRules.item_sorting);

                LogManager.Info($"IMDB IDs found, syncing collection: {_collectionRules.name}");
                try
                {
                    collection = await CreateOrUpdateAsync(_collectionRules.cid, _collectionRules.name, _collectionRules.collection_sid, _collectionRules.imdb_ids);
                }
                catch (Exception ex)
                {
                    LogManager.Error($"Error creating or updating collection: {_collectionRules.name}", new ActivityLogEventArgs { Description = ex.Message });
                }
            }

            if (collection is null)
            {
                SettingsManager.CollectionRemovedCleanup(_collectionRules.collection_sid);
                return;
            }

            try
            {
                UpdateNameDescription(collection, _collectionRules.name, _collectionRules.description);
            }
            catch (Exception ex)
            {
                LogManager.Error($"Error updating name or description for collection: {_collectionRules.name}", new ActivityLogEventArgs { Description = ex.Message });
            }

            try
            {
                bool updatedCollectionSortName = await UpdateCollectionSortNameAsync(collection, _collectionRules.sort_name, _collectionRules.sort_to_top);
                if (updatedCollectionSortName)
                {
                    LogManager.Info($"Updated sort name for collection: {_collectionRules.name}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"Error updating sort name for collection: {_collectionRules.name}", new ActivityLogEventArgs { Description = ex.Message });
            }

            HandleItemDisplayOrder(collection, _collectionRules.item_sorting);

            if (_collectionRules.set_poster == true)
            {
                Manager.FetchImages.Add(collection);
            }
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
            Manager.Utils.UpdateItem(collection, ItemUpdateType.MetadataEdit);
        }

        private bool DeleteCollection(string collectionID)
        {
            BaseItem collectionItem = _libraryManager.GetItemById(collectionID);

            if (collectionItem == null || collectionItem.GetType() != typeof(BoxSet))
            {
                string errorMessage = collectionItem == null
                    ? "Collection sync requested to delete a collection that does not exist."
                    : "Collection sync requested to delete a collection but collection ID provided is not a collection, can not process collection.";
                LogManager.LogEvent(LogTypeEnum.error, errorMessage);
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
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                LogManager.LogEvent(LogTypeEnum.error, $"Error deleting collection", new ActivityLogEventArgs { Description = e.Message });
                return false;
            }
        }


        private async Task<BoxSet> CreateOrUpdateAsync(string collectionID, string name, string collection_sid, List<string> imdbIDs)
        {
            if (string.IsNullOrEmpty(collectionID) && string.IsNullOrEmpty(name))
            {
                LogManager.LogEvent(LogTypeEnum.error, $"Collection sync requested but no collection ID or name provided, can not process collection.", new ActivityLogEventArgs { Description = $"Collection SID: {collection_sid}" });
                _collectionReport.paused = true;
                return null;
            }

            imdbIDs = imdbIDs.Distinct().ToList();

            collectionID = ValidateExistingCollection(collectionID, name);

            if (string.IsNullOrEmpty(collectionID))
            {
                Dictionary<string, string> allCollectionNames = Manager.Utils.AllCollectionNames();

                if (!allCollectionNames.ContainsValue(name))
                {
                    BoxSet item = await UpdateCollectionAsync(imdbIDs);
                    if (item == null)
                    {
                        return null;
                    }
                    Manager.FetchImages.Add(item);
                    return item;
                }

                LogManager.LogEvent(LogTypeEnum.info, $"Collection {name} already exists. Will merge.");
                collectionID = allCollectionNames.FirstOrDefault(x => x.Value == name).Key;
            }

            if (string.IsNullOrEmpty(collectionID))
            {
                LogManager.LogEvent(LogTypeEnum.error, "Collection ID is null after attempting to find or create collection.");
                return null;
            }

            var collectionItem = _libraryManager.GetItemById(collectionID);
            if (collectionItem is BoxSet collection)
            {
                SettingsManager.AddCollectionSidToGuid(collection_sid, collection.Id);
                return await UpdateCollectionAsync(imdbIDs, collection);
            }

            LogManager.LogEvent(LogTypeEnum.error, $"Collection ID {collectionID} is not a BoxSet, cannot process collection.");
            return null;
        }


        private async Task<BoxSet> UpdateCollectionAsync(List<string> listImdbIds, BoxSet collection = null)
        {
            try
            {
                int removeCount = 0;
                _collectionOperationResult = Manager.Utils.GetItemsIdsWithImdbIds(listImdbIds, IncludeLibraries);
                _collectionReport.missing_imdbs = _collectionOperationResult.MissingImdbIds;
                List<BaseItem> itemsToAdd = Manager.Utils.ApplyLimiting(_collectionOperationResult.FoundItems, _collectionRules.limit, _collectionRules.limit_type);
                List<string> itemIDsToAdd = Manager.Utils.GetItemIds(itemsToAdd);

                if (collection is null)
                {
                    _collectionReport.is_new = true;
                    if (itemsToAdd.Count == 0)
                    {
                        LogManager.LogEvent(LogTypeEnum.warning, $"Can't create {_collectionReport.name} as you do not own any of the items.", new ActivityLogEventArgs { Description = $"Click to view.", HyperLink = $"{PluginConfig.CollectionIdUrl}{_collectionReport.collection_sid}" });
                        return null;
                    }
                    else
                    {
                        collection = await CollectionManager.CreateCollectionAsync(_collectionReport.name, itemIDsToAdd) as BoxSet;
                        SettingsManager.AddCollectionSidToGuid(_collectionReport.collection_sid, collection.Id);
                        ACdbCollectionCreated?.Invoke(this, collection);
                        _collectionReport.added_count = itemIDsToAdd.Count;
                        _collectionReport.cid = collection.Id.ToString();
                    }
                }
                else
                {
                    _collectionReport.is_new = false;
                    List<string> itemsInCollectionBefore = Manager.Utils.GetItemIdsInCollection(collection);
                    List<string> itemsToAddIds = Manager.Utils.GetItemIdsExcept(itemsToAdd, itemsInCollectionBefore);
                    List<string> itemsToRemove = itemsInCollectionBefore.Except(itemIDsToAdd).ToList();
                    removeCount = itemsToRemove.Count;
                    await CollectionManager.AddToCollectionAsync(collection, itemsToAddIds); // It's important to add items before removing items. Or else when adding random items, and not the same random items get added the collection cleanup event is triggered
                    CollectionManager.RemoveFromCollection(collection, itemsToRemove); // Must be after Adding Items.
                    _collectionReport.added_count = itemsToAddIds.Count;
                    _collectionReport.cid = collection.Id.ToString();
                }

                _collectionReport.removed_count = removeCount;
                LogManager.LogEvent(LogTypeEnum.info, $"Synced: {_collectionRules.name}", new ActivityLogEventArgs { Description = $"Added: {_collectionReport.added_count}. Removed: {removeCount}. Missing: {_collectionOperationResult.MissingImdbIds.Count}. Click to view", HyperLink = $"{PluginConfig.CollectionIdUrl}{_collectionReport.collection_sid}" });
            }
            catch (Exception e)
            {
                LogManager.LogEvent(LogTypeEnum.error, $"Error updating collection", new ActivityLogEventArgs { Description = e.Message });
                return collection;
            }
            return collection;
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
                Manager.Utils.UpdateItem(collectionItem, ItemUpdateType.None);
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
            return Manager.Utils.SetSortName(collectionItem, newSortName);
        }


        private string ValidateExistingCollection(string collectionID, string name)
        {
            if (string.IsNullOrEmpty(collectionID))
                return collectionID;

            BaseItem existingCollection = null;
            try
            {
                existingCollection = _libraryManager.GetItemById(collectionID);
            }
            catch (Exception)
            {
                LogManager.LogEvent(LogTypeEnum.warning, $"Collection ID not valid format, user may be moving from Emby to Jellyfin. Collection will be re-created.");
            }

            string url = $"{PluginConfig.CollectionIdUrl}{_collectionReport.collection_sid}";

            if (existingCollection == null)
            {
                LogManager.LogEvent(LogTypeEnum.info, $"Collection {name} was not found. It will be recreated.", new ActivityLogEventArgs { Description = $"Click {url} to pause or delete it.", HyperLink = url });
                return null;
            }

            if (existingCollection.GetType() != typeof(BoxSet))
            {
                LogManager.LogEvent(LogTypeEnum.warning, $"Collection ID for {name} was found but it's not a Collection. It will be recreated.", new ActivityLogEventArgs { Description = $"Click {url} to pause or delete it.", HyperLink = url });
                return null;
            }

            return collectionID;
        }
    }

}
