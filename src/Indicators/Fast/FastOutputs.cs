namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>
/// The series one kernel publishes. A slot is allocated when the kernel first asks for it, so a slot the
/// kernel declares as a flag column - zeros up to a bar and ones from it on - costs no allocation, no fill
/// and no validation: it is a shared read-only column.
/// </summary>
internal sealed class FastOutputs
{
    private readonly double[][] _slots;
    // One bit a slot; a slot past the mask is simply validated like a computed one.
    private ulong _shared;

    internal FastOutputs(int slots, int count)
    {
        _slots = new double[slots][];
        Count = count;
    }

    /// <summary>How many bars each series holds.</summary>
    internal int Count { get; }

    internal double[] this[int slot]
    {
        get
        {
            var values = _slots[slot];
            if (values is null)
            {
                values = FastMath.Allocate(Count);
                _slots[slot] = values;
            }
            return values;
        }
    }

    /// <summary>Publishes a slot as zero before <paramref name="first"/> and one from it on.</summary>
    internal void Flag(int slot, long first)
    {
        _slots[slot] = FastFlags.From(Count, first);
        if (slot < 64) _shared |= 1UL << slot;
    }

    /// <summary>Publishes a slot as the very series another slot holds, where the two are equal bar for bar.</summary>
    internal void Alias(int slot, int source)
    {
        _slots[slot] = this[source];
        if (slot < 64) _shared |= 1UL << slot;
    }

    /// <summary>Whether a slot is a shared flag column, which is finite by construction.</summary>
    internal bool IsShared(int slot) => slot < 64 && (_shared & (1UL << slot)) != 0;

    /// <summary>Every slot, for a kernel that fills them all itself.</summary>
    internal double[][] All()
    {
        for (var slot = 0; slot < _slots.Length; slot++) _ = this[slot];
        return _slots;
    }

    public static implicit operator double[][](FastOutputs outputs) => outputs.All();
}

/// <summary>
/// Shared flag columns. They are published read-only, so every indicator whose flag turns on at the same
/// bar of an equally long series can publish the same one.
/// </summary>
internal static class FastFlags
{
    // Longer columns are built per use: holding them would pin memory in proportion to the largest history.
    private const int LongestHeld = 1 << 16;
    private const int Entries = 32;
    private static readonly Entry?[] Held = new Entry?[Entries];

    private sealed class Entry(int first, double[] values)
    {
        internal readonly int First = first;
        internal readonly double[] Values = values;
    }

    internal static double[] From(int count, long first)
    {
        var start = (int)Math.Max(0, Math.Min(first, count));
        if (count == 0) return [];
        if (count > LongestHeld) return Build(count, start);
        var index = (int)((uint)(count * 31 + start) % Entries);
        var held = Volatile.Read(ref Held[index]);
        if (held is not null && held.Values.Length == count && held.First == start) return held.Values;
        var values = Build(count, start);
        Volatile.Write(ref Held[index], new Entry(start, values));
        return values;
    }

    /// <summary>Whether every held column is still the step it was built as; a kernel must never write one.</summary>
    internal static bool HeldAreIntact()
    {
        for (var index = 0; index < Entries; index++)
        {
            if (Volatile.Read(ref Held[index]) is not { } held) continue;
            for (var i = 0; i < held.Values.Length; i++)
                if (BitConverter.DoubleToInt64Bits(held.Values[i]) != BitConverter.DoubleToInt64Bits(i < held.First ? 0d : 1d))
                    return false;
        }
        return true;
    }

    private static double[] Build(int count, int start)
    {
        var values = new double[count];
        values.AsSpan(start).Fill(1);
        return values;
    }
}
