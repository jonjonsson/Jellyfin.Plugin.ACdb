using ACdb.Model.JobResponse;
using ACdb.Model.Reporting;
using ACdb.Settings;
using MediaBrowser.Controller.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static ACdb.Model.JobResponse.Response;

namespace ACdb.Services.Collections
{
    internal class ProcessTag
    {
        private readonly CollectionJobReport _collectionReport;
        private CollectionOperationResult _collectionOperationResult;
        private readonly TagJob _tagJob;

        public CollectionJobReport GetCollectionReport()
        {
            return _collectionReport;
        }

        public ProcessTag(TagJob tagJob, CollectionOperationResult collectionOperationResult = null, CollectionJobReport collectionReport = null)
        {
            _tagJob = tagJob;
            _collectionReport = collectionReport;

            if (_collectionReport is null)
            {
                _collectionReport = new CollectionJobReport();
                _collectionReport.start_time = DateTime.Now;
                _collectionReport.collection_sid = _tagJob.collection_sid;
            }

            _collectionOperationResult = collectionOperationResult;
        }

        public async Task ProcessTagAsync()
        {
            if (_tagJob.delete)
            {
                await DeleteTag();
                return;
            }

            switch (_tagJob.tag_type)
            {
                case TagTypeEnum.Collection:
                    AddCollectionTag();
                    break;
                case TagTypeEnum.MoviesAndSeries:
                    AddItemTag(); 
                    break;
                default:
                    LogManager.LogEvent(LogTypeEnum.warning, $"Tag type {_tagJob.tag_type} is not supported. You may need to update the plugin.");
                    return;
            }
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
                if (_tagJob.excluded_libraries == null)
                {
                    return new List<string>();
                }
                return _tagJob.excluded_libraries;
            }
        }


        private async Task DeleteTag()
        {
            int count;
            switch (_tagJob.tag_type)
            {
                case TagTypeEnum.Collection:
                    count = Manager.Utils.RemoveTagFromCollection(_tagJob.collection_sid, _tagJob.tag);
                    break;
                case TagTypeEnum.MoviesAndSeries:
                    count = Manager.Utils.RemoveTagFromAllItems(_tagJob.tag);
                    break;
                default:
                    LogManager.LogEvent(LogTypeEnum.warning, $"Tag type {_tagJob.tag_type} is not supported for deletion. You may need to update the plugin.");
                    return;
            }

            if (count > 0)
            {
                LogManager.LogEvent(LogTypeEnum.info, $"Removed tag {_tagJob.tag} from {count} items");
            }
            
            await Manager.Utils.ApiCon.Post(
                Manager.ApiKey,
                new TagDeletedReport
                {
                    tag = _tagJob.tag,
                    collection_sid = _tagJob.collection_sid,
                    type = (int)_tagJob.tag_type
                },
                PluginConfig.TagDeletedUrl,
                CancellationToken.None
            );
        }

        private void AddItemTag()
        {
            if (_collectionOperationResult == null)
            {
                if (_tagJob.imdb_ids == null || _tagJob.imdb_ids.Count == 0)
                {
                    LogManager.LogEvent(LogTypeEnum.warning, $"No IMDb IDs provided for tag {_tagJob.tag}. Skipping tag addition.");
                    return;
                }
                _collectionOperationResult = Manager.Utils.GetItemsIdsWithImdbIds(_tagJob.imdb_ids, IncludeLibraries);
                _collectionReport.missing_imdbs = _collectionOperationResult.MissingImdbIds;
            }

            List<BaseItem> itemsToAdd = Manager.Utils.ApplyLimiting(_collectionOperationResult.FoundItems, _tagJob.limit, _tagJob.limit_type);
            List<string> itemIDsToAdd = Manager.Utils.GetItemIds(itemsToAdd);
            (int addedTags, int removedTags) = Manager.Utils.UpdateTags(itemIDsToAdd, _tagJob.tag);

            if (addedTags > 0 || removedTags > 0)
            {
                LogManager.LogEvent(LogTypeEnum.info, $"Updated tag {_tagJob.tag}", new ActivityLogEventArgs { Description = $"Added tag to {addedTags} items, removed tag from {removedTags} items." });
                _collectionReport.added_count = addedTags;
                _collectionReport.removed_count = removedTags;
            }
        }

        private void AddCollectionTag()
        {
            if (string.IsNullOrWhiteSpace(_tagJob.collection_sid) || string.IsNullOrWhiteSpace(_tagJob.tag))
            {
                LogManager.LogEvent(LogTypeEnum.warning, $"Collection SID or tag is null or empty for collection tag job: {_tagJob.tag}. Skipping. ");
                return;
            }
            Guid? collectionId = SettingsManager.GetCollectionGuidBySid(_tagJob.collection_sid);

            if (collectionId == null) {
                LogManager.LogEvent(LogTypeEnum.warning, $"No collection found with SID {_tagJob.collection_sid} for tag {_tagJob.tag}. Skipping tag addition.");
                return;
            }

            BaseItem collection = Manager.Utils.GetItem(collectionId.Value);
            if (collection == null)
            {
                LogManager.LogEvent(LogTypeEnum.warning, $"No collection found with ID {collectionId.Value} for tag {_tagJob.tag}. Skipping tag addition.");
                return;
            }

            (bool added, bool isNew) = Manager.Utils.AddTag(collection, _tagJob.tag);
            if (added == false) LogManager.LogEvent(LogTypeEnum.error, $"Could not add tag '{_tagJob.tag}' to {collection.Name}");
            if (isNew) LogManager.LogEvent(LogTypeEnum.info, $"Added tag '{_tagJob.tag}' to {collection.Name}");
        }
    }


}
