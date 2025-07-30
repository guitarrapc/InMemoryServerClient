using InMemoryServer.Services;

namespace InMemoryServer.Extensions;

/// <summary>
/// Extension methods for service collection to register group management services
/// </summary>
public static class GroupManagerServiceExtensions
{
    /// <summary>
    /// Add Actor-based Group Manager services to the DI container
    /// </summary>
    public static IServiceCollection AddActorGroupManager(this IServiceCollection services)
    {
        // Register the actor as singleton
        services.AddSingleton<GroupManagerActor>();

        // Register the adapter as the interface implementation
        services.AddSingleton<IGroupManager>(serviceProvider =>
        {
            var actor = serviceProvider.GetRequiredService<GroupManagerActor>();
            return new GroupManagerAdapter(actor);
        });

        return services;
    }
}
