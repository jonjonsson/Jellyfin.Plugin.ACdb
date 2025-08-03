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

    public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        List<RemoteImageInfo> images = [];

        if (item is BoxSet == false)
        {
            return Task.FromResult<IEnumerable<RemoteImageInfo>>(images);
        }

        if (SettingsManager.IsCollectionWithPoster(item.Id) == false)
        {
            LogManager.Info($"Collection {item.Name} does not have poster");
            return Task.FromResult<IEnumerable<RemoteImageInfo>>(images);
        }

        string collection_sid = SettingsManager.GetCollectionSidByGuid(item.Id);

        if (string.IsNullOrWhiteSpace(collection_sid))
        {
            LogManager.Error($"Could not lookup ACdb ID for {item.Name}");
            return Task.FromResult<IEnumerable<RemoteImageInfo>>(images);
        }

        string url = string.Format(PluginConfig.ImageUrlTemplate, collection_sid);
        string apiKeyHashed = Manager.ApiKeyHashed;

        if (string.IsNullOrWhiteSpace(apiKeyHashed) == false)
        {
            url = $"{url}/{apiKeyHashed}";
        }

        images.Add(new RemoteImageInfo
        {
            Type = ImageType.Primary,
            ProviderName = Name,
            Url = url
        });

        return Task.FromResult<IEnumerable<RemoteImageInfo>>(images);
    }


    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return [ImageType.Primary];
    }

    public bool Supports(BaseItem item)
    {
        return item is BoxSet;
    }
}

