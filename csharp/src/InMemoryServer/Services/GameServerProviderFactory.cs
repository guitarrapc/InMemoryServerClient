using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Models.GameLift;

namespace InMemoryServer.Services;

/// <summary>
/// Factory for creating game server providers
/// </summary>
internal class GameServerProviderFactory(IServiceProvider serviceProvider, IOptions<GameLiftOptions> options) : IGameServerProviderFactory
{
    public IGameServerProvider CreateProvider()
    {
        var o = options.Value;
        return o.Mode switch
        {
            GameLiftMode.Direct => serviceProvider.GetRequiredService<DirectConnectionProvider>(),
            GameLiftMode.Anywhere => serviceProvider.GetRequiredService<GameLiftAnywhereProvider>(),
            GameLiftMode.FleetIQ => throw new NotImplementedException("GameLift FleetIQ support will be implemented in Phase 2"),
            _ => throw new ArgumentOutOfRangeException(nameof(o.Mode), o.Mode, "Unknown GameLift mode")
        };
    }
}
