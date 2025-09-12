namespace WasmClient.Models;

public class ConnectionInfo
{
    public required string ServerUrl { get; init; }
    public required string GroupName { get; init; }
    public required ConnectionType Type { get; init; }
}
