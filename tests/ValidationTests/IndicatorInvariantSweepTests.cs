using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Properties every indicator must have, asserted across every indicator the library ships.
/// </summary>
/// <remarks>
/// <para>
/// These are not reference values. A reference test says "this indicator on this data gives these numbers",
/// which only ever covers the numbers someone wrote down. An invariant says something that must hold for any
/// correct implementation on any input, so it covers inputs nobody thought of - and it is what caught our ATR
/// returning 1.9999999999999984 on a series whose true range is exactly 2, where every competitor returned 2.
/// </para>
/// <para>
/// The sweep discovers its subjects, so a new indicator is covered the moment it is emitted, and every failure
/// is collected rather than thrown so one bad indicator does not hide the rest.
/// </para>
/// </remarks>
public sealed class IndicatorInvariantSweepTests
{
    private const int Bars_ = 160;

    // The flat series needs far more room than the random walk: twenty averages have a warmup of 200 bars or
    // more, and at 160 bars their settle window was empty, so they were skipped rather than checked. Twenty-one
    // failures hid behind that until the fixture was lengthened.
    private const int FlatBars_ = 1200;

    private static IReadOnlyList<Type> Indicators() =>
        [.. typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .Where(typeof(IIndicator).IsAssignableFrom)
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    private static IIndicator? TryConstruct(Type type)
    {
        var constructor = type.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault(c => c.GetParameters().All(p => p.IsOptional));

        if (constructor is null)
        {
            return null;
        }

        try
        {
            return (IIndicator)constructor.Invoke(
                constructor.GetParameters().Select(p => p.DefaultValue).ToArray());
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Every bar identical, so anything derived from price is constant and anything derived from
    /// range is zero. The answer is fixed by arithmetic rather than by a chart someone read.</summary>
    private static IReadOnlyList<Bar> Flat(double price)
    {
        var start = new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc);
        return [.. Enumerable.Range(0, FlatBars_).Select(i =>
            new Bar(start.AddMinutes(i), price, price, price, price, 1000))];
    }

    private static IReadOnlyList<Bar> Seeded()
    {
        var random = new Random(31);
        var bars = new List<Bar>(Bars_);
        var start = new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc);
        var last = 100d;

        for (var i = 0; i < Bars_; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new Bar(start.AddMinutes(i), open, Math.Max(open, close) + random.NextDouble(),
                Math.Max(0.01, Math.Min(open, close) - random.NextDouble()), close, random.Next(1000, 400000)));
            last = close;
        }

        return bars;
    }

    private static double[]? Run(IIndicator indicator, IReadOnlyList<Bar> bars)
    {
        try
        {
            using var run = new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator)
                .BuildAsync().GetAwaiter().GetResult();

            return run[indicator].ToArray();
        }
        catch (Exception)
        {
            // An indicator that refuses this fixture is not what these invariants are about. The reachability
            // floor below is what stops that becoming a silent pass.
            return null;
        }
    }

    [Fact]
    public void EveryIndicatorReturnsOneValuePerBar()
    {
        var bars = Seeded();
        var wrong = new List<string>();
        var reached = 0;

        foreach (var type in Indicators())
        {
            var indicator = TryConstruct(type);
            if (indicator is null)
            {
                continue;
            }

            var values = Run(indicator, bars);
            if (values is null)
            {
                continue;
            }

            reached++;
            if (values.Length != Bars_)
            {
                wrong.Add(type.Name + ": " + values.Length + " values for " + Bars_ + " bars");
            }
        }

        reached.Should().BeGreaterThan(500, "the sweep must actually compute indicators");
        wrong.Should().BeEmpty("anything indexing a series by bar is misaligned otherwise");
    }

    [Fact]
    public void EveryIndicatorIsDeterministic()
    {
        var bars = Seeded();
        var wrong = new List<string>();
        var reached = 0;

        foreach (var type in Indicators())
        {
            var first = TryConstruct(type);
            var second = TryConstruct(type);
            if (first is null || second is null)
            {
                continue;
            }

            var a = Run(first, bars);
            var b = Run(second, bars);
            if (a is null || b is null)
            {
                continue;
            }

            reached++;
            if (!a.SequenceEqual(b))
            {
                wrong.Add(type.Name);
            }
        }

        reached.Should().BeGreaterThan(500);
        wrong.Should().BeEmpty("the same bars must give the same answer - usually an unseeded random otherwise");
    }

    [Fact]
    public void EveryIndicatorIsFiniteOnASaneSeries()
    {
        var bars = Seeded();
        var wrong = new List<string>();
        var reached = 0;

        foreach (var type in Indicators())
        {
            var indicator = TryConstruct(type);
            if (indicator is null)
            {
                continue;
            }

            var values = Run(indicator, bars);
            if (values is null)
            {
                continue;
            }

            reached++;
            var bad = values.Count(v => double.IsNaN(v) || double.IsInfinity(v));
            if (bad > 0)
            {
                wrong.Add(type.Name + ": " + bad + " non-finite values");
            }
        }

        reached.Should().BeGreaterThan(500);
        wrong.Should().BeEmpty("a NaN reaching a trading decision is the failure this exists to stop");
    }

    [Fact]
    public void AMovingAverageOfAConstantSeriesIsThatConstant()
    {
        const double price = 50;
        var bars = Flat(price);
        var wrong = new List<string>();
        var reached = 0;

        foreach (var type in Indicators().Where(t => typeof(IMovingAverage).IsAssignableFrom(t)))
        {
            var indicator = TryConstruct(type);
            if (indicator is null)
            {
                continue;
            }

            var values = Run(indicator, bars);
            if (values is null)
            {
                continue;
            }

            reached++;

            // Past the warm-up, an average of a constant can only be that constant. Any weighting, any
            // smoothing, any adaptive length: the weights sum to one over identical values.
            // Three warmups, and never so far in that nothing is left to check: a window that empties is a
            // skipped indicator wearing a pass.
            var settled = values.Skip(Math.Min(FlatBars_ - 200, Math.Max(indicator.WarmupBars * 3, 60))).ToArray();
            settled.Should().NotBeEmpty("an empty settle window checks nothing");

            var worst = settled.Max(v => Math.Abs(v - price));
            if (worst > 1e-6)
            {
                wrong.Add(type.Name + ": off by " + worst.ToString("G4"));
            }
        }

        reached.Should().BeGreaterThan(100, "there are 180 moving averages to sweep");
        wrong.Should().BeEmpty();
    }
}
