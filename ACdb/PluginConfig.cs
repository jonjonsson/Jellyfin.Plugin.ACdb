using System;

namespace ACdb;


public static class PluginConfig
{
    public static string Name { get; } = "ACdb.tv";
    public static string Description { get; } = "Create automated collections for your media library";
    public static string PluginType { get; } =
        Model.PluginType.jellyfin.ToString();
    public static Version PluginVersion { get; set; } 
    public static string ClientID { get; set; }
    public static Version ClientVersion { get; set; }

    public static string Guid { get; } = "06411f7c-08f6-41da-9e2a-a79b56144845"; 

    public static string WebSiteUrl { get; } = "https://acdb.tv"; 

    public static string ApiBaseUrl { get; } = "https://api.acdb.tv/api"; 

    public static string WebSiteLoginUrl { get; } = $"{WebSiteUrl}/login";
    public static string CollectionIdUrl { get; } = $"{WebSiteUrl}/collection_id/";
    public static string ApiGetJobsUrl { get; } = $"{ApiBaseUrl}/jobs";
    public static string PostJobResultsUrl { get; } = $"{ApiBaseUrl}/report";
    public static string RegisterPluginUrl { get; } = $"{ApiBaseUrl}/register";
    public static string GenerateLoginUrl { get; } = $"{ApiBaseUrl}/get-login-token";
    public static string ImageLibraryUrl { get; } = WebSiteUrl + "/static/images/library/{0}/max/{1}?set_as_sent=true";
    public static string ImageProviderUrl { get; } = ApiBaseUrl + "/images/collection/{0}";
    public static string AddLibrariesUrl { get; } = $"{ApiBaseUrl}/add-libraries";


    public static int ScheduledTaskMinutesBetweenRuns { get; } = 120; 
    public static string ScheduledTaskName { get; } = "Check for pending jobs";
    public static string ScheduledTaskDescription { get; } = $"Checks if {Name} has any new jobs.";
    public static string ScheduledTaskCategory { get; } = "ACdb.tv";
    public static string ScheduledTaskKey { get; } = "acdb";
    public static string Secret { get; } = "matarbillnizzamy"; 

    
}
