namespace OoplesFinance.StockIndicators.Streaming;

public enum BarTimeframeUnit
{
    Tick,
    Second,
    Minute,
    Hour,
    Day
}

public sealed class BarTimeframe : IEquatable<BarTimeframe>
{
    public BarTimeframe(int amount, BarTimeframeUnit unit)
    {
        if (amount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be at least 1.");
        }

        Amount = amount;
        Unit = unit;
    }

    public int Amount { get; }
    public BarTimeframeUnit Unit { get; }
    public bool IsTick => Unit == BarTimeframeUnit.Tick;

    public TimeSpan ToTimeSpan()
    {
        if (Unit == BarTimeframeUnit.Tick)
        {
            return TimeSpan.Zero;
        }

        return Unit switch
        {
            BarTimeframeUnit.Second => TimeSpan.FromSeconds(Amount),
            BarTimeframeUnit.Minute => TimeSpan.FromMinutes(Amount),
            BarTimeframeUnit.Hour => TimeSpan.FromHours(Amount),
            BarTimeframeUnit.Day => TimeSpan.FromDays(Amount),
            _ => TimeSpan.FromSeconds(Amount)
        };
    }

    public override string ToString()
    {
        return Unit switch
        {
            BarTimeframeUnit.Tick => "tick",
            BarTimeframeUnit.Second => $"{Amount}s",
            BarTimeframeUnit.Minute => $"{Amount}m",
            BarTimeframeUnit.Hour => $"{Amount}h",
            BarTimeframeUnit.Day => $"{Amount}d",
            _ => $"{Amount}{Unit}"
        };
    }

    public bool Equals(BarTimeframe? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Amount == other.Amount && Unit == other.Unit;
    }

    public override bool Equals(object? obj)
    {
        return obj is BarTimeframe other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Amount * 397) ^ (int)Unit;
        }
    }

    public static BarTimeframe Tick => new(1, BarTimeframeUnit.Tick);
    public static BarTimeframe Seconds(int amount) => new(amount, BarTimeframeUnit.Second);
    public static BarTimeframe Minutes(int amount) => new(amount, BarTimeframeUnit.Minute);
    public static BarTimeframe Hours(int amount) => new(amount, BarTimeframeUnit.Hour);
    public static BarTimeframe Days(int amount) => new(amount, BarTimeframeUnit.Day);
}
