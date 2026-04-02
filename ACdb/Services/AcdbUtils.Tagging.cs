using Jellyfin.Data;
using Jellyfin.Data.Enums;
using ACdb.Model.Reporting;
using ACdb.Settings;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACdb.Services
{
    public partial class ACdbUtils
    {


        public (int addedCount, int removedCount) UpdateTags(List<string> itemIDsToAdd, string itemTag)
        {
            int addedCount = 0;
            int removedCount = 0;
            if (!string.IsNullOrWhiteSpace(itemTag))
            {
                List<string> itemsAlreadyWithTag = Manager.Utils.GetItemsWithTag(itemTag);
                List<string> itemIDsToAddThatDontHaveTag = itemIDsToAdd.Except(itemsAlreadyWithTag).ToList();
                List<string> itemIDsToRemoveTagFrom = itemsAlreadyWithTag.Except(itemIDsToAdd).ToList();
                removedCount = Manager.Utils.RemoveTagFromItems(itemIDsToRemoveTagFrom, itemTag);
                addedCount = Manager.Utils.AddTagToItems(itemIDsToAddThatDontHaveTag, itemTag);
            }
            return (addedCount, removedCount);
        }

        public (bool, bool) AddTag(BaseItem item, string tag)
        {
            if (item == null || string.IsNullOrWhiteSpace(tag))
            {
                return (false, false);
            }
            if (item.Tags != null && item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                return (true, false); // Tag already exists
            }

            item.AddTag(tag);
            UpdateItem(item, ItemUpdateType.MetadataEdit); // Must be saved
            return (true, true);
        }



        public int RemoveTagFromAllItems(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return 0;
            }

            var itemIds = GetItemsWithTag(tag);
            return RemoveTagFromItems(itemIds, tag);
        }


        public int RemoveTagFromCollection(string collectionSid, string tag)
        {
            if (string.IsNullOrWhiteSpace(collectionSid) || string.IsNullOrWhiteSpace(tag))
            {
                return 0;
            }
            Guid? collectionId = SettingsManager.GetCollectionGuidBySid(collectionSid);

            if (!collectionId.HasValue)
            {
                LogManager.LogEvent(LogTypeEnum.info, $"Could not remove tag {tag}, no collection found.");
                return 0;
            }

            BoxSet collection = GetItem(collectionId.Value) as BoxSet;
            
            if (collection == null)
            {
                LogManager.LogEvent(LogTypeEnum.info, $"Could not remove tag {tag}, no collection found.");
                return 0;
            }

            int removedCount = RemoveTagFromCollections(collection, tag);
            return removedCount;
        }


        private List<string> GetItemsWithTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return new List<string>();
            }

            InternalItemsQuery query = new InternalItemsQuery
            {
                Tags = new[] { tag },
                Recursive = true,
                IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            };

            var result = Enumerable.ToArray(LibraryManager.GetItemIds(query));
            return result.Select(id => id.ToString()).ToList();
        }

        private int RemoveTagFromItems(List<string> itemIds, string tag)
        {
            if (itemIds == null || itemIds.Count == 0 || string.IsNullOrWhiteSpace(tag))
            {
                return 0;
            }

            int count = 0;
            List<BaseItem> items = GetItems(itemIds);

            foreach (var item in items)
            {
                if (item.Tags == null || item.Tags.Length == 0)
                {
                    continue;
                }

                if (item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    item.Tags = item.Tags.Where(t => !string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)).ToArray();
                    UpdateItem(item, ItemUpdateType.MetadataEdit);
                    count++;
                }
            }
            return count;
        }

        private int RemoveTagFromCollections(BoxSet collection, string tag)
        {
            if (collection.Tags == null || collection.Tags.Length == 0)
            {
                return 0;
            }
            if (collection.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                collection.Tags = collection.Tags.Where(t => !string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)).ToArray();
                UpdateItem(collection, ItemUpdateType.MetadataEdit); 
                return 1;
            }
            return 0;
        }



        private int AddTagToItems(List<string> itemIDs, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return 0;
            }

            var items = GetItems(itemIDs);
            if (items == null || items.Count == 0)
            {
                return 0;
            }

            int count = 0;
            foreach (BaseItem item in items)
            {
                bool hasTag = item.Tags != null && item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
                if (!hasTag)
                {
                    item.AddTag(tag);
                    UpdateItem(item, ItemUpdateType.MetadataEdit);
                    count++;
                }
            }
            return count;
        }


    }
}
