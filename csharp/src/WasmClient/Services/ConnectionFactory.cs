using WasmClient.Models;

namespace WasmClient.Services;

/// <summary>
/// Connection factory implementation for WasmClient
/// </summary>
public class ConnectionFactory : IConnectionFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ConnectionFactory> _logger;

    public ConnectionFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ConnectionFactory>();
    }

    public async Task<IBattleConnection> CreateSignalRConnectionAsync(ConnectionInfo connectionInfo)
    {
        _logger.LogInformation("Creating SignalR connection to {ServerUrl}", connectionInfo.ServerUrl);

        var signalRLogger = _loggerFactory.CreateLogger<SignalRConnection>();
        var connection = new SignalRConnection(connectionInfo, signalRLogger);

        var connected = await connection.ConnectAsync();
        if (!connected)
        {
            await connection.DisposeAsync();
            throw new InvalidOperationException($"Failed to connect to SignalR server: {connectionInfo.ServerUrl}");
        }

        return connection;
    }

    public async Task<IBattleConnection> CreateMagicOnionConnectionAsync(ConnectionInfo connectionInfo)
    {
        _logger.LogInformation("Creating MagicOnion connection to {ServerUrl}", connectionInfo.ServerUrl);

        // TODO: Implement MagicOnion connection
        var connection = new MagicOnionConnection(connectionInfo);

        var connected = await connection.ConnectAsync();
        if (!connected)
        {
            await connection.DisposeAsync();
            throw new InvalidOperationException($"Failed to connect to MagicOnion server: {connectionInfo.ServerUrl}");
        }

        return connection;
    }
}
