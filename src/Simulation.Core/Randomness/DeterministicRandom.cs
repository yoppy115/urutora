using System.Buffers.Binary;
using System.Text;

namespace Simulation.Core.Randomness;

public sealed class RandomStreamFactory
{
    public RandomStreamFactory(long runSeed)
    {
        RunSeed = runSeed;
    }

    public long RunSeed { get; }

    public DeterministicRandom Create(
        string subsystem,
        int tick,
        long entityId,
        string purpose,
        string scope = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subsystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        var key = FormattableString.Invariant($"v1|{RunSeed}|{subsystem}|{tick}|{entityId}|{purpose}|{scope}");
        return new DeterministicRandom(StableHash.Hash64(key));
    }

    public double StablePriority(
        string subsystem,
        int tick,
        long entityId,
        string purpose,
        string scope = "") => Create(subsystem, tick, entityId, purpose, scope).NextDouble();
}

public sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed)
    {
        _state = seed;
    }

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public double NextDouble()
    {
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    public double NextDouble(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Random range is invalid.");
        }

        return minimum + (maximum - minimum) * NextDouble();
    }

    public int NextInt(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        }

        var bound = (ulong)exclusiveMaximum;
        var threshold = unchecked((0UL - bound) % bound);
        while (true)
        {
            var value = NextUInt64();
            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }

    public double NextGaussian(double mean, double standardDeviation)
    {
        if (standardDeviation < 0 || !double.IsFinite(standardDeviation))
        {
            throw new ArgumentOutOfRangeException(nameof(standardDeviation));
        }

        var first = Math.Max(NextDouble(), double.Epsilon);
        var second = NextDouble();
        var standardNormal = Math.Sqrt(-2.0 * Math.Log(first)) * Math.Cos(2.0 * Math.PI * second);
        return mean + standardDeviation * standardNormal;
    }
}

public static class StableHash
{
    public static ulong Hash64(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = Encoding.UTF8.GetBytes(value);
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var item in bytes)
        {
            hash ^= item;
            hash *= prime;
        }

        var lengthBytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, bytes.LongLength);
        foreach (var item in lengthBytes)
        {
            hash ^= item;
            hash *= prime;
        }

        return hash;
    }

    public static string StableId(string prefix, params object?[] parts)
    {
        var payload = prefix + "|" + string.Join("|", parts.Select(item => item?.ToString() ?? "-"));
        return $"{prefix}-{Hash64(payload):x16}";
    }
}
