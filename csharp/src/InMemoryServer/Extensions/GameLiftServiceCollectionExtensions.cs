using Amazon.GameLift;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using InMemoryServer.Services;
using Shared.Models.GameLift;

namespace InMemoryServer.Extensions;

/// <summary>
/// Extension methods for configuring GameLift services
/// </summary>
public static class GameLiftServiceCollectionExtensions
{
    /// <summary>
    /// Configure GameLift services based on options
    /// </summary>
    /// <param name="builder">Web application builder</param>
    /// <param name="configSection">Configuration section name for GameLift options</param>
    /// <returns>Host application builder for chaining</returns>
    public static IHostApplicationBuilder ConfigureGameLiftServices(this WebApplicationBuilder builder, string configSection = "GameLift")
    {
        // Configure GameLift options
        builder.Services.Configure<GameLiftOptions>(builder.Configuration.GetSection(configSection));

        // Register factory
        builder.Services.AddSingleton<IGameServerProviderFactory, GameServerProviderFactory>();

        // Register a singleton instance of the selected provider for proper lifecycle management
        builder.Services.AddSingleton<IGameServerProvider>(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<IGameServerProviderFactory>();
            return factory.CreateProvider();
        });

        // Register providers - always register both, factory will choose which to use
        builder.Services.AddTransient<DirectConnectionProvider>();
        builder.Services.AddTransient<GameLiftAnywhereProvider>();

        // Conditionally register AWS GameLift client only for non-Direct modes
        // We check the configuration early to avoid registering unnecessary services
        var config = builder.Configuration.GetSection(configSection);
        var gameLiftMode = config.GetValue<string>("Mode") ?? "Direct";

        if (!string.Equals(gameLiftMode, "Direct", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IAmazonGameLift>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<GameLiftOptions>>().Value;
                return CreateGameLiftClient(options);
            });
        }

        return builder;
    }

    /// <summary>
    /// Creates an AWS GameLift client with proper credential chain handling
    /// </summary>
    /// <param name="options">GameLift configuration options</param>
    /// <returns>Configured GameLift client</returns>
    private static IAmazonGameLift CreateGameLiftClient(GameLiftOptions options)
    {
        var config = new AmazonGameLiftConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.AWS.Region)
        };

        // AWS credential chain priority (recommended order):
        // 1. AWS Profile (recommended for local development)
        // 2. AWS SSO Session (recommended for enterprise environments)
        // 3. Explicit credentials (deprecated, for testing only)
        // 4. Default credential chain (environment variables, IAM roles, etc.)

        if (!string.IsNullOrEmpty(options.AWS.Profile))
        {
            // Use AWS Profile - highest priority
            var profileChain = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain();
            if (profileChain.TryGetAWSCredentials(options.AWS.Profile, out var profileCredentials))
            {
                return new AmazonGameLiftClient(profileCredentials, config);
            }
            throw new InvalidOperationException($"Failed to load AWS credentials from profile: {options.AWS.Profile}");
        }

        if (!string.IsNullOrEmpty(options.AWS.SsoSessionName))
        {
            // Use AWS SSO Session - second priority
            // Note: This requires AWS CLI to be configured with SSO session
            return new AmazonGameLiftClient(config);
        }

        if (!string.IsNullOrEmpty(options.AWS.AccessKeyId) && !string.IsNullOrEmpty(options.AWS.SecretAccessKey))
        {
            // Use explicit credentials (deprecated for production) - third priority
            Amazon.Runtime.AWSCredentials credentials = !string.IsNullOrEmpty(options.AWS.SessionToken)
                ? new Amazon.Runtime.SessionAWSCredentials(options.AWS.AccessKeyId, options.AWS.SecretAccessKey, options.AWS.SessionToken)
                : new Amazon.Runtime.BasicAWSCredentials(options.AWS.AccessKeyId, options.AWS.SecretAccessKey);

            return new AmazonGameLiftClient(credentials, config);
        }        // Fall back to default credential chain (environment variables, IAM roles, etc.)
        return new AmazonGameLiftClient(config);
    }
}
