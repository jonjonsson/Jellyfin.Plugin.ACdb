using System.Net.Http; // Jellyfin uses HttpResponseMessage
using MediaBrowser.Controller.Entities.Movies;
using System;
using ACdb.Settings;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ACdb.Model.Reporting;
using ACdb.Model.JobResponse;


namespace ACdb.Services.Collections;




public class PosterImageProvider : IRemoteImageProvider
{
    public string Name => PluginConfig.Name;

    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        try
        {
            return await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error fetching image from {url}: {ex.Message}");
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = ex.Message
            };
        }
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        List<RemoteImageInfo> images = new List<RemoteImageInfo>();

        if (!(item is BoxSet))
        {
            return [];
        }

        string collection_sid = SettingsManager.GetCollectionSidByGuid(item.Id);

        if (string.IsNullOrWhiteSpace(collection_sid))
        {
            LogManager.LogEvent(LogTypeEnum.error, $"Could not lookup ACdb ID for {item.Name}");
            return [];
        }

        string imageProviderUrl = string.Format(PluginConfig.ImageProviderUrl, collection_sid) + "/" + Manager.ApiKeyHashed;

        string json;
        try
        {
            json = await Manager.Utils.ApiCon.Get(null, imageProviderUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            LogManager.LogEvent(LogTypeEnum.error, $"Error fetching collection images from {imageProviderUrl}: {ex}");
            return [];
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        ImagesResponse imagesResponse;
        try
        {
            imagesResponse = JsonManager.DeserializeFromString<ImagesResponse>(json);
        }
        catch (Exception e)
        {
            LogManager.LogEvent(LogTypeEnum.error, $"Could not parse collection images from {imageProviderUrl}: {e}");
            return [];
        }

        if (imagesResponse == null || imagesResponse.images == null)
        {
            return [];
        }

        foreach (ACdbImageInfo image in imagesResponse.images)
        {
            if (string.IsNullOrWhiteSpace(image.url) || image.remove == true)
            {
                continue;
            }
            images.Add(new RemoteImageInfo
            {
                Type = image.type,
                ProviderName = Name,
                Url = image.url
            });
        }
        return images.ToArray();
    }


    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return [ImageType.Primary, ImageType.Backdrop];
    }

    public bool Supports(BaseItem item)
    {
        return item is BoxSet;
    }
}

