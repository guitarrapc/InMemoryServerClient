using Shared.BattleServer.Constants;
using Shared.BattleServer.Models;
using System.Threading.Channels;

namespace InMemoryServer.Services;

/// <summary>
/// Actor-based Group Manager for thread-safe group operations
/// </summary>
public class GroupManagerActor : IDisposable
{
    private readonly ILogger<GroupManagerActor> _logger;
    private readonly ConnectionManager _connectionManager;
    private readonly Channel<GroupOperation> _operationChannel;
    private readonly ChannelWriter<GroupOperation> _writer;
    private readonly ChannelReader<GroupOperation> _reader;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _processingTask;

    // Single-threaded access to these collections (only accessed by the actor)
    private readonly Dictionary<string, BattleGroupContext> _groups = new();
    private readonly Dictionary<string, string> _connectionToGroup = new();
    private readonly Dictionary<string, string> _groupNameToId = new();

    public GroupManagerActor(ILogger<GroupManagerActor> logger, ConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;
        _cancellationTokenSource = new CancellationTokenSource();

        // Create unbounded channel for operations
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _operationChannel = Channel.CreateUnbounded<GroupOperation>(options);
        _writer = _operationChannel.Writer;
        _reader = _operationChannel.Reader;

        // Start the actor processing loop
        _processingTask = ProcessOperationsAsync(_cancellationTokenSource.Token);

        // Start cleanup timer
        StartCleanupTimer();
    }

