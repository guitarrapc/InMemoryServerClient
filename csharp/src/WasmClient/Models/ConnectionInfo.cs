namespace WasmClient.Models;

public class ConnectionInfo
{
    public string ServerUrl { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public string? PlayerId { get; set; }
    public ConnectionType Type { get; set; }
}

public enum ConnectionType
{
    SignalR,
    MagicOnion
}
