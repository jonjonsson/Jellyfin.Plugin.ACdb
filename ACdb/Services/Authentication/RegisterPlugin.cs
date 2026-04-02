using ACdb.Model.Authentication;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ACdb.Services.Authentication
{

    internal static class RegisterPlugin
    {


        public static async Task<(bool success, string message)> VerifyExistingApiKey(string api_key)
        {
            RegisterResponse response = await SendRegistration(new RegisterPluginRequest
            {
                existing_api_key = api_key,
            });
            return await ProcessRegistrationResultAsync(response);
        }

        public static async Task<(bool success, string message)> RegisterAsync()
        {
            RegisterResponse response = await SendRegistration(new RegisterPluginRequest());
            return await ProcessRegistrationResultAsync(response);
        }

        private static async Task<RegisterResponse> SendRegistration(RegisterPluginRequest registerPluginRequest)
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
                json = await Manager.Utils.ApiCon.Post(secretKey, registerPluginRequest, PluginConfig.RegisterPluginUrl, CancellationToken.None);
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
                LogManager.Error($"Error processing incoming json while sending registration: {e.Message}");
                return null;
            }

            if (response == null)
            {
                LogManager.Error("Deserialized response is null.");
                return null;
            }

            int status = response.status;
            string message = response.message;
            Version pluginMinVersion = new Version(response.plugin_min_version);
            Version clientMinVersion = new Version(response.client_min_version);

            if (status != 200)
            {
                string msg = response.message;

                if (clientMinVersion > PluginConfig.ClientVersion)
                {
                }

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


        private static async Task<(bool success, string message)> ProcessRegistrationResultAsync(RegisterResponse response)
        {
            if (response is null)
            {
                return (false, "Did not get any reponse from server.");
            }

            if (response.status != 200 || response.api_key is null || response.api_key == string.Empty)
            {
                if (response.message is null || response.message == string.Empty)
                {
                    response.message = "No error message from server.";
                }
                return (false, response.message);
            }

            Manager.ApiKey = response.api_key;

            if (response.is_new == false)
            {
                await Manager.ExecuteJobTaskAsync();
                await Task.Delay(5000);
                await Manager.ExecuteJobTaskAsync(); // If user has multiple plugin secrets, this will get library images on this sync
                return (true, "You are logged in.");
            }
            else
            {
                return (true, "User created successfully.");
            }
        }


        public static async Task<string> GetLoginTokenAsync(string apiKey)
        {
            string url = PluginConfig.GenerateLoginUrl;
            string json;
            try
            {
                json = await Manager.Utils.ApiCon.Get(apiKey, url, CancellationToken.None).ConfigureAwait(false);
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
                LogManager.Error($"Error processing incoming json while trying to get login token {ex.Message}");
                return null;
            }
        }

    }
}
