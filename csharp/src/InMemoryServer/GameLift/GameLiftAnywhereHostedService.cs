using Amazon.GameLift;
using Amazon.GameLift.Model;
using Aws.GameLift.Server;
using Microsoft.Extensions.Options;
using Shared.GameLift;

namespace InMemoryServer.GameLift;

/// <summary>
/// Hosted service for managing GameLift Anywhere server lifecycle
/// </summary>
internal sealed class GameLiftAnywhereHostedService(
    IAmazonGameLift gameLiftClient,
    IOptions<GameLiftOptions> options,
    ILogger<GameLiftAnywhereHostedService> logger) : BackgroundService
{
    private ComputeInfo? _currentCompute;
    private AuthTokenInfo? _currentAuthToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting GameLift Anywhere hosted service");

            // Initialize GameLift Anywhere
            if (!await InitializeGameLiftAsync(stoppingToken))
            {
                logger.LogError("Failed to initialize GameLift Anywhere");
                return;
            }

            // Initialize Server SDK
            if (!await InitializeServerSdkAsync(stoppingToken))
            {
                logger.LogError("Failed to initialize GameLift Server SDK");
                return;
            }

            logger.LogInformation("GameLift Anywhere hosted service started successfully");

            // Keep the service running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("GameLift Anywhere hosted service is stopping");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GameLift Anywhere hosted service");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping GameLift Anywhere hosted service");

        try
        {
            // Notify GameLift that the process is ending
            await ProcessEndingAsync();

            logger.LogInformation("GameLift Anywhere hosted service stopped successfully");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during GameLift Anywhere shutdown");
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task<bool> InitializeGameLiftAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        try
        {
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

            logger.LogInformation("GameLift control plane initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize GameLift control plane");
            return false;
        }
    }

    private async Task<ComputeInfo> RegisterComputeAsync(CancellationToken cancellationToken)
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
                    Enum.Parse<Shared.GameLift.ComputeStatus>(existingCompute.ComputeStatus.ToString(), true)
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
                Enum.Parse<Shared.GameLift.ComputeStatus>(registerResponse.Compute.ComputeStatus.ToString(), true)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register compute");
            return new ComputeInfo(string.Empty, string.Empty, string.Empty, string.Empty, Shared.GameLift.ComputeStatus.Unknown);
        }
    }

    private async Task<AuthTokenInfo> GetAuthTokenAsync(CancellationToken cancellationToken)
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

            // Build WebSocket URL based on AWS region
            var region = o.AWS.Region;
            var websocketUrl = $"wss://gamelift.{region}.amazonaws.com";

            logger.LogInformation("Retrieved auth token for compute: {ComputeName}", o.Anywhere.ComputeName);
            logger.LogInformation("Using WebSocket URL: {WebSocketUrl} for region: {Region}", websocketUrl, region);

            return new AuthTokenInfo(
                response.AuthToken,
                websocketUrl, // Use region-based WebSocket URL
                response.ExpirationTimestamp ?? DateTime.UtcNow.AddHours(1)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get auth token");
            return new AuthTokenInfo(string.Empty, string.Empty, DateTime.UtcNow);
        }
    }

    private async Task<bool> InitializeServerSdkAsync(CancellationToken cancellationToken)
    {
        if (_currentAuthToken == null || _currentCompute == null)
        {
            logger.LogError("Cannot initialize Server SDK without auth token and compute info");
            return false;
        }

        var o = options.Value;
        try
        {
            // Use WebSocket URL from auth token response
            var webSocketUrl = _currentAuthToken.Value.WebSocketUrl;
            if (string.IsNullOrEmpty(webSocketUrl))
            {
                logger.LogError("WebSocket URL is empty in auth token response");
                return false;
            }

            logger.LogInformation("Initializing GameLift Server SDK with WebSocket URL: {WebSocketUrl}", webSocketUrl);

            // Initialize GameLift Server SDK
            var serverParameters = new ServerParameters(
                webSocketUrl: webSocketUrl, // Use WebSocket URL from response
                processId: o.Anywhere.ProcessId,
                hostId: o.Anywhere.HostId,
                fleetId: o.Anywhere.FleetId,
                authToken: _currentAuthToken.Value.AuthToken
            );

            var initOutcome = GameLiftServerAPI.InitSDK(serverParameters);
            if (!initOutcome.Success)
            {
                logger.LogError("Failed to initialize GameLift Server SDK: {Error}", initOutcome.Error.ErrorMessage);
                return false;
            }

            // Register process ready
            var processParameters = new ProcessParameters(
                onStartGameSession: (gameSession) =>
                {
                    logger.LogInformation("Game session started: {GameSessionId}", gameSession.GameSessionId);
                    GameLiftServerAPI.ActivateGameSession();
                },
                onUpdateGameSession: (updateGameSession) =>
                {
                    logger.LogInformation("Game session updated: {GameSessionId}", updateGameSession.GameSession.GameSessionId);
                },
                onProcessTerminate: () =>
                {
                    logger.LogInformation("Process termination requested");
                    GameLiftServerAPI.ProcessEnding();
                },
                onHealthCheck: () => true,
                port: 5000, // HTTP/1 port for SignalR
                logParameters: new LogParameters([])
            );

            var processReadyOutcome = GameLiftServerAPI.ProcessReady(processParameters);
            if (!processReadyOutcome.Success)
            {
                logger.LogError("Failed to signal process ready: {Error}", processReadyOutcome.Error.ErrorMessage);
                return false;
            }

            logger.LogInformation("GameLift Server SDK initialized and process ready signaled");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize GameLift Server SDK");
            return false;
        }
    }

    private async Task ProcessEndingAsync()
    {
        try
        {
            // Notify GameLift that the process is ending
            var outcome = GameLiftServerAPI.ProcessEnding();
            if (!outcome.Success)
            {
                logger.LogWarning("Failed to notify GameLift of process ending: {Error}", outcome.Error.ErrorMessage);
            }
            else
            {
                logger.LogInformation("Successfully notified GameLift of process ending");
            }

            // Add small delay to ensure the message is sent
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during process ending notification");
        }
    }
}
