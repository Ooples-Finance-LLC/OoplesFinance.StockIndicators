using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// A window indicator is still exact after a very long run, in both engines.
/// </summary>
/// <remarks>
/// <para>
/// Each of these indicators depends only on the bars in its window. After two hundred thousand bars, its last
/// values must therefore match a batch run over nothing but the last few hundred bars - a recomputation with no
/// history to drift from. The value parity test cannot see drift: 251 bars is too few for running sums to
/// wander.
/// </para>
/// <para>
/// The series spends its first half near 100,000 and its second near 10. That is the case running sums handle
/// worst: they keep the rounding error of the large values and carry it into the small ones.
/// </para>
/// </remarks>
public sealed class StreamingLongRunStabilityTests
{
    private const int Bars = 200_000;
    private const int Compared = 300;

    public static TheoryData<Type> WindowStates => new()
    {
        typeof(SimpleMovingAverageState),
        typeof(WeightedMovingAverageState),
        typeof(LinearRegressionState),
        typeof(ChandeForecastOscillatorState),
        typeof(BollingerBandsState),
        typeof(StandardDeviationChannelState),
        typeof(EhlersMovingAverageDifferenceIndicatorState),
        // Window sums of price the batch engine takes as differences of prefix sums.
        typeof(ChandeMomentumOscillatorState),
        typeof(MoneyFlowIndexState),
        typeof(VortexIndicatorState),
    };

    [Theory]
    [MemberData(nameof(WindowStates))]
    public void AWindowIndicatorIsStillExactAfterAVeryLongRun(Type stateType)
    {
        var tickers = BuildSeries();
        var ctor = stateType.GetConstructors()
            .Where(c => c.GetParameters().All(p => p.HasDefaultValue))
            .OrderBy(c => c.GetParameters().Length)
            .First();
        var state = (IStreamingIndicatorState)ctor.Invoke(ctor.GetParameters().Select(p => p.DefaultValue).ToArray());
        var method = IndicatorInvoker.GetMethod(state.Name)
            ?? throw new InvalidOperationException($"{state.Name} has no batch method");

        var streamed = RunStreaming(state, tickers);
        var batch = RunBatch(method, tickers);
        // Twice the compared span, so every compared bar has a full window behind it.
        var exact = RunBatch(method, tickers.Skip(Bars - (2 * Compared)).ToList());

        using var scope = new AssertionScope();
        // The regression's intercept is reported at the first bar of the run, so it depends on how long the run
        // has been and not only on the window; its slope and fitted values are compared.
        foreach (var key in exact.Keys.Where(k => k != "Intercept").OrderBy(k => k, StringComparer.Ordinal))
        {
            var expected = exact[key].Skip(Compared).ToList();
            FirstMismatch(expected, streamed[key]).Should().BeNull($"streaming {stateType.Name} {key} stays exact");
            FirstMismatch(expected, batch[key]).Should().BeNull($"batch {method.Name} {key} stays exact");
        }
    }

    private static string? FirstMismatch(List<double> expected, List<double> run)
    {
        var tail = run.Skip(run.Count - expected.Count).ToList();
        for (var i = 0; i < expected.Count; i++)
        {
            if (!IsClose(expected[i], tail[i]))
            {
                return $"bar {Bars - expected.Count + i}: exact {expected[i]}, after the long run {tail[i]}";
            }
        }

        return null;
    }

    private static List<TickerData> BuildSeries()
    {
        var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tickers = new List<TickerData>(Bars);
        var prevClose = 100_000d;
        for (var i = 0; i < Bars; i++)
        {
            // Deterministic, and irregular enough that no window is flat: two incommensurate waves on a level
            // that falls four orders of magnitude halfway through.
            var level = i < Bars / 2 ? 100_000d : 10d;
            var close = level * (1 + (0.01 * Math.Sin(i * 0.37)) + (0.004 * Math.Sin(i * 1.91)));
            var open = i == Bars / 2 ? close : prevClose;
            tickers.Add(new TickerData
            {
                Date = start.AddMinutes(i),
                Open = open,
                High = Math.Max(open, close) * 1.001,
                Low = Math.Min(open, close) * 0.999,
                Close = close,
                Volume = 1_000 + (500 * Math.Sin(i * 0.11)),
            });
            prevClose = close;
        }

        return tickers;
    }
}
