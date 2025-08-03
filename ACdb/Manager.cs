using ACdb.Model.JobResponse;
using ACdb.Model.Reporting;
using ACdb.Services;
using ACdb.Services.Authentication;
using ACdb.Services.Collections;
using ACdb.Services.Scheduling;
using ACdb.Settings;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb;

public static class Manager
{
    private static ILibraryManager _libraryManager;
    private static IFileSystem _fileSystem;
    private static SchedulingManager _schedulingManager;
    private static ITaskManager _taskManager;
    private static DateAddedSorting _dateAddedSorting;
    private static ACdbUtils _utils;
    private static Api _api;
    private static int ScheduleIntervalMinutes { get; set; } = PluginConfig.ScheduledTaskMinutesBetweenRuns;
    private static Report _jobReport;

    public static void Initialize(ILibraryManager libraryManager, IDirectoryService directoryService, ITaskManager taskManager, ICollectionManager collectionManager, IFileSystem fileSystem, IUserManager userManager)
    {
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _taskManager = taskManager;
        EventsManager.RegisterEventHandler(EventType.Progress, ActivityEventTriggered);
        _api = new Api();
        _utils = new ACdbUtils(_libraryManager, _fileSystem, directoryService, userManager);
        _dateAddedSorting = new DateAddedSorting(_utils, _libraryManager);
        _schedulingManager = new SchedulingManager(_taskManager);

        collectionManager.ItemsRemovedFromCollection += (sender, args) =>
        {
            _dateAddedSorting.ItemsRemovedFromCollectionEvent(args.Collection.Id, args.ItemsChanged);

            int itemCount = _utils.CollectionItemCount(args.Collection);
            if (args.Collection == null || itemCount == 0)
            {
                SettingsManager.CollectionRemovedCleanup(args.Collection.Id); // todo next this is not called on jellyfin delete on server, I could just do a settings clean up after every sync?
            }
        };


        collectionManager.ItemsAddedToCollection += (sender, args) =>
        {
            _dateAddedSorting.ItemsAddedToCollectionEvent(args.Collection.Id, args.ItemsChanged);
        };

        ProcessCollection.ACdbCollectionCreated += (sender, collection) =>
        {
            _dateAddedSorting.CreatedCollectionEvent(collection);
        };

        collectionManager.CollectionCreated += (sender, args) => // Not in use, using ACdbCollectionCreated event instead
        {
        };
    }

    private static string _apiKey;
    public static string ApiKey
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _apiKey = SettingsManager.GetApiKey();
            }
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _apiKey = null;
            }
            return _apiKey;
        }
        set
        {
            _apiKey = value;
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return;
            }
            if (SettingsManager.GetApiKey() != _apiKey)
            {
                SettingsManager.SetApiKey(_apiKey);
                ResetScheduleInterval();
            }
        }
    }

    public static string ApiKeyHashed => Sha256.Hash(ApiKey);

    public static void ResetScheduleInterval()
    {
        _schedulingManager.SetInterval(ScheduleIntervalMinutes);
    }

    public static void OverrideScheduleInterval(int minutes = 0)
    {
        ScheduleIntervalMinutes = minutes;
        ResetScheduleInterval();
    }

    public static async Task ExecuteJobTaskAsync()
    {
        await _schedulingManager.ExecuteJobTaskAsync();
    }

    public static string TimeSinceJobLastRan()
    {
        DateTime? lastRan = SettingsManager.GetLastSync();
        if (lastRan is null)
        {
            return null;
        }
        DateTime lastTime = lastRan ?? DateTime.MinValue;
        return $"{_schedulingManager.ConvertSecondsToHumanReadable((int)(DateTime.Now - lastTime).TotalSeconds)} ago";
    }

    public static string TimeUntilNextSync()
    {
        int? seconds = _schedulingManager.GetSecondsUntilNextRun();
        if (seconds is null)
        {
            return null;
        }
        if (seconds < 10)
        {
            return "Soon...";
        }
        return $"Approx. {_schedulingManager.ConvertSecondsToHumanReadable(seconds.Value)}";
    }

#pragma warning disable IDE0060 // Disable warning for unused parameter, Emby uses it
    public static async Task PullDataAsync(IProgress<double> progress, double currentProgress)
