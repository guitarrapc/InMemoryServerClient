using Amazon.GameLift;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.GameLift;

namespace CliClient.GameLift;

/// <summary>
/// Extension methods for configuring GameLift client services
/// </summary>
public static class GameLiftClientServiceCollectionExtensions
{
    /// <summary>
    /// Configure GameLift client services based on options
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection ConfigureGameLiftClientServices(this IServiceCollection services)
    {
        // Register GameLift client provider
        services.AddSingleton<IGameLiftClientProvider, GameLiftClientProvider>();

        // Register AWS GameLift client only for GameLift modes
        services.AddSingleton<IAmazonGameLift>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GameLiftOptions>>().Value;

            if (options.Mode == GameLiftMode.Direct)
            {
                // For direct mode, don't register any GameLift client
                return NullAmazonGameLift.Default;
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
                return new AmazonGameLiftClient(config);
            }

            if (!string.IsNullOrEmpty(options.AWS.AccessKeyId) && !string.IsNullOrEmpty(options.AWS.SecretAccessKey))
            {
                // Use explicit credentials (deprecated for production)
                var credentials = new Amazon.Runtime.BasicAWSCredentials(options.AWS.AccessKeyId, options.AWS.SecretAccessKey);
                return new AmazonGameLiftClient(credentials, config);
            }

            // Fall back to default credential chain
            return new AmazonGameLiftClient(config);
        });

        return services;
    }
}
