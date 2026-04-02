using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using ACdb.Model.JobResponse;
using ACdb.Settings;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using System;
using System.Collections.Generic;
using System.Linq;


namespace ACdb.Services.Collections
{
    public class DateAddedSorting
    {
        private readonly ILibraryManager _libraryManager;

        public DateAddedSorting(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }


        public void CreatedCollectionEvent(BoxSet collection)
        {
            if (SettingsManager.IsCollectionWithDateAddedSortName(collection.Id) == false)
            {
                return;
            }
            SetDateAddedSortnames(Manager.Utils.GetItems(collection));

        }

        public void ItemsAddedToCollectionEvent(Guid collectionId, IReadOnlyCollection<BaseItem> items)
        {
            if (SettingsManager.IsCollectionWithDateAddedSortName(collectionId) == false)
            {
                return;
            }
            SetDateAddedSortnames(items.ToList());
        }


        public void ItemsRemovedFromCollectionEvent(Guid collectionId, IReadOnlyCollection<BaseItem> items)
        {
            if (SettingsManager.IsCollectionWithDateAddedSortName(collectionId) == false)
            {
                return;
            }
            RemoveSortNameIfNotInOtherDateAddedCollections(collectionId, items.ToList());
        }


        private void RemoveSortNameIfNotInOtherDateAddedCollections(Guid collectionId, List<BaseItem> items)
        {
            foreach (var item in items)
            {
                if (SortingUtils.HasSortName(item.SortName) == false)
                {
                    continue;
                }

                try
                {
                    List<BoxSet> collectionsWithItem = Manager.Utils.GetItemsAllKnownCollections(item);
                    bool isInOtherDateAddedCollection = false;

                    foreach (var collectionInfo in collectionsWithItem)
                    {
                        BaseItem collection_item_is_in = _libraryManager.GetItemById(collectionInfo.Id);

                        if (collectionId == collection_item_is_in.Id)
                        {
                            continue;
                        }

                        if (SettingsManager.IsCollectionWithDateAddedSortName(collection_item_is_in.Id))
                        {
                            isInOtherDateAddedCollection = true;
                            break;
                        }
                    }

                    if (isInOtherDateAddedCollection == false)
                    {
                        Manager.Utils.SetSortName(item, SortingUtils.RemoveSortName(item.SortName));
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error($"Error while checking if item {item.Name} is in other date added collections: {ex.Message}");
                }
            }
        }

        public void ProcessExistingCollections(string collection_sid, ItemSorting? itemSorting)
        {
            bool isDateAddedSortName = SettingsManager.IsCollectionSidWithDateAddedSortName(collection_sid);

            Guid? collectionId = SettingsManager.GetCollectionGuidBySid(collection_sid);

            if (isDateAddedSortName == true && itemSorting != ItemSorting.DateAdded)
            {
                SettingsManager.RemoveCollectionWithDateAddedSortName(collection_sid);

                if (collectionId.HasValue == false)
                {
                    LogManager.Error($"Error, could not fine collection to reset item sort names for {collection_sid}.");
                    return;
                }

                List<BaseItem> items = Manager.Utils.GetItems(collectionId.Value);
                RemoveSortNameIfNotInOtherDateAddedCollections(collectionId.Value, items);
            }
            else if (isDateAddedSortName == false && itemSorting == ItemSorting.DateAdded)
            {
                SettingsManager.AddCollectionSidWithDateAddedSortName(collection_sid);

                if (collectionId.HasValue == false)
                {
                    return;
                }

                List<BaseItem> items = Manager.Utils.GetItems(collectionId.Value);
                SetDateAddedSortnames(items);
            }
        }

        private void SetDateAddedSortnames(List<BaseItem> items)
        {
            foreach (var item in items)
            {
                if (SortingUtils.HasSortName(item.SortName))
                {
                    continue;
                }
                Manager.Utils.SetSortName(item, SortingUtils.GetDateUntilSortName(item.SortName, item.DateCreated));
            }
        }

        public List<BaseItem> GetItemsStartingWithSortName()
        {
            string prefix = SortingUtils.SortNameStart;
            int batchSize = 1000;
            int startIndex = 0;
            List<BaseItem> allMatchingItems = new List<BaseItem>();

            while (true)
            {
                var query = new InternalItemsQuery
                {
                    IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
                    Recursive = true,
                    Limit = batchSize,
                    StartIndex = startIndex,
                    OrderBy = [(ItemSortBy.SortName, SortOrder.Ascending)]
                };
                List<BaseItem> items = _libraryManager.QueryItems(query).Items.ToList();

                if (items.Count == 0)
                {
                    break;
                }

                var matchingItems = items
                    .Where(item => item.SortName != null && item.SortName.StartsWith(prefix, StringComparison.Ordinal))
                    .ToList(); // TODO Very slow on Jellyfin. Not sure if there is anything that can be done.

                if (matchingItems.Count == 0)
                {
                    break;
                }

                allMatchingItems.AddRange(matchingItems);
                startIndex += batchSize;
            }

            return allMatchingItems;
        }


        public void ResetAllItemsStartingWithDateAddedSortName()
        {
            List<BaseItem> matchingItems = GetItemsStartingWithSortName();
            foreach (var item in matchingItems)
            {
                if (SortingUtils.HasSortName(item.SortName) == false)
                {
                    continue;
                }
                string newSortName = SortingUtils.RemoveSortName(item.SortName);
                Manager.Utils.SetSortName(item, newSortName);
            }
        }

    }
}