#pragma warning restore IDE0060
    {
        LogManager.LogEvent(LogTypeEnum.info, "Contacting ACdb.tv to sync collections");

        if (ApiKey is null)
        {
            LogManager.Error($"{PluginConfig.Name} Secret Key is missing. Are you logged in?", new ActivityLogEventArgs { Progress = 100 });
            return;
        }

        SettingsManager.AddSync(DateTime.Now);

        _jobReport = new Report();
        _jobReport.JobReport.plugin_version = PluginConfig.PluginVersion;
        _jobReport.JobReport.client_version = PluginConfig.ClientVersion.ToString();
        _jobReport.JobReport.api_key = ApiKey;
        _jobReport.JobReport.start_time = DateTime.UtcNow;

        string jobUrl = $"{PluginConfig.ApiGetJobsUrl}?v={PluginConfig.PluginVersion}&plugin-type=";
        string json = await _api.Get(ApiKey, jobUrl, CancellationToken.None);

        if (json is null || json == string.Empty)
        {
            _jobReport.AddToLog(LogTypeEnum.error, $"Got an invalid response from {PluginConfig.WebSiteUrl}.", new ActivityLogEventArgs { Progress = 100, Description = json });
            return;
        }

        Response response;
        try
        {
            response = JsonManager.DeserializeFromString<Response>(json);
        }
        catch (Exception e)
        {
            _jobReport.AddToLog(LogTypeEnum.error, $"Error processing incoming json from {PluginConfig.WebSiteUrl} ", new ActivityLogEventArgs { Progress = 100, Description = e.Message });

            if (await SendManagerReport(_jobReport) == false)
            {
                LogManager.Error($"Failed to send job results to {PluginConfig.WebSiteUrl}.", new ActivityLogEventArgs { Progress = 100 });
            }
            return;
        }

        int status = response.status;
        string message = response.message;

        if (status is 204) // No jobs to process
        {
            LogManager.LogEvent(LogTypeEnum.info, $"No new jobs found, you are up to date.", new ActivityLogEventArgs { Progress = 100 });
            return;
        }
        else if (status is 401)
        {
            LogManager.Error($"Not authorized to get jobs. Response: {message}", new ActivityLogEventArgs { Progress = 100 });
            return;
        }
        else if (status != 200)
        {
            LogManager.Error($"Error getting jobs. Message from {PluginConfig.WebSiteUrl}: {message}", new ActivityLogEventArgs { Progress = 100 });
            return;
        }

        currentProgress = 15;
        progress.Report(currentProgress);

        _jobReport.JobReport.job_id = response.job_id;
        _jobReport.JobReport.api_version = response.api_version;
        _jobReport.JobReport.client_min_version = response.min_client_version;

        ProcessCollections processCollections = new(_libraryManager, _fileSystem, _jobReport, _api, progress, currentProgress, _utils);
        await processCollections.ProcessCollectionsAsync(response.collections_sync);

        bool sentReport = await SendManagerReport(_jobReport);

        if (sentReport == false)
        {
            LogManager.Error($"Failed to send job report to {PluginConfig.WebSiteUrl}", new ActivityLogEventArgs { Progress = currentProgress });
        }

        LogManager.LogEvent(LogTypeEnum.info, "Sync Complete", new ActivityLogEventArgs { Progress = 100, Description = _jobReport.Summarize() });
    }

    private static async Task<bool> SendManagerReport(Report report)
    {
        _jobReport.JobReport.end_time = DateTime.UtcNow;
        try
        {
            string response = await _api.Post(ApiKey, report.JobReport, PluginConfig.PostJobResultsUrl, CancellationToken.None);
            return !string.IsNullOrEmpty(response);
        }
        catch (Exception ex)
        {
            LogManager.Error($"Failed to send manager report: {ex.Message}");
            return false;
        }
    }

    private static int _currentProgress = 100;
    public static int CurrentProgress
    {
        get { return _currentProgress; }
        set { _currentProgress = value; }
    }

    private static void ActivityEventTriggered(BaseEventArgs args)
    {
        double? progr = ((ActivityLogEventArgs)args).Progress;
        if (progr.HasValue)
        {
            CurrentProgress = (int)progr.Value;
        }
    }

    public static void Logout()
    {
        ApiKey = null;
        SettingsManager.ResetSettings();
        _dateAddedSorting.ResetAllItemsStartingWithDateAddedSortName();
        _schedulingManager.RemoveAllScheduledJobs();
    }

    public static void Uninstalling() // Emby uses this, no event for Jelly
    {
        Logout();
    }

    internal static async Task<(bool success, string message)> RegisterWithApiKey(string apiKey)
    {
        await Task.Delay(500);  // Tiny delay for show
        RegisterPlugin registerManager = new(_api);
        return await registerManager.RegisterAsync(apiKey);
    }

    internal static async Task<(bool success, string message)> CreateUser()
    {
        await Task.Delay(500);  // Tiny delay for show
        RegisterPlugin registerManager = new(_api);
        return await registerManager.RegisterAsync();
    }

    public static async Task<string> GetLoginTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            LogManager.Error("API key is missing. Cannot retrieve login token.");
            return null;
        }
        RegisterPlugin registerManager = new(_api);
        return await registerManager.GetLoginTokenAsync(ApiKey);
    }
}

