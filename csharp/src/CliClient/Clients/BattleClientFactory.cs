using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.Models;

namespace CliClient.Clients;

/// <summary>
/// Factory for creating IInMemoryServerClient instances
/// </summary>
public static class BattleClientFactory
{
    /// <summary>
    /// Create a new IInMemoryServerClient instance
    /// </summary>
    /// <param name="connectionType">Connection type to use</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>IInMemoryServerClient instance</returns>
    public static IBattleClient Create(
        ConnectionType connectionType,
        ILoggerFactory loggerFactory)
    {
        return connectionType switch
        {
            ConnectionType.SignalR => new SignalRBattleClient(
                loggerFactory.CreateLogger<SignalRBattleClient>()),
            ConnectionType.MagicOnion => new MagicOnionBattleClient(
                loggerFactory.CreateLogger<MagicOnionBattleClient>()),
            _ => throw new ArgumentException($"Unsupported connection type: {connectionType}")
        };
    }

    /// <summary>
    /// Create a new IInMemoryServerClient instance with default SignalR connection
    /// </summary>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>IInMemoryServerClient instance</returns>
    public static IBattleClient Create(ILoggerFactory loggerFactory)
    {
        return Create(ConnectionType.SignalR, loggerFactory);
    }
}
