using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PairedBatchAllocationTests
{
    public static IEnumerable<object[]> Cases => from operation in new[] { 0, 1, 2, 3 }
        from length in new[] { 1, 3, 14 } select new object[] { operation, length };
    private static IMultiSeriesIndicatorState Create(int operation, int length, SeriesKey p, SeriesKey b) => operation switch
    {
        0 => new ComparePriceMomentumOscillatorState(p, b, length, length),
        1 => new RelativeNormalizedVolatilityState(p, b, MovingAvgType.SimpleMovingAverage, length),
        2 => new RelativeStrength3DIndicatorState(p, b, MovingAvgType.ExponentialMovingAverage, length, length, length, length, length),
        _ => new SectorRotationModelState(p, b, MovingAvgType.ExponentialMovingAverage, length, length)
    };

    [Theory, MemberData(nameof(Cases))]
    public void SingleAndMultipleOutputsMatchDictionaryReplay(int operation, int length)
    {
        foreach (var prices in new double[][] { [], [1], [1, 2, 1, 4, 3, 2],
            [double.Epsilon, double.MaxValue, -double.MaxValue, 0, 1] })
        {
            Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> factory = (p, b) => Create(operation, length, p, b);
            Dictionary<string, List<double>>? expected = null, actual = null;
            var expectedError = Record.Exception(() => expected = Replay(prices, prices, factory));
            var actualError = Record.Exception(() => actual = PairedBatch.Run(prices, prices, factory));
            Assert.Equal(expectedError?.GetType(), actualError?.GetType());
            Assert.Equal(expectedError?.Message, actualError?.Message);
            if (expectedError is null) Equal(expected!, actual!);
            // Independent benchmark prices expose nonzero relative outputs.
            if (prices.All(double.IsFinite) && prices.All(p => p is >= 1 and <= 4))
                Equal(Replay(prices, prices.Select(p => 5 - p).ToArray(), factory),
                    PairedBatch.Run(prices, prices.Select(p => 5 - p).ToArray(), factory));
        }
    }

    [Fact]
    public void UnknownStateWithRecognizedNameRetainsAllOutputs()
    {
        var output = PairedBatch.Run([1d, 2], [3d, 4], (p, b) => new AdditionalOutputState(p));
        Assert.Equal(new[] { "First", "Second" }, output.Keys);
        Assert.Equal(new[] { 1d, 2 }, output["First"]);
        Assert.Equal(new[] { -1d, -2 }, output["Second"]);
    }

    [Fact]
    public void InvalidInputsRetainDictionaryReplayErrors()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var primaryInvalid in new[] { false, true })
        {
            double[] p = primaryInvalid ? [1, value] : [1, 2], b = primaryInvalid ? [1, 2] : [1, value];
            Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> factory = (a, c) => Create(0, 3, a, c);
            var expected = Record.Exception(() => Replay(p, b, factory));
            var actual = Record.Exception(() => PairedBatch.Run(p, b, factory));
            Assert.NotNull(expected); Assert.Equal(expected.GetType(), actual?.GetType()); Assert.Equal(expected.Message, actual?.Message);
        }
    }

    private sealed class AdditionalOutputState(SeriesKey primary) : IMultiSeriesIndicatorState
    {
        public IndicatorName Name => IndicatorName.ComparePriceMomentumOscillator;
        public void Reset() { }
        public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar, bool isFinal, bool includeOutputs)
        {
            if (!series.Equals(primary)) return new(false, 0, null);
            Assert.True(includeOutputs);
            return new(true, bar.Close, new Dictionary<string, double> { ["First"] = bar.Close, ["Second"] = -bar.Close });
        }
    }

    private static Dictionary<string, List<double>> Replay(IReadOnlyList<double> primary, IReadOnlyList<double> benchmark,
        Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> factory)
    {
        var p = new SeriesKey("PRIMARY", BarTimeframe.Minutes(1)); var b = new SeriesKey("BENCHMARK", BarTimeframe.Minutes(1));
        var state = factory(p, b); using var lifetime = state as IDisposable;
        var context = new MultiSeriesContext(new SeriesStore()); var output = new Dictionary<string, List<double>>();
        for (var i = 0; i < primary.Count; i++)
        {
            var time = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i);
            OhlcvBar Bar(SeriesKey key, double price) => new(key.Symbol, key.Timeframe, time, time, price, price, price, price, 0, true);
            state.Update(context, b, Bar(b, benchmark[i]), true, false);
            var point = state.Update(context, p, Bar(p, primary[i]), true, true);
            Assert.True(point.HasValue); Assert.NotNull(point.Outputs);
            foreach (var pair in point.Outputs)
            {
                if (!output.TryGetValue(pair.Key, out var values)) output[pair.Key] = values = new(primary.Count);
                values.Add(pair.Value);
            }
        }
        return output;
    }

    private static void Equal(Dictionary<string, List<double>> expected, Dictionary<string, List<double>> actual)
    {
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var key in expected.Keys)
            Assert.Equal(expected[key].Select(BitConverter.DoubleToInt64Bits), actual[key].Select(BitConverter.DoubleToInt64Bits));
    }
}
