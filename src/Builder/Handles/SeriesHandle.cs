namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Type-safe handle to a computed series (indicator output, formula result, or base price).
/// </summary>
public readonly struct SeriesHandle : IEquatable<SeriesHandle>
{
    public SeriesHandle(int id)
    {
        Id = id;
    }

    public int Id { get; }

    public bool Equals(SeriesHandle other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is SeriesHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public override string ToString()
    {
        return $"Series:{Id}";
    }

    public static bool operator ==(SeriesHandle left, SeriesHandle right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SeriesHandle left, SeriesHandle right)
    {
        return !left.Equals(right);
    }
}
