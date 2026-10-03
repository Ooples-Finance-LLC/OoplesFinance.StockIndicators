using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EmaInitializationTests
{
    public static IEnumerable<object[]> Prefixes()
    {
        foreach (var count in new[] { 2, 3, 14, 63 })
            foreach (var fixture in IndicatorAdversarialCases.Generate(count, 244))
                yield return new object[] { fixture.Bars.Select(bar => bar.Close).ToArray() };
        yield return new object[] { new[] { double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue } };
        yield return new object[] { new[] { double.MaxValue, 1d, -double.MaxValue } };
        yield return new object[] { new[] { 1e100, 1e84, -9.9989e99, -1.0998790000010039e96 } };
        yield return new object[] { new[] { double.Epsilon, 0d, double.Epsilon, -double.Epsilon } };
        yield return new object[] { new[] { 1d, double.BitIncrement(1), double.BitDecrement(1), -1d } };
    }

    [Theory]
    [MemberData(nameof(Prefixes))]
    public async Task EveryInitializationPrefixIsTheRoundedExactMean(double[] values)
    {
        var length = values.Length;
        var expected = values.Select((_, i) => values.Take(i + 1)
            .Select(ReferenceFraction.FromDouble).Aggregate(new ReferenceFraction(0), (a, b) => a + b)
            / new ReferenceFraction(i + 1)).Select(value => value.ToDouble()).ToArray();
        var output = new double[length];
        MovingAverageCore.ExponentialMovingAverage(values, output, length);
        Assert.Equal(expected, output);
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = new Ema(length);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, run[indicator].ToArray());
        var legacy = new StockData(values.ToList(), values.ToList(), values.ToList(), values.ToList(),
            values.Select(_ => 1d).ToList(), bars.Select(bar => bar.Time).ToList());
        Assert.Equal(expected, legacy.CalculateExponentialMovingAverage(length).CustomValuesList);
        var state = new EmaState(length);
        var native = new ExponentialMovingAverageState(length);
        using var smoother = new ExponentialMovingAverageSmoother(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); smoother.Reset(); native.Reset();
            for (var i = 0; i < length; i++)
            {
                // An unrelated overflowing preview must not alter the committed sum.
                state.GetNext(double.MaxValue, false);
                smoother.Next(double.MaxValue, false);
                Assert.Equal(expected[i], state.GetNext(values[i], false));
                Assert.Equal(expected[i], state.GetNext(values[i], true));
                Assert.Equal(expected[i], smoother.Next(values[i], false));
                Assert.Equal(expected[i], smoother.Next(values[i], true));
                var bar = bars[i];
                var input = new OhlcvBar("EMA", BarTimeframe.Minutes(1), bar.Time, bar.Time,
                    bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
                Assert.Equal(expected[i], native.Update(input, false, true).Value);
                Assert.Equal(expected[i], native.Update(input, true, true).Value);
            }
        }
    }

    [Fact]
    public void CorrectedSeedFeedsTheFirstRecursiveValue()
    {
        var state = new EmaState(2);
        Assert.Equal(double.MaxValue, state.GetNext(double.MaxValue, true));
        Assert.Equal(double.MaxValue, state.GetNext(double.MaxValue, true));
        var expected = (ReferenceFraction.FromDouble(double.MaxValue) / new ReferenceFraction(3)).ToDouble();
        Assert.Equal(expected, state.GetNext(0, false));
        Assert.Equal(expected, state.GetNext(0, true));
    }
}
