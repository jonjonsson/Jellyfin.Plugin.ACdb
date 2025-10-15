using ACdb.Model.JobResponse;
using ACdb.Model.Reporting;
using ACdb.Settings;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services
{
    public class Library
    {
        private readonly ILibraryManager _libraryManager;

        public Library(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }


        public void UpdateLibraryImages(LibrarySync librarySync)
        {

            if (librarySync == null || librarySync.images == null || librarySync.images.Count == 0)
            {
                return;
            }

            List<(string Name, string Id)> allLibraries = GetAll();

            foreach (LibraryImage libraryImage in librarySync.images)
            {
                (string Name, string Id) matchingLibrary = allLibraries.FirstOrDefault(lib => lib.Name.Equals(libraryImage.name, StringComparison.OrdinalIgnoreCase));
                if (matchingLibrary != default)
                {
                    BaseItem item = _libraryManager.GetItemById(matchingLibrary.Id);
                    SetImageForItem(item, libraryImage.poster_id);
                }
            }

        }

        private bool SetImageForItem(BaseItem item, string posterId)
        {
            if (item == null)
            {
                return false;
            }

            item.RemoveImages(item.ImageInfos.ToList());

            try
            {
                if (string.IsNullOrEmpty(posterId) == false)
                {
                    item.SetImage(new ItemImageInfo
                    {
                        Path = string.Format(PluginConfig.ImageLibraryUrl, posterId, Manager.ApiKeyHashed),
                        Type = ImageType.Primary
                    }, 0);
                }
                Manager.Utils.UpdateItem(item, ItemUpdateType.ImageUpdate);
                LogManager.LogEvent(LogTypeEnum.info, $"Poster set for {item.Name}");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        public async Task UpdateLibraryNames()
        {
            string currentHash = GetHashed(); // dateTimeNow;
            string lastHash = SettingsManager.GetLastLibraryHash();

            if (currentHash == lastHash)
            {
                return;
            }

            int status = 0;
            try
            {
                List<string> libraryNames = GetAll().Select(x => x.Name).ToList();
                string json = await Manager.Utils.ApiCon.Post(Manager.ApiKey, libraryNames, PluginConfig.AddLibrariesUrl, CancellationToken.None);
                Response response = JsonManager.DeserializeFromString<Response>(json);
                status = response.status;
                if (status == 200 || status == 204)
                {
                    SettingsManager.SetLastLibraryHash(currentHash);
                }
            }
            catch
            {
                LogManager.LogEvent(LogTypeEnum.error, $"Could not parse response from {PluginConfig.AddLibrariesUrl}");
            }
        }


        private List<(string Name, string Id)> GetAll()
        {
            var libraries = new List<(string Name, string Id)>();

            if (_libraryManager == null)
            {
                return libraries;
            }

            List<BaseItem> results = _libraryManager.RootFolder.GetChildren(Manager.Utils.GetAdminUser(), true);

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

        private string GetHashed()
        {
            var names = GetAll()
                .Select(x => x.Name)
                .OrderBy(name => name);

            var concatenated = string.Join(",", names);

            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(concatenated);
                var hashBytes = md5.ComputeHash(bytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}