using ACdb.Model.Authentication;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services.Authentication;


internal class RegisterPlugin
{

    private static Api _api;

    public RegisterPlugin(Api api)
    {
        _api = api;
    }


    public async Task<(bool success, string message)> RegisterAsync(string api_key = null)
    {
        RegisterPluginRequest registerPluginRequest = new()
        {
            existing_api_key = api_key,
        };
        RegisterResponse response = await SendRegistration(registerPluginRequest);
        return ParseRegistrationReponse(response);
    }

    private (bool success, string message) ParseRegistrationReponse(RegisterResponse response)
    {
        if (response is null)
        {
            return (false, "Did not get response from server.");
        }

        if (response.status != 200 || response.api_key is null || response.api_key == string.Empty)
        {
            string errorMsg = response.message;
            if (errorMsg is null || errorMsg == string.Empty)
            {
                errorMsg = "No error message from server.";
            }
            return (false, errorMsg);
        }

        Manager.ApiKey = response.api_key;
        return (true, null);
    }

    private async Task<RegisterResponse> SendRegistration(RegisterPluginRequest registerPluginRequest)
    {
        string secretKey;
        try
        {
            secretKey = SecretKeyGenerator.GenerateSecretKey(registerPluginRequest.client_id, PluginConfig.Secret);
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error generating secret key: {ex.Message}");
            return null;
        }

        string json;
        try
        {
                json = await _api.Post(secretKey, registerPluginRequest, PluginConfig.RegisterPluginUrl, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogManager.Error($"Exception during API post request: {ex.Message}");
            return null;
        }

        if (string.IsNullOrEmpty(json))
        {
            LogManager.Error($"Request to {PluginConfig.RegisterPluginUrl} returned nothing or failed. See log for more information.");
            return null;
        }

        RegisterResponse response;
        try
        {
            response = JsonManager.DeserializeFromString<RegisterResponse>(json);
        }
        catch (Exception e)
        {
            LogManager.Error($"Error processing incoming json: {e.Message}");
            return null;
        }

        if (response == null)
        {
            LogManager.Error("Deserialized response is null.");
            return null;
        }

        int status = response.status;
        string message = response.message;
        Version pluginMinVersion = new(response.plugin_min_version);
        Version clientMinVersion = new(response.client_min_version);

        if (status != 200)
        {
            string msg = response.message;
            if (pluginMinVersion > PluginConfig.PluginVersion)
            {
                LogManager.Error($"Plugin version is too old. Please update to version {pluginMinVersion} or newer.");
                response.message = $"Plugin version is too old. {msg}";
                return response;
            }
            LogManager.Error($"Error registering plugin. Message from server: {message}");
            return response;
        }

        LogManager.Info("SendRegistration completed successfully.");
        return response;
    }

    public async Task<string> GetLoginTokenAsync(string apiKey)
    {
        string url = PluginConfig.GenerateLoginUrl;
        string json;
        try
        {
            json = await _api.Get(apiKey, url, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogManager.Error($"Exception during API GET request: {ex.Message}");
            return null;
        }

        if (string.IsNullOrEmpty(json))
        {
            LogManager.Error($"Request to {url} returned nothing or failed.");
            return null;
        }

        try
        {
            Dictionary<string, string> obj = JsonManager.DeserializeFromString<Dictionary<string, string>>(json);
            if (obj != null && obj.TryGetValue("token", out var token))
            {
                return token;
            }
            LogManager.Error("Token not found in response.");
            return null;
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error processing incoming json: {ex.Message}");
            return null;
        }
    }

}
