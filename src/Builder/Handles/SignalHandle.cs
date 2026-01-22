namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Type-safe handle to a configured signal rule.
/// </summary>
public readonly struct SignalHandle : IEquatable<SignalHandle>
{
    public SignalHandle(int id)
    {
        Id = id;
    }

    public int Id { get; }

    public bool Equals(SignalHandle other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is SignalHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public override string ToString()
    {
        return $"Signal:{Id}";
    }

    public static bool operator ==(SignalHandle left, SignalHandle right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SignalHandle left, SignalHandle right)
    {
        return !left.Equals(right);
    }
}
