namespace Shared.Contracts;

/// <summary>
/// Represents battle group context information
/// </summary>
public interface IBattleGroupContext
{
    string Id { get; }
    string Name { get; }
    int MaxClients { get; }
    int ConnectedCount { get; }
    IReadOnlyList<string> ClientIds { get; }
}
