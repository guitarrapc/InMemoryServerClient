using Amazon.GameLift;
using Amazon.GameLift.Model;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Models.GameLift;

namespace InMemoryServer.Services;

/// <summary>
/// Provides GameLift Fleet Anywhere server functionality
/// </summary>
public class GameLiftAnywhereProvider(IAmazonGameLift gameLiftClient, IOptions<GameLiftOptions> options, ILogger<GameLiftAnywhereProvider> logger) : IGameServerProvider
{
    // Server SDK will be integrated in future iteration
    private ComputeInfo? _currentCompute;
    private AuthTokenInfo? _currentAuthToken;

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Initializing GameLift Anywhere provider");

            // Register compute if not exists
            var compute = await RegisterComputeAsync(cancellationToken);
            if (compute.ComputeName == string.Empty)
            {
                logger.LogError("Failed to register compute");
                return false;
            }

            // Get auth token
            var authToken = await GetAuthTokenAsync(cancellationToken);
            if (authToken.AuthToken == string.Empty)
            {
                logger.LogError("Failed to get auth token");
                return false;
            }

            _currentCompute = compute;
            _currentAuthToken = authToken;

            logger.LogInformation("GameLift Anywhere provider initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize GameLift Anywhere provider");
            return false;
        }
    }

    public async Task<ComputeInfo> RegisterComputeAsync(CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        try
        {
            // Check if compute already exists
            var listRequest = new ListComputeRequest
            {
                FleetId = o.Anywhere.FleetId
            };

            var listResponse = await gameLiftClient.ListComputeAsync(listRequest, cancellationToken);
            var existingCompute = listResponse.ComputeList
                .FirstOrDefault(c => c.ComputeName == o.Anywhere.ComputeName);

            if (existingCompute != null)
            {
                logger.LogInformation("Using existing compute: {ComputeName}", existingCompute.ComputeName);
                return new ComputeInfo(
                    existingCompute.ComputeName,
                    existingCompute.FleetId,
                    o.Anywhere.CustomLocation,
                    existingCompute.ComputeArn ?? string.Empty,
                    Enum.Parse<Shared.Models.GameLift.ComputeStatus>(existingCompute.ComputeStatus.ToString())
                );
            }

            // Register new compute
            var registerRequest = new RegisterComputeRequest
            {
                FleetId = o.Anywhere.FleetId,
                ComputeName = o.Anywhere.ComputeName,
                Location = o.Anywhere.CustomLocation
            };

            var registerResponse = await gameLiftClient.RegisterComputeAsync(registerRequest, cancellationToken);

            logger.LogInformation("Registered new compute: {ComputeName}", registerResponse.Compute.ComputeName);
            return new ComputeInfo(
                registerResponse.Compute.ComputeName,
                registerResponse.Compute.FleetId,
                o.Anywhere.CustomLocation,
                registerResponse.Compute.ComputeArn ?? string.Empty,
                Enum.Parse<Shared.Models.GameLift.ComputeStatus>(registerResponse.Compute.ComputeStatus.ToString())
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register compute");
            return new ComputeInfo(string.Empty, string.Empty, string.Empty, string.Empty, Shared.Models.GameLift.ComputeStatus.Unknown);
        }
    }

    public async Task<AuthTokenInfo> GetAuthTokenAsync(CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        try
        {
            var request = new GetComputeAuthTokenRequest
            {
                FleetId = o.Anywhere.FleetId,
                ComputeName = o.Anywhere.ComputeName
            };

            var response = await gameLiftClient.GetComputeAuthTokenAsync(request, cancellationToken);

            logger.LogInformation("Retrieved auth token for compute: {ComputeName}", o.Anywhere.ComputeName);
            return new AuthTokenInfo(
                response.AuthToken,
                response.FleetArn ?? string.Empty,
                response.ExpirationTimestamp ?? DateTime.UtcNow.AddHours(1)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get auth token");
            return new AuthTokenInfo(string.Empty, string.Empty, DateTime.UtcNow);
        }
    }

    public Task<bool> InitServerSdkAsync(AuthTokenInfo authToken, CancellationToken cancellationToken = default)
    {
        // TODO: Implement Server SDK initialization in future iteration
        logger.LogInformation("Server SDK initialization - placeholder implementation");
        return Task.FromResult(true);
    }

    public Task<bool> ProcessReadyAsync(ProcessParameters parameters, CancellationToken cancellationToken = default)
    {
        // TODO: Implement Process Ready in future iteration
        logger.LogInformation("Process Ready - placeholder implementation");
        return Task.FromResult(true);
    }

    public Task ActivateGameSessionAsync(string gameSessionId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement Game Session activation in future iteration
        logger.LogInformation("Activate Game Session: {GameSessionId} - placeholder implementation", gameSessionId);
        return Task.CompletedTask;
    }

    public Task ProcessEndingAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement Process Ending in future iteration
        logger.LogInformation("Process Ending - placeholder implementation");
        return Task.CompletedTask;
    }

    public Task<string> GetConnectionEndpointAsync(CancellationToken cancellationToken = default)
    {
        // Return the WebSocket URL for client connections
        var o = options.Value;
        var endpoint = o.Anywhere.WebSocketUrl;
        logger.LogInformation("Connection endpoint: {Endpoint}", endpoint);
        return Task.FromResult(endpoint);
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Shutting down GameLift Anywhere provider");
        return Task.CompletedTask;
    }
}
