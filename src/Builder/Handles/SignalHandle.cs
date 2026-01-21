namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Type-safe handle for referencing a signal in the signal system.
/// </summary>
public readonly struct SignalHandle : IEquatable<SignalHandle>
{
    /// <summary>
    /// Creates a new signal handle with the specified ID.
    /// </summary>
    /// <param name="id">The unique identifier for this signal.</param>
    public SignalHandle(int id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the unique identifier for this signal.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Determines whether this handle equals another handle.
    /// </summary>
    public bool Equals(SignalHandle other)
    {
        return Id == other.Id;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is SignalHandle other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Id;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Signal:{Id}";
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(SignalHandle left, SignalHandle right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(SignalHandle left, SignalHandle right)
    {
        return !left.Equals(right);
    }
}
