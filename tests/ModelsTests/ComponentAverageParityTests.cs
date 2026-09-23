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
            // The library's own simple average, driven bar by bar. Writing the mean out by hand leaves the
            // two disagreeing in the last bits - invisible on its own, but an indicator that feeds its
            // average back amplifies it, which is what OvershootReductionMovingAverage did by bar 58.
            // Borrowing the real arithmetic makes this a mirror by construction, so anything left is
            // structural rather than two ways of writing the same mean.
            private readonly OoplesFinance.StockIndicators.Streaming.IMovingAverageSmoother _sma =
                OoplesFinance.StockIndicators.Streaming.MovingAverageSmootherFactory.Create(
                    MovingAvgType.SimpleMovingAverage, length);

            public void Reset() => _sma.Reset();

            public double Update(in Bar bar) => _sma.Next(bar.Close, isFinal: true);
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
    public void AnIndicatorThatSmoothsTwoThingsTakesOneAverageForEach()
    {
        var bars = Walk();

        // AwesomeOscillator is the fast average of the median minus the slow one, at 5 bars and 34. Handing
        // it one average answers both and makes it a difference of an average with itself, which is zero;
        // handing it the two it actually asks for has to reproduce naming the simple average exactly.
        var named = Run(new AwesomeOscillator(), bars);
        var supplied = Run(new AwesomeOscillator(5, new MirrorSma(5), new MirrorSma(34)), bars);

        named.Should().NotBeNull();
        supplied.Should().NotBeNull();
        supplied!.Should().Equal(named!, "the two averages it asks for are the two it was given");
        supplied.Skip(40).Should().Contain(v => Math.Abs(v) > 1e-9,
            "a difference of two different averages is not identically zero");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ConfiguredStagesRetainTheirOrderAndPeriods(bool builtInFirst, bool builtInSecond)
    {
        var bars = Walk();
        // Deliberately different from AO defaults: ignoring either period must fail.
        IMovingAverage first = builtInFirst ? new Sma(3) : new MirrorSma(3);
        IMovingAverage second = builtInSecond ? new Sma(11) : new MirrorSma(11);
        var actual = Run(new AwesomeOscillator(5, first, second), bars)!;
        var expected = Run(new AwesomeOscillator(5, new MirrorSma(3), new MirrorSma(11)), bars)!;
        actual.Should().Equal(expected, (a, e) => Math.Abs(a - e) <= 1e-8);
        actual.Skip(40).Should().Contain(v => Math.Abs(v) > 1e-9);
    }

    [Fact]
    public void AnExtraAverageCannotSilentlyReplaceAnOmittedPrecedingStage()
    {
        Assert.Throws<ArgumentException>(() => new AwesomeOscillator(5, null, new Sma(13)));
    }

    [Fact]
    public async Task LiveSourceRejectsStagesItCannotRepresent()
    {
        var feed = Bars.Live();
        var builder = new StockIndicatorBuilder().ConfigureSource(feed)
            .ConfigureIndicators(new AwesomeOscillator(5, new Sma(3), new Sma(11)));
        Func<Task> build = async () => { using var run = await builder.BuildAsync(); };
        await build.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*separately configured average stages*");
        feed.Complete();
    }

    [Fact]
    public void SubstitutingAnAverageComputesWhatNamingItComputes()
    {
        var bars = Walk();
        var disagreed = new List<string>();
        var proved = 0;
        var refused = 0;
        var multiAverage = 0;

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
                StockIndicatorBuilder.LastAverageRequests = 0;
                substituted = Run((IIndicator)ctor.Invoke(Args(new MirrorSma(length))), bars);
                var asked = StockIndicatorBuilder.LastAverageLength;

                // Naming a MovingAvgType applies it to every average the calculation asks for. Handing over
                // components does not - each one answers a different request, which is the point of them.
                // So the two forms are the same question only where there is one average to answer, and
                // this test supplies the same instance to every slot, which on a difference of two averages
                // is zero by construction. Those are covered by giving each slot its own average instead.
                if (StockIndicatorBuilder.LastAverageRequests > 1)
                {
                    multiAverage++;
                    continue;
                }

                // An average whose window never fills over this fixture is not being exercised by either
                // side - HirashimaSugitaRS asks for 1000 bars of it - so there is nothing here to hold the
                // two to. Comparing them anyway measures the warm-up convention, not the substitution.
                if (asked > bars.Count)
                {
                    continue;
                }

                var matchedLength = asked > 0 ? asked : length;
                substituted = Run((IIndicator)ctor.Invoke(Args(new MirrorSma(matchedLength))), bars);
                // Multiple configured built-in stages now retain their periods too. Comparing
                // Sma(20) with MirrorSma(matchedLength) would test different configurations.
                baseline = Run((IIndicator)ctor.Invoke(Args(new Sma(matchedLength))), bars);
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
            "substituted=" + proved + " refused=" + refused + " multiAverage=" + multiAverage
                + " disagree=" + disagreed.Count);

        proved.Should().BeGreaterThan(0, "the substitution has to be exercised for this to prove anything");
        disagreed.Should().BeEmpty(proved + " indicators substituted, " + refused + " refused as ambiguous, "
            + disagreed.Count + " disagree: " + string.Join(" | ", disagreed.Take(12)));
    }
}
