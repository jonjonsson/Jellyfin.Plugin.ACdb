using MediaBrowser.Controller.Entities.Movies;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ACdb.Model.JobResponse;
using ACdb.Model;


namespace ACdb.Services
{

    public partial class ACdbUtils
    {
        public readonly ILibraryManager LibraryManager;
        public readonly IDirectoryService DirectoryService;
        public readonly IFileSystem FileSystem;
        public readonly Api ApiCon;
        private readonly User _adminUser;

        public ACdbUtils(ILibraryManager libraryManager, IFileSystem fileSystem, IDirectoryService directoryService, IUserManager userManager, Api apiCon)
        {
            LibraryManager = libraryManager;
            DirectoryService = directoryService;
            FileSystem = fileSystem;
            ApiCon = apiCon;

            MetadataRefreshOptionsParam = directoryService;

            IEnumerable<User> users = userManager.GetUsers();
            _adminUser = users.FirstOrDefault(u => u.HasPermission(PermissionKind.IsAdministrator));
        }

        public User GetAdminUser()
        {
            if (_adminUser == null)
            {
                LogManager.Error("Could not find Admin user.");
            }
            return _adminUser;
        }

        public BaseItem GetItem(Guid itemId)
        {
            return LibraryManager.GetItemById(itemId);
        }


        public List<BaseItem> GetItems(List<string> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0)
            {
                return new List<BaseItem>();
            }

            const int batchSize = 500;
            List<BaseItem> result = new List<BaseItem>();

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


        public List<BaseItem> GetItemsInCollection(BoxSet collection)
        {
            List<BaseItem> items = collection.GetItems(new InternalItemsQuery(_adminUser)
            { Recursive = false }).Items.ToList();
            return items;
        }

        public List<string> GetImdbIds(List<BaseItem> items)
        {
            List<string> imdbIds = items
                .Where(item => item.ProviderIds != null && item.ProviderIds.TryGetValue("Imdb", out var _))
                .Select(item => item.ProviderIds["Imdb"])
                .ToList();
            return imdbIds;
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
            Dictionary<string, string> allCollectionsNames = new Dictionary<string, string>();
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


        public CollectionOperationResult GetItemsIdsWithImdbIds(List<string> ImdbIds, Guid[] allowedTopParentIds = null)
        {
            CollectionOperationResult result = new CollectionOperationResult();

            if (ImdbIds == null || ImdbIds.Count == 0)
            {
                return result;
            }

            Guid[] topParentIdsArray = allowedTopParentIds ?? Array.Empty<Guid>();

            const int batchSize = 500;
            List<string> allItemIds = new List<string>();

            for (int i = 0; i < ImdbIds.Count; i += batchSize)
            {
                List<string> batch = ImdbIds.Skip(i).Take(batchSize).Select(id => id.ToLowerInvariant()).ToList();
                List<BaseItem> items = GetItemsIdsWithImdbIdsBatch(batch, topParentIdsArray);

                foreach (BaseItem item in items)
                {
                    if (item.ProviderIds != null && item.ProviderIds.TryGetValue("Imdb", out var imdbId))
                    {
                        allItemIds.Add(item.Id.ToString());
                        result.FoundImdbIds.Add(imdbId);
                        result.FoundItems.Add(item);
                    }
                }
            }

            result.FoundItemIds = allItemIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
            result.MissingImdbIds = new List<string>(ImdbIds).Except(result.FoundImdbIds).ToList();
            return result;
        }

        public List<(string Name, string Id)> GetAllLibraries()
        {
            var libraries = new List<(string Name, string Id)>();

            if (LibraryManager == null)
            {
                return libraries;
            }

            IReadOnlyList<BaseItem> results = LibraryManager.RootFolder.GetChildren(Manager.Utils.GetAdminUser(), true);

            if (results == null)
            {
                return libraries;
            }

            foreach (BaseItem item in results)
            {
                if (item == null) continue;
                libraries.Add((item.Name, item.Id.ToString()));
            }

            return libraries;
        }

        public Guid[] GetAllLibrariesExcluding(IEnumerable<string> namesToExclude)
        {
            List<(string Name, string Id)> all = GetAllLibraries();
            if (all == null || all.Count == 0)
            {
                return Array.Empty<Guid>();
            }

            foreach (string name in namesToExclude)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (all.Any(x => string.Equals(x.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    all = all.Where(x => !string.Equals(x.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            return all.Select(x => Guid.Parse(x.Id)).ToArray();
        }


        public string ConvertSecondsToHumanReadable(int sec)
        {
            int hours = sec / 3600;
            sec %= 3600;
            int minutes = sec / 60;
            sec %= 60;

            string hourString = hours == 1 ? "hour" : "hours";
            string minuteString = minutes == 1 ? "minute" : "minutes";
            string secondString = sec == 1 ? "second" : "seconds";

            if (hours > 0)
            {
                return $"{hours} {hourString}, {minutes} {minuteString} and {sec} {secondString}";
            }
            else if (minutes > 0)
            {
                return $"{minutes} {minuteString} and {sec} {secondString}";
            }
            else
            {
                return $"{sec} {secondString}";
            }
        }

        public string HumanReadablerDuration(DateTime startTime, DateTime endTime)
        {
            TimeSpan duration = endTime - startTime;
            return ConvertSecondsToHumanReadable((int)duration.TotalSeconds);
        }

        public List<string> GetItemIds(List<BaseItem> items)
        {
            return items.Select(item => item.Id.ToString()).ToList();
        }


        public List<string> GetItemIdsExcept(List<BaseItem> items, List<string> exceptItems)
        {
            return items.Select(item => item.Id.ToString()).Except(exceptItems).ToList();
        }


        public List<BaseItem> ApplyLimiting(List<BaseItem> items, int limit, LimitType? limitType)
        {
            if (limit <= 0 || items.Count <= limit)
            {
                return items;
            }
            if (limitType == LimitType.Random)
            {
                Random rnd = new Random();
                items = items.OrderBy(x => rnd.Next()).Take(limit).ToList();
            }
            else
            {
                items = items.OrderByDescending(x => x.DateCreated).Take(limit).ToList();
            }
            return items;
        }


    }
}