    /// <summary>
    /// Join a group asynchronously
    /// </summary>
    public async Task<BattleGroupContext> JoinGroupAsync(string connectionId, string? groupName = null)
    {
        var tcs = new TaskCompletionSource<BattleGroupContext>();
        var operation = new JoinGroupOperation(connectionId, groupName, tcs);

        if (!_writer.TryWrite(operation))
        {
            throw new InvalidOperationException("Group manager is shutting down");
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Leave a group asynchronously
    /// </summary>
    public async Task<(BattleGroupContext? group, int newCount)> LeaveGroupAsync(string connectionId)
    {
        var tcs = new TaskCompletionSource<(BattleGroupContext?, int)>();
        var operation = new LeaveGroupOperation(connectionId, tcs);

        if (!_writer.TryWrite(operation))
        {
            throw new InvalidOperationException("Group manager is shutting down");
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Get all groups (read-only snapshot)
    /// </summary>
    public async Task<IEnumerable<BattleGroupContext>> GetAllGroupsAsync()
    {
        var tcs = new TaskCompletionSource<IEnumerable<BattleGroupContext>>();
        var operation = new GetAllGroupsOperation(tcs);

        if (!_writer.TryWrite(operation))
        {
            throw new InvalidOperationException("Group manager is shutting down");
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Get group info by ID
    /// </summary>
    public async Task<BattleGroupContext?> GetGroupInfoAsync(string groupId)
    {
        var tcs = new TaskCompletionSource<BattleGroupContext?>();
        var operation = new GetGroupInfoOperation(groupId, tcs);

        if (!_writer.TryWrite(operation))
        {
            throw new InvalidOperationException("Group manager is shutting down");
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Get group ID for a connection
    /// </summary>
    public async Task<string?> GetGroupIdForConnectionAsync(string connectionId)
    {
        var tcs = new TaskCompletionSource<string?>();
        var operation = new GetGroupIdForConnectionOperation(connectionId, tcs);

        if (!_writer.TryWrite(operation))
        {
            throw new InvalidOperationException("Group manager is shutting down");
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Extend group waiting time if possible
    /// </summary>
    public async Task<bool> ExtendGroupWaitingTimeAsync(string groupId)
    {
        var tcs = new TaskCompletionSource<bool>();
        var operation = new ExtendGroupWaitingTimeOperation(groupId, tcs);

        if (!_writer.TryWrite(operation))
        {
            throw new InvalidOperationException("Group manager is shutting down");
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Dissolve a group and notify all members
    /// </summary>
    public async Task<List<string>> DissolveGroupAsync(string groupId, string reason = "Group disbanded due to timeout")
    {
        var tcs = new TaskCompletionSource<List<string>>();
        var operation = new DissolveGroupOperation(groupId, reason, tcs);

        if (!_writer.TryWrite(operation))
        {
            throw new InvalidOperationException("Group manager is shutting down");
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Main actor processing loop
    /// </summary>
    private async Task ProcessOperationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var operation in _reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessOperationAsync(operation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing group operation: {OperationType}", operation.GetType().Name);
                    operation.SetException(ex);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in group manager actor");
        }
    }

    /// <summary>
    /// Process individual operations
    /// </summary>
    private async Task ProcessOperationAsync(GroupOperation operation)
    {
        switch (operation)
        {
            case JoinGroupOperation joinOp:
                await ProcessJoinGroupAsync(joinOp);
                break;

            case LeaveGroupOperation leaveOp:
                await ProcessLeaveGroupAsync(leaveOp);
                break;

            case GetAllGroupsOperation getAllOp:
                ProcessGetAllGroups(getAllOp);
                break;

            case GetGroupInfoOperation getGroupInfoOp:
                ProcessGetGroupInfo(getGroupInfoOp);
                break;

            case GetGroupIdForConnectionOperation getGroupIdOp:
                ProcessGetGroupIdForConnection(getGroupIdOp);
                break;

            case ExtendGroupWaitingTimeOperation extendOp:
                ProcessExtendGroupWaitingTime(extendOp);
                break;

            case DissolveGroupOperation dissolveOp:
                await ProcessDissolveGroupAsync(dissolveOp);
                break;

            case CleanupOperation cleanupOp:
                ProcessCleanup(cleanupOp);
                break;

            default:
                throw new NotSupportedException($"Operation type {operation.GetType().Name} is not supported");
        }
    }

    private async Task ProcessJoinGroupAsync(JoinGroupOperation operation)
    {
        var connectionId = operation.ConnectionId;
        var groupName = operation.GroupName;

        try
        {
            // Validate connection is still active
            if (!await _connectionManager.IsConnectionActiveAsync(connectionId))
            {
                operation.SetException(new InvalidOperationException($"Connection {connectionId} is no longer active"));
                return;
            }

            BattleGroupContext targetGroup;

            // If group name is specified, try to join that group
            if (!string.IsNullOrEmpty(groupName))
            {
                if (_groupNameToId.TryGetValue(groupName, out var existingGroupId) &&
                    _groups.TryGetValue(existingGroupId, out var existingGroup))
                {
                    if (!existingGroup.IsFull() && string.IsNullOrEmpty(existingGroup.BattleId))
                    {
                        targetGroup = existingGroup;
                    }
                    else
                    {
                        // Create new group with incremented name
                        targetGroup = CreateNewGroup(connectionId, GenerateUniqueGroupName(groupName));
                    }
                }
                else
                {
                    targetGroup = CreateNewGroup(connectionId, groupName);
                }
            }
            else
            {
                // Find available group or create new one
                var availableGroup = _groups.Values
                    .Where(g => !g.IsFull() && string.IsNullOrEmpty(g.BattleId))
                    .OrderByDescending(g => g.ConnectionCount)
                    .FirstOrDefault();

                if (availableGroup != null)
                {
                    targetGroup = availableGroup;
                }
                else
                {
                    var newGroupId = Guid.CreateVersion7().ToString();
                    targetGroup = CreateNewGroup(connectionId, $"Group-{newGroupId[..8]}");
                }
            }

            // Add connection to group
            AddConnectionToGroup(connectionId, targetGroup);

            _logger.LogDebug($"Connection {connectionId} joined group {targetGroup.Name} (ID: {targetGroup.GroupId})");

            operation.SetResult(targetGroup);
        }
        catch (Exception ex)
        {
            operation.SetException(ex);
        }
    }

    private async Task ProcessLeaveGroupAsync(LeaveGroupOperation operation)
    {
        var connectionId = operation.ConnectionId;

        try
        {
            if (_connectionToGroup.TryGetValue(connectionId, out var groupId) &&
                _groups.TryGetValue(groupId, out var group))
            {
                RemoveConnectionFromGroup(connectionId, group);

                var newCount = group.ConnectionCount;
                _logger.LogDebug($"Connection {connectionId} left group {group.Name}. New count: {newCount}");

                // Remove group if empty
                if (newCount <= 0)
                {
                    RemoveGroup(group);
                    operation.SetResult((null, 0));
                }
                else
                {
                    operation.SetResult((group, newCount));
                }
            }
            else
            {
                operation.SetResult((null, 0));
            }
        }
        catch (Exception ex)
        {
            operation.SetException(ex);
        }
    }

    private void ProcessGetAllGroups(GetAllGroupsOperation operation)
    {
        try
        {
            // Return a deep copy to avoid external mutations
            var snapshot = _groups.Values.Select(g => new BattleGroupContext
            {
                GroupId = g.GroupId,
                Name = g.Name,
                ConnectionCount = g.ConnectionCount,
                MaxConnections = g.MaxConnections,
                CreatedAt = g.CreatedAt,
                ExpiresAt = g.ExpiresAt,
                BattleId = g.BattleId,
                ExtensionCount = g.ExtensionCount,
                LastExtendedAt = g.LastExtendedAt,
                ClientIds = new List<string>(g.ClientIds) // Copy the client IDs
            }).ToList();

            operation.SetResult(snapshot);
        }
        catch (Exception ex)
        {
            operation.SetException(ex);
        }
    }

    private void ProcessGetGroupInfo(GetGroupInfoOperation operation)
    {
        try
        {
            if (_groups.TryGetValue(operation.GroupId, out var group))
            {
                var snapshot = new BattleGroupContext
                {
                    GroupId = group.GroupId,
                    Name = group.Name,
                    ConnectionCount = group.ConnectionCount,
                    MaxConnections = group.MaxConnections,
                    CreatedAt = group.CreatedAt,
                    ExpiresAt = group.ExpiresAt,
                    BattleId = group.BattleId,
                    ExtensionCount = group.ExtensionCount,
                    LastExtendedAt = group.LastExtendedAt,
                    ClientIds = new List<string>(group.ClientIds) // Copy the client IDs
                };
                operation.SetResult(snapshot);
            }
            else
            {
                operation.SetResult(null);
            }
        }
        catch (Exception ex)
        {
            operation.SetException(ex);
        }
    }

    private void ProcessGetGroupIdForConnection(GetGroupIdForConnectionOperation operation)
    {
        try
        {
            var groupId = _connectionToGroup.TryGetValue(operation.ConnectionId, out var id) ? id : null;
            operation.SetResult(groupId);
        }
        catch (Exception ex)
        {
            operation.SetException(ex);
        }
    }

    private void ProcessExtendGroupWaitingTime(ExtendGroupWaitingTimeOperation operation)
    {
        try
        {
            if (!_groups.TryGetValue(operation.GroupId, out var group))
            {
                operation.SetResult(false);
                return;
            }

            // Check if extension is allowed
            if (group.ExtensionCount >= SystemDefines.MaxGroupExtensions)
            {
                _logger.LogWarning($"Group {group.Name} (ID: {operation.GroupId}) has reached maximum extensions ({SystemDefines.MaxGroupExtensions})");
                operation.SetResult(false);
                return;
            }

            // Extend the group
            group.ExtensionCount++;
            group.LastExtendedAt = DateTime.UtcNow;
            group.ExpiresAt = group.ExpiresAt.AddMinutes(SystemDefines.GroupExtensionMinutes);

            _logger.LogDebug($"Extended group {group.Name} (ID: {operation.GroupId}) for {SystemDefines.GroupExtensionMinutes} minutes. Extension count: {group.ExtensionCount}/{SystemDefines.MaxGroupExtensions}");
            operation.SetResult(true);
        }
        catch (Exception ex)
        {
            operation.SetException(ex);
        }
    }

    private void ProcessCleanup(CleanupOperation operation)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Handle empty expired groups
            var expiredEmptyGroups = _groups.Values.Where(g => g.ExpiresAt < now && g.ConnectionCount == 0).ToList();
            foreach (var group in expiredEmptyGroups)
            {
                RemoveGroup(group);
                _logger.LogDebug($"Removed expired empty group {group.Name} (ID: {group.GroupId})");
            }

            // Handle groups that need attention (extension or dissolution)
            var groupsNeedingAttention = _groups.Values.Where(g =>
                g.ConnectionCount > 0 &&
                !g.IsFull() &&
                string.IsNullOrEmpty(g.BattleId) &&
                g.ExpiresAt <= now.AddMinutes(1) // Groups expiring within 1 minute
            ).ToList();

            foreach (var group in groupsNeedingAttention)
            {
                if (group.ExpiresAt <= now)
                {
                    // Group has expired, attempt extension or dissolve
                    if (group.ExtensionCount < SystemDefines.MaxGroupExtensions)
                    {
                        // Extend the group
                        group.ExtensionCount++;
                        group.LastExtendedAt = DateTime.UtcNow;
                        group.ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExtensionMinutes);
                        _logger.LogDebug($"Auto-extended group {group.Name} (ID: {group.GroupId}) due to timeout. Members: {group.ConnectionCount}/{group.MaxConnections}");
                    }
                    else
                    {
                        // Dissolve the group - maximum extensions reached
                        var clientIds = new List<string>(group.ClientIds);
                        RemoveGroup(group);
                        _logger.LogWarning($"Auto-dissolved group {group.Name} (ID: {group.GroupId}) - maximum extensions reached. Affected clients: {clientIds.Count}");

                        // Fire the dissolution event
                        OnGroupDissolved?.Invoke(group.GroupId, group.Name, clientIds, "Maximum extensions reached");
                    }
                }
            }

            operation.SetResult(null);
        }
        catch (Exception ex)
        {
            operation.SetException(ex);
        }
    }

    private async Task ProcessDissolveGroupAsync(DissolveGroupOperation operation)
    {
        try
        {
            if (_groups.TryGetValue(operation.GroupId, out var group))
            {
                var clientIds = new List<string>(group.ClientIds);

                // Fire group dissolved event with the provided reason
                OnGroupDissolved?.Invoke(group.GroupId, group.Name, clientIds, operation.Reason);

                RemoveGroupInternal(group);

                _logger.LogDebug($"Dissolved group {group.Name} (ID: {operation.GroupId}). Reason: {operation.Reason}");
                operation.SetResult(clientIds);
            }
            else
            {
                operation.SetResult(new List<string>());
            }
        }
        catch (Exception ex)
        {
            operation.SetException(ex);
        }
    }

    private BattleGroupContext CreateNewGroup(string connectionId, string groupName)
    {
        var groupId = Guid.CreateVersion7().ToString();
        var group = new BattleGroupContext
        {
            GroupId = groupId,
            Name = groupName,
            ConnectionCount = 0, // Will be incremented when adding connection
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes),
            ClientIds = new List<string>()
        };

        _groups[groupId] = group;
        _groupNameToId[groupName] = groupId;

        return group;
    }

    private void AddConnectionToGroup(string connectionId, BattleGroupContext group)
    {
        group.IncrementConnectionCount();
        group.ClientIds.Add(connectionId);
        _connectionToGroup[connectionId] = group.GroupId;

        if (group.IsFull() && string.IsNullOrEmpty(group.BattleId))
        {
            _logger.LogDebug($"Group {group.Name} is now full and ready for battle!");
        }
    }

    private void RemoveConnectionFromGroup(string connectionId, BattleGroupContext group)
    {
        group.DecrementConnectionCount();
        group.ClientIds.Remove(connectionId);
        _connectionToGroup.Remove(connectionId);
    }

    private void RemoveGroup(BattleGroupContext group)
    {
        // Get a copy of client IDs before clearing
        var clientIds = new List<string>(group.ClientIds);

        RemoveGroupInternal(group);

        // Fire group dissolved event
        OnGroupDissolved?.Invoke(group.GroupId, group.Name, clientIds, "Group removed");
    }

    private void RemoveGroupInternal(BattleGroupContext group)
    {
        _groups.Remove(group.GroupId);
        _groupNameToId.Remove(group.Name);

        // Remove all remaining connections
        foreach (var clientId in group.ClientIds)
        {
            _connectionToGroup.Remove(clientId);
        }

        _logger.LogDebug($"Removed group {group.Name} (ID: {group.GroupId})");
    }

    private string GenerateUniqueGroupName(string baseName)
    {
        var counter = 1;
        string candidateName;

        do
        {
            candidateName = $"{baseName}-{counter}";
            counter++;
        } while (_groupNameToId.ContainsKey(candidateName));

        return candidateName;
    }

    private void StartCleanupTimer()
    {
        Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), _cancellationTokenSource.Token);

                var cleanupOp = new CleanupOperation();
                if (_writer.TryWrite(cleanupOp))
                {
                    await cleanupOp.Task;
                }
            }
        }, _cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        try
        {
            _writer.Complete();
        }
        catch (InvalidOperationException)
        {
            // Channel is already closed, which is fine
        }

        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource is already disposed, which is fine
        }

        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during group manager shutdown");
        }

        _cancellationTokenSource.Dispose();
    }

    /// <summary>
    /// Event fired when a group is dissolved
    /// </summary>
    public event Action<string, string, List<string>, string>? OnGroupDissolved;
}

// Operation base classes and implementations
public abstract class GroupOperation
{
    protected TaskCompletionSource<object?> _tcs = new();

    public Task Task => _tcs.Task;

    public void SetException(Exception ex)
    {
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.SetException(ex);
        }
    }

    public void SetResult(object? result)
    {
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.SetResult(result);
        }
    }
}

public class JoinGroupOperation : GroupOperation
{
    private readonly TaskCompletionSource<BattleGroupContext> _typedTcs = new();

