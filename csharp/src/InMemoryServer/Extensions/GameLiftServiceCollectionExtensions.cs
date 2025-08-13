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
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IHostApplicationBuilder ConfigureGameLiftServices(this WebApplicationBuilder builder, string configSection = "GameLift")
    {
        // Configure GameLift options
        builder.Services.Configure<GameLiftOptions>(builder.Configuration.GetSection(configSection));

        // Register factory
        builder.Services.AddSingleton<IGameServerProviderFactory, GameServerProviderFactory>();

        // Register providers
        builder.Services.AddTransient<DirectConnectionProvider>();
        builder.Services.AddTransient<GameLiftAnywhereProvider>();

        // Register AWS GameLift client only for GameLift modes
        builder.Services.AddSingleton<IAmazonGameLift>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GameLiftOptions>>().Value;

            if (options.Mode == GameLiftMode.Direct)
            {
                // For direct mode, throw an exception if GameLift client is requested
                throw new InvalidOperationException("GameLift client is not available in Direct mode");
            }

            var config = new AmazonGameLiftConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.AWS.Region)
            };

            // Configure AWS credentials based on options
            if (!string.IsNullOrEmpty(options.AWS.Profile))
            {
                // Use AWS Profile
                var profileChain = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain();
                if (profileChain.TryGetAWSCredentials(options.AWS.Profile, out var credentials))
                {
                    return new AmazonGameLiftClient(credentials, config);
                }
            }

            if (!string.IsNullOrEmpty(options.AWS.SsoSessionName))
            {
                // Use AWS SSO Session
                // Note: This requires AWS CLI to be configured with SSO
                return new AmazonGameLiftClient(config);
            }

            if (!string.IsNullOrEmpty(options.AWS.AccessKeyId) && !string.IsNullOrEmpty(options.AWS.SecretAccessKey))
            {
                // Use explicit credentials (deprecated for production)
                var credentials = new Amazon.Runtime.BasicAWSCredentials(options.AWS.AccessKeyId, options.AWS.SecretAccessKey);
                return new AmazonGameLiftClient(credentials, config);
            }

            // Fall back to default credential chain (environment variables, IAM roles, etc.)
            return new AmazonGameLiftClient(config);
        });

        return builder;
    }
}
