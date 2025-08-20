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

            // Cleanup compute instance if configured
            var o = options.Value;
            if (o.Anywhere.CleanupComputeOnShutdown && _currentCompute != null)
            {
                logger.LogInformation("Cleaning up compute instance on shutdown: {ComputeName}", _currentCompute.Value.ComputeName);
                await DeregisterComputeAsync(_currentCompute.Value.ComputeName, cancellationToken);
            }

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
            // Clean up old compute instances first
            await CleanupOldComputeInstancesAsync(cancellationToken);

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

    private async Task CleanupOldComputeInstancesAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        if (o.Anywhere.CleanupComputeOnStartup)
        {
            logger.LogInformation("Clean up compute instances on start up was disabled, clean up not executed.");
            return;
        }

        try
        {
            logger.LogInformation("Checking for old compute instances to cleanup in fleet: {FleetId}", o.Anywhere.FleetId);

            var listRequest = new ListComputeRequest
            {
                FleetId = o.Anywhere.FleetId
            };

            var listResponse = await gameLiftClient.ListComputeAsync(listRequest, cancellationToken);
            var computeList = listResponse.ComputeList;

            logger.LogInformation("Found {ComputeCount} compute instance(s) in fleet", computeList.Count);

            // Case 1: Multiple compute instances - cleanup all and start fresh
            if (computeList.Count > 1)
            {
                logger.LogWarning("Found {ComputeCount} compute instances (expected 1). Cleaning up all instances for localhost usage",
                    computeList.Count);

                foreach (var compute in computeList)
                {
                    await DeregisterComputeAsync(compute.ComputeName, cancellationToken);
                }
                return;
            }

            // Case 2: Single compute instance - check if it needs cleanup
            if (computeList.Count == 1)
            {
                var existingCompute = computeList[0];

                // Case 2a: Different compute name - remove and register new one
                if (existingCompute.ComputeName != o.Anywhere.ComputeName)
                {
                    logger.LogInformation("Found existing compute with different name: {ExistingName} (expected: {ExpectedName}). Cleaning up...",
                        existingCompute.ComputeName, o.Anywhere.ComputeName);

                    await DeregisterComputeAsync(existingCompute.ComputeName, cancellationToken);
                    return;
                }

                // Case 2b: Same compute name - check age
                var registrationTime = existingCompute.CreationTime ?? DateTime.UtcNow;
                var age = DateTime.UtcNow - registrationTime;
                var cleanupThreshold = o.Anywhere.ComputeCleanupThreshold;

                if (age > cleanupThreshold)
                {
                    logger.LogInformation("Found existing compute {ComputeName} registered {Age:hh\\:mm\\:ss} ago (threshold: {Threshold:hh\\:mm\\:ss}). Cleaning up...",
                        existingCompute.ComputeName, age, cleanupThreshold);                    await DeregisterComputeAsync(existingCompute.ComputeName, cancellationToken);
                    return;
                }

                logger.LogInformation("Existing compute {ComputeName} is recent (age: {Age:hh\\:mm\\:ss}). Reusing...",
                    existingCompute.ComputeName, age);
            }

            // Case 3: No compute instances - nothing to cleanup
            if (computeList.Count == 0)
            {
                logger.LogInformation("No existing compute instances found. Will register new compute");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during compute cleanup. Continuing with registration...");
        }
    }

    private async Task DeregisterComputeAsync(string computeName, CancellationToken cancellationToken)
    {
        var o = options.Value;
        try
        {
            logger.LogInformation("Deregistering compute: {ComputeName} from fleet: {FleetId}", computeName, o.Anywhere.FleetId);

            var deregisterRequest = new DeregisterComputeRequest
            {
                FleetId = o.Anywhere.FleetId,
                ComputeName = computeName
            };

            await gameLiftClient.DeregisterComputeAsync(deregisterRequest, cancellationToken);
            logger.LogInformation("Successfully deregistered compute: {ComputeName}", computeName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deregister compute: {ComputeName}", computeName);
        }
    }

    private async Task<ComputeInfo> RegisterComputeAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        try
        {
            // After cleanup, check if our compute still exists (reuse case)
            var listRequest = new ListComputeRequest
            {
                FleetId = o.Anywhere.FleetId
            };

            var listResponse = await gameLiftClient.ListComputeAsync(listRequest, cancellationToken);
            var existingCompute = listResponse.ComputeList
                .FirstOrDefault(c => c.ComputeName == o.Anywhere.ComputeName);

            if (existingCompute != null)
            {
                logger.LogInformation("Reusing existing compute: {ComputeName}", existingCompute.ComputeName);
                return new ComputeInfo(
                    existingCompute.ComputeName,
                    existingCompute.FleetId,
                    o.Anywhere.CustomLocation,
                    existingCompute.ComputeArn ?? string.Empty,
                    Enum.Parse<Shared.GameLift.ComputeStatus>(existingCompute.ComputeStatus.ToString(), true)
                );
            }

            // Register new compute
            logger.LogInformation("Registering new compute: {ComputeName} in fleet: {FleetId}", o.Anywhere.ComputeName, o.Anywhere.FleetId);

            var registerRequest = new RegisterComputeRequest
            {
                FleetId = o.Anywhere.FleetId,
                ComputeName = o.Anywhere.ComputeName,
                Location = o.Anywhere.CustomLocation,
                IpAddress = o.Anywhere.IpAddress,
            };

            var registerResponse = await gameLiftClient.RegisterComputeAsync(registerRequest, cancellationToken);

            logger.LogInformation("Successfully registered new compute: {ComputeName}", registerResponse.Compute.ComputeName);
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
            // see: https://github.com/search?q=repo%3Aaws-samples%2Famazon-gamelift-anywhere-sample%20api.amazongamelift.com&type=code
            var region = o.AWS.Region;
            var websocketUrl = $"wss://{region}.api.amazongamelift.com";

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
            logger.LogDebug("Server parameters - ProcessId: {ProcessId}, HostId: {HostId}, FleetId: {FleetId}", o.Anywhere.ProcessId, o.Anywhere.HostId, o.Anywhere.FleetId);
            logger.LogDebug("Auth Token length: {TokenLength} characters", _currentAuthToken.Value.AuthToken.Length);
            logger.LogDebug("Auth Token expiration: {ExpirationTime}", _currentAuthToken.Value.ExpirationTime);

            // Validate required parameters
            if (string.IsNullOrEmpty(o.Anywhere.ProcessId))
            {
                logger.LogError("ProcessId is empty");
                return false;
            }
            if (string.IsNullOrEmpty(o.Anywhere.HostId))
            {
                logger.LogError("HostId is empty");
                return false;
            }
            if (string.IsNullOrEmpty(o.Anywhere.FleetId))
            {
                logger.LogError("FleetId is empty");
                return false;
            }

            // Initialize GameLift Server SDK with timeout
            var serverParameters = new ServerParameters(
                webSocketUrl: webSocketUrl, // Use WebSocket URL from response
                processId: o.Anywhere.ProcessId,
                hostId: o.Anywhere.HostId,
                fleetId: o.Anywhere.FleetId,
                authToken: _currentAuthToken.Value.AuthToken
            );

            logger.LogInformation("Calling GameLiftServerAPI.InitSDK...");

            // Run InitSDK in a separate task to avoid potential deadlock
            var initTask = Task.Run(() => GameLiftServerAPI.InitSDK(serverParameters));

            // Wait for initialization with timeout
            if (await Task.WhenAny(initTask, Task.Delay(30000, cancellationToken)) == initTask)
            {
                var initOutcome = await initTask;
                logger.LogInformation("InitSDK completed with success: {Success}", initOutcome.Success);

                if (!initOutcome.Success)
                {
                    logger.LogError("Failed to initialize GameLift Server SDK: {Error}", initOutcome.Error?.ErrorMessage ?? "Unknown error");
                    return false;
                }
            }
            else
            {
                logger.LogError("GameLift Server SDK initialization timed out after 30 seconds");
                return false;
            }

            logger.LogInformation("GameLift Server SDK initialized successfully");

            // Register process ready
            logger.LogInformation("Registering process ready with GameLift...");
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
                onHealthCheck: () =>
                {
                    logger.LogDebug("Health check requested");
                    return true;
                },
                port: 5001, // HTTPS unified port for both SignalR (HTTP/1) and MagicOnion (HTTP/2)
                logParameters: new LogParameters([])
            );

            logger.LogInformation("Calling GameLiftServerAPI.ProcessReady...");

            // Run ProcessReady in a separate task with timeout
            var processReadyTask = Task.Run(() => GameLiftServerAPI.ProcessReady(processParameters));

            if (await Task.WhenAny(processReadyTask, Task.Delay(30000, cancellationToken)) == processReadyTask)
            {
                var processReadyOutcome = await processReadyTask;
                logger.LogInformation("ProcessReady completed with success: {Success}", processReadyOutcome.Success);

                if (!processReadyOutcome.Success)
                {
                    logger.LogError("Failed to signal process ready: {Error}", processReadyOutcome.Error?.ErrorMessage ?? "Unknown error");
                    return false;
                }
            }
            else
            {
                logger.LogError("GameLift ProcessReady timed out after 30 seconds");
                return false;
            }

            logger.LogInformation("GameLift Server SDK initialized and process ready signaled");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize GameLift Server SDK - Exception details: {ExceptionType}: {Message}",
                ex.GetType().Name, ex.Message);

            // Log additional details for common issues
            if (ex.Message.Contains("websocket") || ex.Message.Contains("WebSocket"))
            {
                logger.LogError("WebSocket connection issue detected. Check network connectivity and GameLift service availability.");
            }

            if (ex.Message.Contains("authentication") || ex.Message.Contains("auth"))
            {
                logger.LogError("Authentication issue detected. Verify AuthToken validity and expiration time.");
            }

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
