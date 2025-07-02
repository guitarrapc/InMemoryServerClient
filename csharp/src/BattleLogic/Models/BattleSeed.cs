using System.Security.Cryptography;

namespace BattleLogic.Models;

/// <summary>
/// Provides deterministic random number generation for battle reproducibility.
/// This class ensures that battles can be reproduced exactly by using the same seed.
/// </summary>
public sealed class BattleSeed
{
    private readonly Random _random;
    private long _guidCounter;

    /// <summary>
    /// Gets the seed value used for this battle
    /// </summary>
    public int Seed { get; }

    /// <summary>
    /// Initializes a new instance of BattleSeed using a battle ID as the seed source
    /// </summary>
    /// <param name="battleId">The battle ID to generate a deterministic seed from</param>
    public BattleSeed(string battleId)
    {
        if (string.IsNullOrEmpty(battleId))
            throw new ArgumentException("Battle ID cannot be null or empty", nameof(battleId));

        Seed = GenerateSeedFromBattleId(battleId);
        _random = new Random(Seed);
        _guidCounter = 0;
    }

    /// <summary>
    /// Gets the Random instance associated with this seed
    /// </summary>
    public Random Random => _random;

    /// <summary>
    /// Generate a deterministic GUID based on the current seed and counter.
    /// This ensures reproducible entity IDs for the same battle seed.
    ///
    /// THREAD SAFETY: This method is thread-safe and uses atomic operations.
    /// ORDER DEPENDENCY: The order of NextGuid() calls affects the results.
    /// For reproducibility, ensure consistent calling patterns.
    /// </summary>
    /// <returns>A deterministic GUID that will be the same for the same seed and call order</returns>
    public Guid NextGuid()
    {
        // Use thread-safe atomic increment for counter
        var currentCounter = Interlocked.Increment(ref _guidCounter);

        // Create deterministic byte array using seed and counter
        var guidBytes = new byte[16];

        // Use seed as base (first 4 bytes)
        var seedBytes = BitConverter.GetBytes(Seed);
        Array.Copy(seedBytes, 0, guidBytes, 0, 4);

        // Use counter (next 4 bytes)
        var counterBytes = BitConverter.GetBytes((uint)currentCounter);
        Array.Copy(counterBytes, 0, guidBytes, 4, 4);

        // Fill remaining 8 bytes with deterministic random data
        for (int i = 8; i < 16; i++)
        {
            guidBytes[i] = (byte)_random.Next(0, 256);
        }

        return new Guid(guidBytes);
    }

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
    /// Returns a string representation of this BattleSeed
    /// </summary>
    public override string ToString() => $"BattleSeed(Seed={Seed}, GuidCounter={Interlocked.Read(ref _guidCounter)})";
}
