using System.Reflection;
using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Catalogs;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// <see cref="IndicatorDataSource.FromColumns"/> must compute exactly what
/// <see cref="IndicatorDataSource.FromBatch(StockData, IDataProviderDefaults?)"/> computes.
///
/// <para><b>Why bit for bit and not within a tolerance.</b> FromColumns exists to skip the copy FromBatch
/// makes, not to take a different route through the arithmetic. Two entry points that agree closely are two
/// engines, and the difference surfaces as a number a caller cannot reconcile with the documentation, on
/// whichever overload they did not test with. A tolerance here would hide exactly the class of mistake this
/// change can introduce - reading a stale view, an off-by-one slice, a column materialised from the wrong
/// source - because all of those produce values that are close on a random walk.</para>
///
/// <para><b>Why the whole catalog.</b> The seven benchmarked indicators are the ones that motivated the
/// change, so they are the ones least likely to be wrong. The risk lives in the 800 that were never looked at,
/// particularly the ones reading columns other than the close.</para>
/// </summary>
public sealed class ColumnSourceParityTests
{
    private const int BarCount = 300;

    [Fact]
    public void EveryCatalogIndicatorAgreesBetweenTheTwoBatchSources()
    {
        var bars = Bars(BarCount);
        var methods = SingleSeriesCatalogMethods();

        methods.Should().NotBeEmpty("the generated catalog is what this sweep covers");

        var disagreed = new List<string>();
        var reached = 0;

        foreach (var method in methods)
        {
            var fromBatch = TryCompute(method, () => IndicatorDataSource.FromBatch(bars.NewStockData()));
            var fromColumns = TryCompute(method, () => IndicatorDataSource.FromColumns(
                bars.Opens, bars.Highs, bars.Lows, bars.Closes, bars.Volumes, bars.Dates));

            if (fromBatch.Failed || fromColumns.Failed)
            {
                // An indicator that rejects this fixture must reject it identically through both sources.
                if (fromBatch.Failed != fromColumns.Failed)
                {
                    disagreed.Add(method.Name + ": one source threw and the other did not");
                }

                continue;
            }

            reached++;

            if (fromBatch.Values.Length != fromColumns.Values.Length)
            {
                disagreed.Add(method.Name + ": " + fromBatch.Values.Length + " values against "
                    + fromColumns.Values.Length);
                continue;
            }

            for (var i = 0; i < fromBatch.Values.Length; i++)
            {
                // The bits, not the value: double.Equals calls +0 and -0 the same number, and a column
                // path that turned one into the other would slip past a test whose whole job is to say the
                // two paths compute the same thing.
                if (BitConverter.DoubleToInt64Bits(fromBatch.Values[i])
                    != BitConverter.DoubleToInt64Bits(fromColumns.Values[i]))
                {
                    disagreed.Add(method.Name + "[" + i + "]: " + fromBatch.Values[i].ToString("R") + " against "
                        + fromColumns.Values[i].ToString("R"));
                    break;
                }
            }
        }

        // A sweep that reached nothing passes silently, which is the failure mode this guards against: the
        // catalog is generated, so a change to its shape can empty this test without emptying its assertion.
        reached.Should().BeGreaterThan(400, "the sweep must actually compute indicators, not just skip them");
        disagreed.Should().BeEmpty();
    }
    [Fact]
    public void TheMultiOutputIndicatorsAgreeOnEveryPublishedSeries()
    {
        var bars = Bars(BarCount);

        AssertAgrees(bars, catalog => catalog.BollingerBands(20, 2).Upper);
        AssertAgrees(bars, catalog => catalog.BollingerBands(20, 2).Middle);
        AssertAgrees(bars, catalog => catalog.BollingerBands(20, 2).Lower);
        AssertAgrees(bars, catalog => catalog.Macd(12, 26, 9).Primary);
        AssertAgrees(bars, catalog => catalog.Macd(12, 26, 9).Signal);
        AssertAgrees(bars, catalog => catalog.Macd(12, 26, 9).Histogram);
        AssertAgrees(bars, catalog => catalog.Stochastic(14, 3).K);
        AssertAgrees(bars, catalog => catalog.Stochastic(14, 3).D);
        AssertAgrees(bars, catalog => catalog.Atr(14));
    }

