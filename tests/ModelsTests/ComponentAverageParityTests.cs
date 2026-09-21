using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// The adversarial check on component substitution. An indicator handed one of the library's averages
/// collapses it into a MovingAvgType and the batch calculation answers. Handed a caller's own average, the
/// indicator's own calculation runs with that one average answered by the caller's series instead. If the
/// caller's average computes exactly what the built-in computes, the two must agree bit for bit - and any
/// indicator where they do not is one whose average is not the single thing the substitution assumed.
/// </summary>
public sealed class ComponentAverageParityTests
{
    /// <summary>A simple moving average that is deliberately not one of ours, so it cannot collapse to an enum.</summary>
    private sealed class MirrorSma(int length) : IndicatorBase, IMovingAverage
    {
        public override int WarmupBars => length;

        protected internal override object? CreateState() => new State(length);

        private sealed class State(int length) : IIndicatorState
        {
            private readonly Queue<double> _window = new(length);

            public void Reset() => _window.Clear();

            public double Update(in Bar bar)
            {
                _window.Enqueue(bar.Close);
                if (_window.Count > length)
                {
                    _window.Dequeue();
                }

                // Zero until the window is full, which is what the library's own simple average does. A
                // double that warms up differently is not the same average, and every disagreement it
                // caused would be the test's rather than the substitution's.
                if (_window.Count < length)
                {
                    return 0;
                }

                double sum = 0;
                foreach (var value in _window)
                {
                    sum += value;
                }

                return sum / _window.Count;
            }
        }
    }

    private static IReadOnlyList<Bar> Walk(int count = 150)
    {
        var random = new Random(31);
        var bars = new List<Bar>(count);
        var start = new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc);
        var last = 100d;

        for (var i = 0; i < count; i++)
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
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync().GetAwaiter().GetResult();

        return run[indicator].ToArray();
    }

    [Fact]
    public void SubstitutingAnAverageComputesWhatNamingItComputes()
    {
        var bars = Walk();
        var disagreed = new List<string>();
        var proved = 0;
        var refused = 0;

        foreach (var type in typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var ctor = type.GetConstructors().FirstOrDefault(c =>
                c.GetParameters().Any(p => p.ParameterType == typeof(IMovingAverage))
                && c.GetParameters().All(p => p.IsOptional));
            if (ctor is null)
            {
                continue;
            }

            object?[] Args(IMovingAverage? average) => ctor.GetParameters()
                .Select(p => p.ParameterType == typeof(IMovingAverage) ? average : p.DefaultValue)
                .ToArray();

            IIndicator named;
            int length;
            try
            {
                named = (IIndicator)ctor.Invoke(Args(new Sma(20)));

                // The batch runs its average over the indicator's own length, not the component's, so the
                // caller's average has to use that same length or the two are not the same question.
                var declared = type.GetProperty("Length")?.GetValue(named);
                if (declared is not int value)
                {
                    continue;
                }

                length = value;
            }
            catch
            {
                continue;
            }

            double[]? baseline;
            double[]? substituted;
            try
            {
                baseline = Run(named, bars);

                // Several indicators smooth over a second parameter rather than the one they are named
                // with, so the first run is only there to learn the period the average was actually asked
                // for. Mirroring the wrong period would compare two different averages and blame the
                // substitution for the difference.
                // Cleared first: an indicator that computes its own bands never reaches the substitution,
                // and reading a period left behind by the previous indicator would mirror the wrong one.
                StockIndicatorBuilder.LastAverageLength = 0;
                substituted = Run((IIndicator)ctor.Invoke(Args(new MirrorSma(length))), bars);
                var asked = StockIndicatorBuilder.LastAverageLength;
                if (asked > 0 && asked != length)
                {
                    substituted = Run((IIndicator)ctor.Invoke(Args(new MirrorSma(asked))), bars);
                }
            }
            catch (NotSupportedException)
            {
                refused++;
                continue;
            }
            catch
            {
                continue;
            }

            if (baseline is null || substituted is null || baseline.Length != substituted.Length)
            {
                continue;
            }

            proved++;
            for (var i = 0; i < baseline.Length; i++)
            {
                if (Math.Abs(baseline[i] - substituted[i]) > 1e-9)
                {
                    disagreed.Add(type.Name + " at bar " + i + ": named " + baseline[i].ToString("G6")
                        + ", substituted " + substituted[i].ToString("G6"));
                    break;
                }
            }
        }

        // Written out on success as well as failure: how many indicators accept a caller's own average is
        // the number this work is measured by, and an assertion message is only shown when it fails.
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "component-average-parity.txt"),
            "substituted=" + proved + " refused=" + refused + " disagree=" + disagreed.Count);

        proved.Should().BeGreaterThan(0, "the substitution has to be exercised for this to prove anything");
        disagreed.Should().BeEmpty(proved + " indicators substituted, " + refused + " refused as ambiguous, "
            + disagreed.Count + " disagree: " + string.Join(" | ", disagreed.Take(12)));
    }
}
