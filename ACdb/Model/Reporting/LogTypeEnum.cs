namespace ACdb.Model.Reporting;

public enum LogTypeEnum // For Emby and server compatibility, keep lowercase for reporting back to server
{
    error, // Keep lowercase for reporting back to server
    info, // Keep lowercase for reporting back to server
    warning, // Keep lowercase for reporting back to server
    debug, // Keep lowercase for reporting back to server
    fatal // Keep lowercase for reporting back to server
}