    /// <summary>
    /// The adopted columns must still answer as lists, because the v1 surface and anything a caller wrote
    /// against it reads them that way. A column is built on first touch and only the column touched.
    /// </summary>
    [Fact]
    public void AnAdoptedColumnStillAnswersAsAList()
    {
        var bars = Bars(BarCount);
        var source = IndicatorDataSource.FromColumns(
            bars.Opens, bars.Highs, bars.Lows, bars.Closes, bars.Volumes, bars.Dates);

        source.BatchData.Should().NotBeNull();
        var data = source.BatchData!;

        data.Count.Should().Be(BarCount);
        data.ClosePrices.Should().Equal(bars.Closes);
        data.HighPrices.Should().Equal(bars.Highs);
        data.LowPrices.Should().Equal(bars.Lows);
        data.OpenPrices.Should().Equal(bars.Opens);
        data.Volumes.Should().Equal(bars.Volumes);
        data.Dates.Should().Equal(bars.Dates);
        data.InputValues.Should().Equal(bars.Closes);
    }

    [Fact]
    public void ColumnsOfUnequalLengthAreRejectedWhereTheMistakeWasMade()
    {
        var bars = Bars(BarCount);

        var act = () => IndicatorDataSource.FromColumns(
            bars.Opens, bars.Highs, bars.Lows, bars.Closes.AsMemory(0, BarCount - 1), bars.Volumes, bars.Dates);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*same length*", "a slicing mistake must be reported here, not become a count of 0");
    }
    private static void AssertAgrees(BarSet bars, Func<IndicatorCatalog, SeriesHandle> configure)
    {
        var fromBatch = Compute(IndicatorDataSource.FromBatch(bars.NewStockData()), configure);
        var fromColumns = Compute(
            IndicatorDataSource.FromColumns(bars.Opens, bars.Highs, bars.Lows, bars.Closes, bars.Volumes,
                bars.Dates),
            configure);

        fromColumns.Should().Equal(fromBatch);
    }

    private static double[] Compute(IndicatorDataSource source, Func<IndicatorCatalog, SeriesHandle> configure)
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(catalog => handle = configure(catalog))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().ToArray();
    }

    private static Computation TryCompute(MethodInfo method, Func<IndicatorDataSource> source)
    {
        try
        {
            var values = Compute(source(),
                catalog => (SeriesHandle)method.Invoke(catalog, [null, null, null])!);
            return new Computation(values, false);
        }
        catch (Exception)
        {
            // Some catalog entries reject a 300-bar fixture, need a longer history, or are obsolete. What
            // matters here is only that both sources behave the same way, which the caller checks.
            return new Computation([], true);
        }
    }

    private static List<MethodInfo> SingleSeriesCatalogMethods()
    {
        return [.. typeof(IndicatorCatalog)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(SeriesHandle))
            .Where(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length == 3
                    && parameters[0].ParameterType == typeof(int?)
                    && parameters[1].ParameterType == typeof(SeriesHandle?)
                    && parameters[2].ParameterType == typeof(IndicatorKey?);
            })
            .OrderBy(m => m.Name, StringComparer.Ordinal)];
    }

    private readonly record struct Computation(double[] Values, bool Failed);

    private static BarSet Bars(int count)
    {
        // Seeded, so a disagreement is reproducible bar for bar rather than a number someone has to chase.
        var random = new Random(7);
        var opens = new double[count];
        var highs = new double[count];
        var lows = new double[count];
        var closes = new double[count];
        var volumes = new double[count];
        var dates = new DateTime[count];
        var start = new DateTime(2021, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastClose = 50d;

        for (var i = 0; i < count; i++)
        {
            var open = lastClose + ((random.NextDouble() - 0.5) * 0.4);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.6));
            opens[i] = open;
            highs[i] = Math.Max(open, close) + random.NextDouble();
            lows[i] = Math.Max(0.01, Math.Min(open, close) - random.NextDouble());
            closes[i] = close;
            volumes[i] = random.Next(1000, 400000);
            dates[i] = start.AddMinutes(i);
            lastClose = close;
        }

        return new BarSet(opens, highs, lows, closes, volumes, dates);
    }

    private sealed record BarSet(
        double[] Opens,
        double[] Highs,
        double[] Lows,
        double[] Closes,
        double[] Volumes,
        DateTime[] Dates)
    {
        /// <summary>A fresh one per arm: the batch API writes its results back into what it is handed.</summary>
        public StockData NewStockData() => new(
            [.. Opens], [.. Highs], [.. Lows], [.. Closes], [.. Volumes], [.. Dates]);
    }
}