    public string ConnectionId { get; }
    public string? GroupName { get; }

    public JoinGroupOperation(string connectionId, string? groupName, TaskCompletionSource<BattleGroupContext> tcs)
    {
        ConnectionId = connectionId;
        GroupName = groupName;
        _typedTcs = tcs;
    }

    public void SetResult(BattleGroupContext result) => _typedTcs.SetResult(result);
    public new void SetException(Exception ex) => _typedTcs.SetException(ex);
    public new Task<BattleGroupContext> Task => _typedTcs.Task;
}

public class LeaveGroupOperation : GroupOperation
{
    private readonly TaskCompletionSource<(BattleGroupContext?, int)> _typedTcs = new();

    public string ConnectionId { get; }

    public LeaveGroupOperation(string connectionId, TaskCompletionSource<(BattleGroupContext?, int)> tcs)
    {
        ConnectionId = connectionId;
        _typedTcs = tcs;
    }

    public void SetResult((BattleGroupContext?, int) result) => _typedTcs.SetResult(result);
    public new void SetException(Exception ex) => _typedTcs.SetException(ex);
    public new Task<(BattleGroupContext?, int)> Task => _typedTcs.Task;
}

public class GetAllGroupsOperation : GroupOperation
{
    private readonly TaskCompletionSource<IEnumerable<BattleGroupContext>> _typedTcs = new();

