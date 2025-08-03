using ACdb.Model.Reporting;
using ACdb.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ACdb.Configuration;

[ApiController]
[Route("Plugins/ACdb")]
public class ConfigController : ControllerBase
{
    private static bool _taskCompleted = false;

    public static string PluginSecret
    {
        get { return Manager.ApiKey; }
        set { Manager.ApiKey = value; }
    }

    public static string CurrentState
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Manager.ApiKey))
            {
                return "loggedOut";
            }
            return "loggedIn";
        }
    }


    public static string LastSynced
    {
        get
        {
            string timeSinceLastSync = Manager.TimeSinceJobLastRan();
            if (timeSinceLastSync == null)
            {
                return "Not ran yet";
            }
            return timeSinceLastSync;
        }
    }

    public static string NextSync
    {
        get
        {
            string timeUntilNextSync = Manager.TimeUntilNextSync();
            if (timeUntilNextSync == null)
            {
                return "No sync scheduled!";
            }
            return $"{timeUntilNextSync}";
        }
    }

    public static string PluginVersion
    {
        get
        {
            return $"v. {PluginConfig.PluginVersion}";
        }
    }

    [HttpGet("GetPluginInfo")]
    public IActionResult GetPluginInfo()
    {
        return Ok(new
        {
            lastSynced = LastSynced,
            nextSync = NextSync,
            pluginVersion = PluginVersion
        });
    }

    [HttpGet("GetPluginSecret")]
    public IActionResult GetPluginSecret()
    {
        return Ok(new { pluginSecret = PluginSecret });
    }

    [HttpGet("GetCurrentState")]
    public IActionResult GetCurrentState()
    {
        return Ok(new { currentState = CurrentState });
    }

    [HttpGet("GetPosterGrid")]
    public async Task<IActionResult> GetPosterGridApiUrl()
    {
        string json = await HttpClientManager.Get($"{PluginConfig.WebSiteUrl}/poster_grid_data?full_url=true");
        return Ok(new { json });
    }

    [HttpPost("ButtonClick")]
    public async Task<IActionResult> ButtonClick([FromBody] ButtonClickRequest request)
    {
        bool success;
        string message;

        LogManager.EventList.Reset();
        _taskCompleted = false;

        if (request.ButtonType == "LoginButton")
        {
            (success, message) = await LogInAsync(request.PluginSecret);
        }
        else if (request.ButtonType == "WebWithTokenButton")
        {
            (success, message) = await GetLoginLink();
        }
        else if (request.ButtonType == "WebAccountWithTokenButton")
        {
            (success, message) = await GetAccountLink();
        }
        else if (request.ButtonType == "WebUrlWithTokenButton")
        {
            (success, message) = await GetUrlWithToken(request.Url);
        }
        else if (request.ButtonType == "CreateUserButton")
        {
            (success, message) = await Createuser();
        }
        else if (request.ButtonType == "SyncNowButton")
        {
            (success, message) = await SyncNow();
        }
        else if (request.ButtonType == "LogoutButton")
        {
            Manager.Logout();
            success = true;
            message = "You have been logged out.";
        }
        else
        {
            return Ok(new { success = true, message = "", currentState = CurrentState });

        }

        await Task.Delay(1000); // Buffer so polling has time to finish before showing our results
        _taskCompleted = true;
        return Ok(new { success, message, currentState = CurrentState });
    }

    public class ButtonClickRequest
    {
        public string ButtonType { get; set; }
        public string PluginSecret { get; set; }
        public string Url { get; set; }
    }

    [HttpPost("SyncNow")]
    public static async Task<(bool success, string message)> SyncNow()
    {
        LogManager.Info("Geting new jobs from ACdb.tv");
        await Manager.ExecuteJobTaskAsync();
        return (true, "Finished");
    }

    [HttpGet("SyncStatus")]
    public IActionResult SyncStatus()
    {
        string[] statuses = [];
        string[] errors = [];

        IReadOnlyList<Event> events = LogManager.EventList.GetAll();

        foreach (Event evt in events)
        {
            if (evt.LogType == LogTypeEnum.error || evt.LogType == LogTypeEnum.warning)
            {
                errors = errors.Append(evt.Message).ToArray();
            }
            else
            {
                statuses = statuses.Append(evt.Message).ToArray();
            }
        }

        return Ok(new
        {
            statuses,
            errors,
            _taskCompleted
        });
    }

    private static async Task<(bool success, string message)> LogInAsync(string pluginSecret)
    {
        if (string.IsNullOrWhiteSpace(pluginSecret))
        {
            return (true, "Plugin secret is empty. Please enter your Secret key from ACdb.tv.");
        }

        (bool success, string message) = await Manager.RegisterWithApiKey(pluginSecret);
        if (success)
        {
            message = "You are logged in.";
            await SyncNow();
        }

        return (success, message);
    }

    private static async Task<(bool success, string message)> Createuser()
    {
        (bool success, string message) = await Manager.CreateUser();
        if (success)
        {
            message = "User created successfully.";
        }
        else
        {
            message = "Failed to create user: " + message;
        }
        return (success, message);
    }

    private static async Task<(bool success, string message)> GetLoginLink()
    {
        string token = await Manager.GetLoginTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return (false, "Failed to generate login token.");
        }
        string loginUrl = $"{PluginConfig.WebSiteUrl}?login={token}&source=plugin";
        return (true, loginUrl);
    }

    private static async Task<(bool success, string message)> GetAccountLink()
    {
        string token = await Manager.GetLoginTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return (false, "Failed to generate login token.");
        }
        string loginUrl = $"{PluginConfig.WebSiteUrl}/account?login={token}&source=plugin";
        return (true, loginUrl);
    }

    private static async Task<(bool success, string message)> GetUrlWithToken(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (false, "URL is required.");
        }

        if (string.IsNullOrEmpty(Manager.ApiKey))
        {
            return (true, $"{url}?source=plugin");
        }

        string token = await Manager.GetLoginTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return (false, "Failed to generate login token.");
        }
        string separator = url.Contains('?') ? "&" : "?";
        string urlWithToken = $"{url}{separator}login={token}&source=plugin";
        return (true, urlWithToken);
    }
}

