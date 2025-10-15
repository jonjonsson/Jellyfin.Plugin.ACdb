using ACdb.Model.JobResponse;
using ACdb.Model.Reporting;
using MediaBrowser.Controller.Library;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services.Collections;

public class ProcessCollections
{
    private readonly ILibraryManager _libraryManager;
    private readonly Report _reporting;
    private readonly IProgress<double> _progress;
    private double _currentProgress;
    private readonly ACdbUtils _utils;

    internal ProcessCollections(ILibraryManager libraryManager, Report reporting, IProgress<double> progress, double currentProgress, ACdbUtils utils)
    {
        _libraryManager = libraryManager;
        _reporting = reporting;
        _progress = progress;
        _currentProgress = currentProgress;
        _utils = utils;
    }


    internal async Task ProcessCollectionsAsync(Response.CollectionsSync collectionsSync)
    {
        if (collectionsSync is null || collectionsSync.collections is null || collectionsSync.collections.Count == 0)
        {
            return;
        }

        double progressLeft = 100 - _currentProgress;
        double leaveProgress = 10;
        double progressPerCollection = (progressLeft - leaveProgress) / collectionsSync.collections.Count;

        foreach (Response.Collection collection in collectionsSync.collections)
        {
            ProcessCollection processCollection = new(_libraryManager, _reporting, _utils);
            await processCollection.ProcessCollectionAsync(collection);
            CollectionJobReport collectionReport = processCollection.GetCollectionReport();
            _currentProgress += progressPerCollection;
            _progress.Report(_currentProgress);

            try
            {
                if (collectionsSync.report_missing == false)
                {
                    collectionReport.missing_imdbs = null;
                }
                string response = await Manager.Utils.ApiCon.Post(Manager.ApiKey, collectionReport, PluginConfig.PostJobResultsUrl, CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogManager.Error($"Failed to send collection report: {ex.Message}");
            }
        }

        LogManager.Info($"Finished processing {collectionsSync.collections.Count} collections");
    }


}

