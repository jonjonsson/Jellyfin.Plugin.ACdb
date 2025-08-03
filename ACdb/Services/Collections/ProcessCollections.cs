using ACdb.Model.JobResponse;
using ACdb.Model.Reporting;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services.Collections;

public class ProcessCollections
{
    private readonly ILibraryManager _libraryManager;
    private readonly IFileSystem _fileSystem;
    private readonly Report _reporting;
    private readonly IProgress<double> _progress;
    private double _currentProgress;
    private readonly ACdbUtils _utils;
    private readonly Api _api;

    internal ProcessCollections(ILibraryManager libraryManager, IFileSystem fileSystem, Report reporting, Api api, IProgress<double> progress, double currentProgress, ACdbUtils utils)
    {
        _libraryManager = libraryManager;
        _reporting = reporting;
        _progress = progress;
        _currentProgress = currentProgress;
        _fileSystem = fileSystem;
        _api = api;
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
            ProcessCollection processCollection = new(_libraryManager, _fileSystem, _reporting, _utils);
            await processCollection.ProcessCollectionAsync(collection);
            CollectionJobReport collectionReport = processCollection.GetCollectionReport();
            _currentProgress += progressPerCollection;
            _progress.Report(_currentProgress);

            try
            {
                string response = await _api.Post(Manager.ApiKey, collectionReport, PluginConfig.PostJobResultsUrl, CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogManager.Error($"Failed to send collection report: {ex.Message}");
            }
        }

        LogManager.Info($"Finished processing {collectionsSync.collections.Count} collections");
    }


}