    public GetAllGroupsOperation(TaskCompletionSource<IEnumerable<BattleGroupContext>> tcs)
    {
        _typedTcs = tcs;
    }

    public void SetResult(IEnumerable<BattleGroupContext> result) => _typedTcs.SetResult(result);
    public new void SetException(Exception ex) => _typedTcs.SetException(ex);
    public new Task<IEnumerable<BattleGroupContext>> Task => _typedTcs.Task;
}

public class DissolveGroupOperation : GroupOperation
{
    private readonly TaskCompletionSource<List<string>> _typedTcs = new();

    public string GroupId { get; }
    public string Reason { get; }

    public DissolveGroupOperation(string groupId, string reason, TaskCompletionSource<List<string>> tcs)
    {
        GroupId = groupId;
        Reason = reason;
        _typedTcs = tcs;
    }

    public void SetResult(List<string> result) => _typedTcs.SetResult(result);
    public new void SetException(Exception ex) => _typedTcs.SetException(ex);
    public new Task<List<string>> Task => _typedTcs.Task;
}

public class CleanupOperation : GroupOperation
{
    public CleanupOperation()
    {
        SetResult(null);
    }
}

public class GetGroupInfoOperation : GroupOperation
{
    private readonly TaskCompletionSource<BattleGroupContext?> _typedTcs = new();

