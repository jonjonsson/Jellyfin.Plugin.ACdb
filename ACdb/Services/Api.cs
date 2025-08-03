using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services;

public class Api
{
    public async Task<string> Get(string apiKey, string url, CancellationToken cancellationToken)
    {
        string json = await WebRequestAPI(url, "GET", null, apiKey, cancellationToken).ConfigureAwait(false);
        return json;
    }

    public async Task<string> Post<T>(string apiKey, T data, string url, CancellationToken cancellationToken)
    {
        string jsonData = JsonManager.SerializeToString(data);
        string response = await WebRequestAPI(url, "POST", jsonData, apiKey, cancellationToken).ConfigureAwait(false);
        return response;
    }

    private async Task<string> WebRequestAPI(
        string url, 
        string method, 
        string postData = null, 
        string secret = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(secret))
        {
            LogManager.Error($"apiKey is null or empty, cannot make request to {url}");
            return null;
        }

        LogManager.Info($"Sending {method} request to {url}");

        var requestHeaders = new List<Dictionary<string, string>>
        {
            new() { { "Auth", $"Bearer {secret}" } }
        };

        try
        {
            if (method.Equals("POST", StringComparison.CurrentCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(postData))
                {
                    LogManager.Error($"postData is null or empty, cannot make POST request to {url}");
                    return null;
                }

                string result = await HttpClientManager.Post(
                    url,
                    20000,
                    requestHeaders,
                    postData.AsMemory(),
                    cancellationToken
                ).ConfigureAwait(false);

                LogManager.Info($"Received response: {result}");
                return result;
            }
            else
            {
                string result = await HttpClientManager.Get(
                    url,
                    20000,
                    requestHeaders,
                    cancellationToken
                ).ConfigureAwait(false);

                LogManager.Info($"Received response: {result}");
                return result;
            }
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error from {url} using method {method} {ex.Message}");
            return string.Empty;
        }
    }
}