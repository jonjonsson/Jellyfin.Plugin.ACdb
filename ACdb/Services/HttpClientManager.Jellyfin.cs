using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services;

internal static class HttpClientManager
{
    private static readonly HttpClient _httpClient = new();


    public static async Task<string> Get(string url)
    {
        using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public static async Task<string> Get(
        string url, 
        int timeoutMs, 
        List<Dictionary<string, string>> requestHeaders, 
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (requestHeaders != null)
        {
            foreach (var headerDict in requestHeaders)
            {
                foreach (var kvp in headerDict)
                {
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cts.Token).ConfigureAwait(false);
    }

    public static async Task<string> Post(
        string url,
        int timeoutMs,
        List<Dictionary<string, string>> requestHeaders,
        ReadOnlyMemory<char> postData,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(postData.ToString())
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        if (requestHeaders != null)
        {
            foreach (var headerDict in requestHeaders)
            {
                foreach (var kvp in headerDict)
                {
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cts.Token).ConfigureAwait(false);
    }
}
