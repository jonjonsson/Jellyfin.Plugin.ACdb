using ACdb.Services;
using ACdb.Settings;
using ACdb.Settings.Model;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;


namespace ACdb;

public class Plugin : BasePlugin<GeneralOptions>, IHasWebPages
{

    public override Guid Id => new(PluginConfig.Guid);

    private readonly ILibraryManager _libraryManager;
    private readonly ITaskManager _taskManager;
    private readonly IFileSystem _fileSystem;
    private readonly IUserManager _userManager;


    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILoggerFactory logManager,
        IServerApplicationHost applicationHost,
        ILibraryManager libraryManager,
        ICollectionManager collectionManager,
        ITaskManager taskManager,
        IFileSystem fileSystem,
        IDirectoryService directoryService,
        IUserManager userManager
    ) : base(applicationPaths, xmlSerializer)
    {

        Instance = this;
        _userManager = userManager;
        ILogger<Plugin> logging = logManager.CreateLogger<Plugin>();
        LogManager.Initialize(logging);
        CollectionManager.Initialize(collectionManager);
        _fileSystem = fileSystem;
        _libraryManager = libraryManager;
        _taskManager = taskManager;

        PluginConfig.ClientID = applicationHost.SystemId;
        PluginConfig.ClientVersion = applicationHost.ApplicationVersion;
        PluginConfig.PluginVersion = GetType().Assembly.GetName().Version;

        SettingsManager.Initialize(applicationHost, Configuration, Name);
        Manager.Initialize(_libraryManager, directoryService, _taskManager, collectionManager, _fileSystem, _userManager);
    }


    public override string Description => PluginConfig.Description;
    public override string Name => PluginConfig.Name;
    public static Plugin Instance { get; private set; }


    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        }
        ];
    }

}

