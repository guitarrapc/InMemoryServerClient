using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace BattleLogic.Models;

/// <summary>
/// Provides deterministic random number generation for battle reproducibility.
/// This class ensures that battles can be reproduced exactly by using the same seed.
/// All methods are thread-safe and maintain deterministic behavior.
///
/// <para><strong>GUID Usage Strategy:</strong></para>
/// <para>This class implements a purpose-specific GUID generation strategy to balance
/// battle reproducibility with proper timestamp ordering for different use cases:</para>
///
/// <para><strong>1. Entity IDs (GUID v4 - Deterministic):</strong></para>
/// <list type="bullet">
/// <item><description>Use: <see cref="NextEntityId()"/> for all battle entities (players, enemies)</description></item>
/// <item><description>Format: GUID v4 with deterministic generation</description></item>
/// <item><description>Benefit: Ensures exact battle reproducibility with same seed</description></item>
/// <item><description>Constraint: Order-dependent - calling sequence must be consistent</description></item>
/// </list>
///
/// <para><strong>2. Timestamp IDs (GUID v7 - Time-ordered):</strong></para>
/// <list type="bullet">
/// <item><description>Use: <see cref="NewTimestampId()"/> for battle IDs, group IDs, log entries</description></item>
/// <item><description>Format: GUID v7 with millisecond-precision timestamp</description></item>
/// <item><description>Benefit: Natural chronological ordering, database performance optimization</description></item>
/// <item><description>Constraint: Non-deterministic - different each time (as intended)</description></item>
/// </list>
///
/// <para><strong>Implementation Guidelines:</strong></para>
/// <list type="bullet">
/// <item><description>Battle entities: Always use <see cref="NextEntityId()"/> for reproducibility</description></item>
/// <item><description>System events: Always use <see cref="NewTimestampId()"/> for chronological tracking</description></item>
/// <item><description>Never mix usage - entity IDs must remain deterministic</description></item>
/// <item><description>Maintain calling order consistency for <see cref="NextEntityId()"/> across battle phases</description></item>
/// </list>
/// </summary>
public sealed class BattleSeed
{
    private readonly Random _random;
    private readonly Lock _lock = new();
    private long _guidCounter;

    /// <summary>
    /// Gets the seed value used for this battle
    /// </summary>
    public int Seed { get; }

    /// <summary>
    /// Gets the battle ID used to generate this seed
    /// </summary>
    public string BattleId { get; }

    /// <summary>
    /// Initializes a new instance of BattleSeed using a battle ID as the seed source
    /// </summary>
    /// <param name="battleId">The battle ID to generate a deterministic seed from</param>
    public BattleSeed(string battleId)
    {
        if (string.IsNullOrEmpty(battleId))
            throw new ArgumentException("Battle ID cannot be null or empty", nameof(battleId));

        BattleId = battleId;
        Seed = GenerateSeedFromBattleId(battleId);
        _random = new Random(Seed);
        _guidCounter = 0;
    }

    /// <summary>
    /// Gets the Random instance associated with this seed
    /// </summary>
    public Random Random => _random;

    /// <summary>
    /// Generate a deterministic GUID v4 for battle entities.
    /// This ensures reproducible entity IDs for the same battle seed.
    /// Uses GUID v4 format which is designed for deterministic generation.
    ///
    /// THREAD SAFETY: This method is thread-safe and uses locks to ensure deterministic behavior.
    /// ORDER DEPENDENCY: The order of NextEntityId() calls affects the results.
    /// For reproducibility, ensure consistent calling patterns.
    /// </summary>
    /// <returns>A deterministic GUID v4 that will be the same for the same seed and call order</returns>
    public Guid NextEntityId()
    {
        lock (_lock)
        {
            // Use thread-safe atomic increment for counter
            Interlocked.Increment(ref _guidCounter);

            // Generate deterministic random bytes using our seeded Random
            var bytes = new byte[16];
            _random.NextBytes(bytes);

            // Set version to 4 and variant bits according to RFC 4122
            // Note: .NET GUID constructor handles byte order conversion, so we need to
            // set the version in the correct position considering endianness
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40); // Version 4 in correct position
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // Variant bits

            return new Guid(bytes);
        }
    }

    /// <summary>
    /// Generate GUID v7 with current timestamp for logs, groups, and other time-sensitive data.
    /// This provides natural time-ordering capabilities.
    /// </summary>
    /// <returns>A GUID v7 with current timestamp</returns>
    public static Guid NewTimestampId() => Guid.CreateVersion7();

    /// <summary>
    /// Generate a cryptographically secure random seed that avoids collisions
    /// even when multiple servers start simultaneously
    /// </summary>
    private static int GenerateRandomSeed()
    {
        // Combine multiple entropy sources to avoid collisions
        using var rng = RandomNumberGenerator.Create();

        // Get 4 bytes of cryptographically secure random data
        Span<byte> cryptoBytes = stackalloc byte[4];
        rng.GetBytes(cryptoBytes);
        var cryptoPart = BitConverter.ToInt32(cryptoBytes);

        // Add high-resolution timestamp
        var timestampPart = (int)(DateTime.UtcNow.Ticks & 0xFFFFFFFF);

        // Add process and thread identifiers
        var processPart = Environment.ProcessId;
        var threadPart = Thread.CurrentThread.ManagedThreadId;

        // Add machine-specific identifier
        var machinePart = Environment.MachineName.GetHashCode();

        // Combine all entropy sources using XOR
        var combinedSeed = cryptoPart ^ timestampPart ^ processPart ^ threadPart ^ machinePart;

        // Ensure we don't return 0 (which could cause issues with some Random implementations)
        return combinedSeed == 0 ? 1 : combinedSeed;
    }

    /// <summary>
    /// Generate a deterministic seed from a battle ID using a consistent hash algorithm
    /// </summary>
    /// <param name="battleId">The battle ID to generate seed from</param>
    /// <returns>A deterministic 32-bit integer seed</returns>
    private static int GenerateSeedFromBattleId(string battleId)
    {
        // Use a simple but effective hash algorithm that's consistent across platforms
        unchecked
        {
            var hash = 17;
            foreach (var c in battleId)
            {
                hash = hash * 31 + c;
            }

            // Ensure we don't return 0 (which could cause issues with some Random implementations)
            return hash == 0 ? 1 : hash;
        }
    }

    /// <summary>
    /// Gets the timestamp from a GUID v7.
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    public static DateTimeOffset GetTimestamp(in Guid guid)
    {
        // not considering endianness here
        ref var p = ref Unsafe.As<Guid, byte>(ref Unsafe.AsRef(in guid));
        var lower = Unsafe.ReadUnaligned<uint>(ref p);
        var upper = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref p, 4));
        var time = (long)upper + (((long)lower) << 16);
        return DateTimeOffset.FromUnixTimeMilliseconds(time);
    }

    /// <summary>
    /// Returns a string representation of this BattleSeed
    /// </summary>
    public override string ToString() => $"BattleSeed(BattleId={BattleId}, Seed={Seed}, GuidCounter={Interlocked.Read(ref _guidCounter)})";
}
