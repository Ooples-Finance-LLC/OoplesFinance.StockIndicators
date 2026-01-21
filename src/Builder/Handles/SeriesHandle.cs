namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Type-safe handle for referencing a data series in the computation graph.
/// </summary>
public readonly struct SeriesHandle : IEquatable<SeriesHandle>
{
    /// <summary>
    /// Creates a new series handle with the specified ID.
    /// </summary>
    /// <param name="id">The unique identifier for this series.</param>
    public SeriesHandle(int id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the unique identifier for this series.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Determines whether this handle equals another handle.
    /// </summary>
    public bool Equals(SeriesHandle other)
    {
        return Id == other.Id;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is SeriesHandle other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Id;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Series:{Id}";
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(SeriesHandle left, SeriesHandle right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(SeriesHandle left, SeriesHandle right)
    {
        return !left.Equals(right);
    }
}
