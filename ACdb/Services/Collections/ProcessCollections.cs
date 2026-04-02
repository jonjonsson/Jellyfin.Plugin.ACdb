using ACdb.Model.JobResponse;
using ACdb.Model.Reporting;
using MediaBrowser.Controller.Library;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ACdb.Model.JobResponse.Response;


namespace ACdb.Services.Collections
{
    public class ProcessCollections
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IProgress<double> _progress;
        private double _currentProgress;
        private readonly CollectionsSync _collectionsSync;
        private readonly double _progressPerCollection; // how much % is used to process each collection, used to report progress after each collection is processed


        internal ProcessCollections(ILibraryManager libraryManager, CollectionsSync collectionsSync, IProgress<double> progress, double currentProgress)
        {
            _libraryManager = libraryManager;
            _collectionsSync = collectionsSync;
            _progress = progress;
            _currentProgress = currentProgress;

            double progressLeft = 100 - _currentProgress;
            double leaveProgress = 10;
            int collectionCount = _collectionsSync != null && _collectionsSync.collections != null ? _collectionsSync.collections.Count : 0;
            int tagCount = _collectionsSync != null && _collectionsSync.tags != null ? _collectionsSync.tags.Count : 0;
            _progressPerCollection = (progressLeft - leaveProgress) / (collectionCount + tagCount);
        }


        internal async Task ProcessJobsAsync()
        {
            DateTime startTime = DateTime.Now;
            await AddCollectionsJobAsync(); // Tag jobs that have an associated collection are handled in this function also to optimize the expensive query of getting all the items using imdb_ids
            await AddTagsJobAsync(); // Process remaining tags that were not associated with a collection or are delete jobs
            TimeSpan duration = DateTime.Now - startTime;
            Manager.LastRanDurationSec = (int?)duration.TotalSeconds;
        }


        private async Task AddCollectionsJobAsync()
        {
            if (_collectionsSync == null || _collectionsSync.collections == null || _collectionsSync.collections.Count == 0)
            {
                return;
            }

            foreach (CollectionJob collection in _collectionsSync.collections)
            {

                if (!string.IsNullOrEmpty(collection.name))
                {
                    string logMsg = $"Processing collection: {collection.name}";
                    if(collection.limit > 0)
                        logMsg += $". Limit: {collection.limit} items";
                    LogManager.LogEvent(LogTypeEnum.info, logMsg);
                }

                ProcessCollection processCollection = new ProcessCollection(_libraryManager, collection);
                await processCollection.ProcessCollectionAsync();
                CollectionJobReport collectionReport = processCollection.GetCollectionReport();
                
                try
                {
                    TagJob addTagJob = GetAndRemoveItemTaggingJob(collection.collection_sid);
                    if (addTagJob != null)
                    {
                        ProcessTag processTag = new ProcessTag(addTagJob, processCollection.GetCollectionOperationResult(), collectionReport);
                        await processTag.ProcessTagAsync();
                    }
                    await SendCollectionReportAsync(collectionReport);
                }
                catch (Exception ex)
                {
                    LogManager.Error($"Failed to send collection report: {ex.Message}");
                }

                IncrementProgress();
            }
        }


        private async Task AddTagsJobAsync()
        {

            if (_collectionsSync == null || _collectionsSync.tags == null || _collectionsSync.tags.Count == 0)
            {
                return;
            }

            foreach (TagJob tagJob in _collectionsSync.tags)
            {
                if (tagJob.tag is null || tagJob.tag.Trim() == "")
                {
                    continue;
                }

                if (tagJob.tag_type != TagTypeEnum.Collection)
                {
                    string logMsg = $"Processing item tags: {tagJob.tag}";
                    if (tagJob.limit > 0)
                        logMsg += $". Limit: {tagJob.limit} items";
                    LogManager.LogEvent(LogTypeEnum.info, logMsg);
                }
                
                ProcessTag processTag = new ProcessTag(tagJob);
                await processTag.ProcessTagAsync();
                IncrementProgress();
                await SendCollectionReportAsync(processTag.GetCollectionReport());
            }
        }

        private TagJob GetAndRemoveItemTaggingJob(string collectionSid)
        {
            if (_collectionsSync == null || _collectionsSync.tags == null || _collectionsSync.tags.Count == 0)
            {
                return null;
            }

            TagJob tagJob = _collectionsSync.tags.FirstOrDefault(t => t.collection_sid == collectionSid && t.delete == false && t.tag_type == TagTypeEnum.MoviesAndSeries);

            if (tagJob != null)
            {
                _collectionsSync.tags.Remove(tagJob);
                return tagJob;
            }
            return null;
        }


        private void IncrementProgress()
        {
            _currentProgress += _progressPerCollection;
            _progress.Report(_currentProgress);
        }

        private async Task SendCollectionReportAsync(CollectionJobReport collectionReport)
        {
            try
            {
                if (_collectionsSync.report_missing == false)
                {
                    collectionReport.missing_imdbs = null;
                }

                collectionReport.duration = (int)(DateTime.Now - collectionReport.start_time).TotalSeconds;
                string response = await Manager.Utils.ApiCon.Post(Manager.ApiKey, collectionReport, PluginConfig.PostJobResultsUrl, CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogManager.Error($"Failed to send collection report: {ex.Message}");
            }
        }


    }

}
