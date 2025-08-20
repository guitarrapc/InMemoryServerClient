using CliClient.GameLift;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts;

namespace CliClient.GameLift;

/// <summary>
/// Service collection extensions for GameLift client services
/// </summary>
public static class GameLiftServiceCollectionExtensions
{
    /// <summary>
    /// Configure GameLift client services based on the specified mode
    /// </summary>
    public static IServiceCollection ConfigureGameLiftClientServices(this IServiceCollection services)
    {
        // Register GameLift client provider - it will handle GameLift client creation internally
        services.AddSingleton<IGameLiftClientProvider, GameLiftClientProvider>();

        return services;
    }
}
