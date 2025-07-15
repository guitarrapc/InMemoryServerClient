using MagicOnion;
using Shared.Models;

namespace Shared.Contracts.MagicOnion;

/// <summary>
/// MagicOnion service interface for server status operations
/// </summary>
public interface IServerStatusService : IService<IServerStatusService>
{
    /// <summary>
    /// Get server status
    /// </summary>
    /// <returns>Current server status information</returns>
    UnaryResult<ServerStatus> GetServerStatusAsync();
}
