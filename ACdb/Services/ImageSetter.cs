using ACdb.Model.JobResponse;
using ACdb.Model.Reporting;
using ACdb.Settings;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace ACdb.Services
{
    internal class ImageSetter
    {

        public async Task FetchAndSetImageForCollection_sid(HashSet<BaseItem> collections)
        {
            foreach (BaseItem collection in collections)
            {
                string collection_sid = SettingsManager.GetCollectionSidByGuid(collection.Id);
                if (collection_sid == null)
                {
                    LogManager.LogEvent(LogTypeEnum.info, $"Could not get ACdb ID for {collection.Name}");
                    continue;
                }
                string imageProviderUrl = $"{string.Format(PluginConfig.ImageProviderUrl, collection_sid)}/{Manager.ApiKeyHashed}?set_as_sent=true";
                await FetchAndSetImageForItem(collection, imageProviderUrl);
            }
        }

        private async Task FetchAndSetImageForItem(BaseItem item, string imageProviderUrl)
        {
            string json = await Manager.Utils.ApiCon.Get(null, imageProviderUrl, CancellationToken.None);
            if (string.IsNullOrEmpty(json))
            {
                LogManager.LogEvent(LogTypeEnum.error, $"Could not get collection images information from {imageProviderUrl}");
                return;
            }

            ImagesResponse imagesResponse;
            try
            {
                imagesResponse = JsonManager.DeserializeFromString<ImagesResponse>(json);
            }
            catch (Exception e)
            {
                LogManager.LogEvent(LogTypeEnum.error, $"Could not get collection images information from {imageProviderUrl} {e.Message}");
                return;
            }


            int index = 0;
            foreach (ACdbImageInfo image in imagesResponse.images)
            {
                if (image.remove)
                {
                    ItemImageInfo removeImage = item.ImageInfos.FirstOrDefault(i => i.Type == image.type);
                    if (removeImage != null)
                    {
                        item.RemoveImage(removeImage);

                        if (image.type == ImageType.Primary)
                        {
                            await RefreshImageMetadata(item);
                        }
                    }
                    continue;
                }

                ItemImageInfo existingImage = item.ImageInfos.FirstOrDefault(i => i.Type == image.type);
                if (existingImage != null)
                {
                    item.RemoveImage(existingImage);
                }

                item.SetImage(new ItemImageInfo
                {
                    Path = image.url,
                    Type = image.type
                }, index);
                index++;
            }

            Manager.Utils.UpdateItem(item, ItemUpdateType.ImageUpdate);
        }

        private async Task RefreshImageMetadata(BaseItem collectionItem)
        {
            var options = new MetadataRefreshOptions(Manager.Utils.MetadataRefreshOptionsParam)
            {
                ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                ReplaceAllImages = true,
            };

            try
            {
                LogManager.LogEvent(LogTypeEnum.info, $"Forcing metadata refresh for images for collection: {collectionItem.Name}");
                await collectionItem.RefreshMetadata(options, new CancellationToken());
            }
            catch
            {
                LogManager.LogEvent(LogTypeEnum.error, $"Error forcing refreshing metadata for collection: {collectionItem.Name}", new ActivityLogEventArgs { Description = "Refresh failed" });
            }
        }

    }
}