    public string GroupId { get; }

    public GetGroupInfoOperation(string groupId, TaskCompletionSource<BattleGroupContext?> tcs)
    {
        GroupId = groupId;
        _typedTcs = tcs;
    }

    public void SetResult(BattleGroupContext? result) => _typedTcs.SetResult(result);
    public new void SetException(Exception ex) => _typedTcs.SetException(ex);
    public new Task<BattleGroupContext?> Task => _typedTcs.Task;
}

public class GetGroupIdForConnectionOperation : GroupOperation
{
    private readonly TaskCompletionSource<string?> _typedTcs = new();

    public string ConnectionId { get; }

    public GetGroupIdForConnectionOperation(string connectionId, TaskCompletionSource<string?> tcs)
    {
        ConnectionId = connectionId;
        _typedTcs = tcs;
    }

    public void SetResult(string? result) => _typedTcs.SetResult(result);
    public new void SetException(Exception ex) => _typedTcs.SetException(ex);
    public new Task<string?> Task => _typedTcs.Task;
}

public class ExtendGroupWaitingTimeOperation : GroupOperation
{
    private readonly TaskCompletionSource<bool> _typedTcs = new();

    public string GroupId { get; }

    public ExtendGroupWaitingTimeOperation(string groupId, TaskCompletionSource<bool> tcs)
    {
        GroupId = groupId;
        _typedTcs = tcs;
    }

    public void SetResult(bool result) => _typedTcs.SetResult(result);
    public new void SetException(Exception ex) => _typedTcs.SetException(ex);
    public new Task<bool> Task => _typedTcs.Task;
}
