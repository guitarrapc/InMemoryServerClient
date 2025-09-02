using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using InMemoryServer.Configuration;
using Shared.ServiceDiscovery.Models;

namespace InMemoryServer.Services;

/// <summary>
/// Service Discovery client for BattleServer registration and health reporting
/// </summary>
public sealed class ServiceDiscoveryClient(ILogger<ServiceDiscoveryClient> logger, IOptions<BattleServerOptions> options, ConnectionManager connectionManager, IServiceProvider serviceProvider) : BackgroundService
{
    private HubConnection? _hubConnection;
    private bool _isRegistered;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting ServiceDiscoveryClient for server {ServerId}", BattleServerOptions.ServerId);

            // Initialize SignalR connection
            await InitializeConnectionAsync(cancellationToken);

            // Perform initial registration
            await RegisterServerAsync();

            await base.StartAsync(cancellationToken);
            logger.LogInformation("ServiceDiscoveryClient started successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start ServiceDiscoveryClient");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Stopping ServiceDiscoveryClient for server {ServerId}", BattleServerOptions.ServerId);

            // Unregister server
            if (_isRegistered && _hubConnection?.State == HubConnectionState.Connected)
            {
                await UnregisterServerAsync();
            }

            // Dispose connection
            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
            }

            await base.StopAsync(cancellationToken);
            logger.LogInformation("ServiceDiscoveryClient stopped successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping ServiceDiscoveryClient");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start heartbeat loop using PeriodicTimer
        var heartbeatInterval = TimeSpan.FromSeconds(options.Value.ServiceDiscovery.HeartbeatIntervalSeconds);
        using var timer = new PeriodicTimer(heartbeatInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SendHeartbeatAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            logger.LogInformation("ServiceDiscoveryClient heartbeat loop cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ServiceDiscoveryClient heartbeat loop");
        }
    }

    private async Task InitializeConnectionAsync(CancellationToken cancellationToken)
    {
        var serviceDiscoveryUrl = options.Value.ServiceDiscovery.SignalREndpoint;
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{serviceDiscoveryUrl}/discoveryHub")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.Reconnected += OnReconnected;
        _hubConnection.Closed += OnConnectionClosed;

        await _hubConnection.StartAsync(cancellationToken);
        logger.LogInformation("Connected to ServiceDiscovery at {Url}", serviceDiscoveryUrl);
    }

    private async Task RegisterServerAsync()
    {
        var registration = CreateServerRegistration();
        try
        {
            if (_hubConnection?.State != HubConnectionState.Connected)
            {
                logger.LogWarning("Cannot register server: ServiceDiscovery connection is not established");
                return;
            }

            var result = await _hubConnection.InvokeAsync<bool>("RegisterServerAsync", registration);

            if (result)
            {
                _isRegistered = true;
                logger.LogInformation("Successfully registered server {ServerId} with ServiceDiscovery", registration.ServerId);
            }
            else
            {
                logger.LogWarning("Failed to register server {ServerId} with ServiceDiscovery", registration.ServerId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering server {ServerId}", registration.ServerId);
        }
    }

    private async Task SendHeartbeatAsync()
    {
        var serverId = BattleServerOptions.ServerId;
        try
        {
            if (!_isRegistered || _hubConnection?.State != HubConnectionState.Connected)
            {
                return;
            }

            var status = CreateServerStatus();
            var result = await _hubConnection.InvokeAsync<bool>("UpdateServerStatusAsync", serverId, status);

            if (result)
            {
                logger.LogDebug("Heartbeat sent successfully for server {ServerId}", serverId);
            }
            else
            {
                logger.LogWarning("Failed to send heartbeat for server {ServerId}", serverId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending heartbeat for server {ServerId}", serverId);
        }
    }

    private async Task UnregisterServerAsync()
    {
        var serverId = BattleServerOptions.ServerId;
        try
        {
            await _hubConnection!.InvokeAsync("UnregisterServerAsync", serverId);
            _isRegistered = false;
            logger.LogInformation("Successfully unregistered server {ServerId} from ServiceDiscovery", serverId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unregistering server {ServerId}", serverId);
        }
    }

    private BattleServerRegistration CreateServerRegistration()
    {
        // Get server configuration
        var kestrelConfig = serviceProvider.GetRequiredService<IConfiguration>().GetSection("Kestrel");
        var http1Port = GetPortFromEndpoint(kestrelConfig, "Http1", 5000);
        var http2Port = GetPortFromEndpoint(kestrelConfig, "Http2", 5001);

        return new BattleServerRegistration
        {
            ServerId = BattleServerOptions.ServerId,
            Address = "localhost", // In production, this should be the actual server address
            SignalRPort = http1Port,
            MagicOnionPort = http2Port,
            MaxConcurrentSessions = options.Value.Server.MaxConcurrentSessions,
            SupportedModes = new List<string> { "Direct" },
            Metadata = new Dictionary<string, object>
            {
                { "Version", "1.0.0" },
                { "StartTime", DateTimeOffset.UtcNow }
            }
        };
    }

    private BattleServerStatus CreateServerStatus()
    {
        var activeConnections = connectionManager.GetTotalConnections();
        var maxSessions = options.Value.Server.MaxConcurrentSessions;

        return new BattleServerStatus
        {
            ServerId = BattleServerOptions.ServerId,
            Health = activeConnections < maxSessions ? ServerHealth.Healthy : ServerHealth.Degraded,
            ActiveSessions = activeConnections,
            MaxSessions = maxSessions,
            CpuUsage = 0.0, // TODO: Implement actual CPU usage monitoring
            MemoryUsage = 0.0, // TODO: Implement actual memory usage monitoring
            LastHeartbeat = DateTime.UtcNow
        };
    }

    private static int GetPortFromEndpoint(IConfigurationSection kestrelConfig, string endpointName, int defaultPort)
    {
        var endpointUrl = kestrelConfig[$"Endpoints:{endpointName}:Url"];
        if (string.IsNullOrEmpty(endpointUrl))
        {
            return defaultPort;
        }

        if (Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri))
        {
            return uri.Port;
        }

        return defaultPort;
    }

    private async Task OnReconnected(string? connectionId)
    {
        logger.LogInformation("Reconnected to ServiceDiscovery with connection ID: {ConnectionId}", connectionId);
        _isRegistered = false; // Force re-registration after reconnection
        await RegisterServerAsync();
    }

    private async Task OnConnectionClosed(Exception? exception)
    {
        if (exception is not null)
        {
            if (exception is OperationCanceledException)
            {
                // Normal shutdown
                logger.LogInformation("ServiceDiscovery connection closed due to shutdown");
            }
            else
            {
                logger.LogWarning(exception, "ServiceDiscovery connection closed unexpectedly");
            }
        }
        else
        {
            logger.LogInformation("ServiceDiscovery connection closed");
        }

        _isRegistered = false;
    }

    public override void Dispose()
    {
        _hubConnection?.DisposeAsync().AsTask().Wait();
        base.Dispose();
    }
}
