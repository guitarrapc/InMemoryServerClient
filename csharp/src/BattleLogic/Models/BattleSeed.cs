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
    /// Gets the original user-provided seed value
    /// </summary>
    public int UserSeed { get; }

    /// <summary>
    /// Gets the BattleId used for this battle
    /// </summary>
    public Guid BattleId { get; }

    /// <summary>
    /// Gets the final deterministic seed value (combination of BattleId + UserSeed)
    /// </summary>
    public int DeterministicSeed { get; }

    /// <summary>
    /// Initializes a new instance of BattleSeed using BattleId and user seed.
    /// The final deterministic seed is generated from the combination of both values,
    /// ensuring that same BattleId + same seed = same result, but different BattleId + same seed = different result.
    /// </summary>
    /// <param name="battleId">The battle ID for this battle</param>
    /// <param name="userSeed">The user-provided seed value</param>
    public BattleSeed(Guid battleId, int userSeed)
    {
        BattleId = battleId;
        UserSeed = userSeed;
        DeterministicSeed = CreateCombinedSeed(battleId, userSeed);
        _random = new Random(DeterministicSeed);
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
    /// Generate a cryptographically secure seed value that is unpredictable from battle ID
    /// </summary>
    /// <returns>A 32-bit seed value derived from cryptographically secure random bytes</returns>
    public static int GenerateSecureSeed()
    {
        using var rng = RandomNumberGenerator.Create();
        Span<byte> bytes = stackalloc byte[4];
        rng.GetBytes(bytes);
        var seed = BitConverter.ToInt32(bytes);

        // Ensure we don't return 0 (which could cause issues with some Random implementations)
        return seed == 0 ? 1 : seed;
    }

    /// <summary>
    /// Generate a battle ID that is completely independent from the seed value
    /// Uses timestamp-based GUID v7 for proper ordering and uniqueness
    /// </summary>
    /// <returns>A GUID v7 for battle identification</returns>
    public static Guid GenerateBattleId()
    {
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Creates a deterministic seed by combining BattleId and user seed.
    /// This ensures that the same BattleId + same user seed always produces the same result,
    /// but different BattleId + same user seed produces different results.
    /// </summary>
    /// <param name="battleId">The battle ID</param>
    /// <param name="userSeed">The user-provided seed</param>
    /// <returns>Combined deterministic seed value</returns>
    public static int CreateCombinedSeed(Guid battleId, int userSeed)
    {
        using var sha256 = SHA256.Create();

        // Combine battleId bytes and userSeed bytes
        var battleIdBytes = battleId.ToByteArray();
        var userSeedBytes = BitConverter.GetBytes(userSeed);
        var combinedBytes = new byte[battleIdBytes.Length + userSeedBytes.Length];

        Array.Copy(battleIdBytes, 0, combinedBytes, 0, battleIdBytes.Length);
        Array.Copy(userSeedBytes, 0, combinedBytes, battleIdBytes.Length, userSeedBytes.Length);

        var hash = sha256.ComputeHash(combinedBytes);
        var seed = BitConverter.ToInt32(hash, 0);

        // Ensure we don't return 0 (which could cause issues with some Random implementations)
        return seed == 0 ? 1 : seed;
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
    public override string ToString() => $"BattleSeed(BattleId={BattleId}, UserSeed={UserSeed}, DeterministicSeed={DeterministicSeed}, GuidCounter={Interlocked.Read(ref _guidCounter)})";
}
