using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WilderNumericalTests
{
    [Fact]
    public void BothNativeFactoriesHonorEveryWilderAliasPeriodPreviewAndReset()
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        foreach (var length in new[] { 1, 3, 14 })
        {
            var expected = BuiltInFormulaReferences.RoundedWilderTrajectory(fixture.Bars.Select(b => b.Close).ToArray(), length);
            foreach (var options in new IIndicatorSpecOptions[] { new WwmaSpecOptions(length), new SmmaSpecOptions(length), new ModifiedMaSpecOptions(length) })
            {
                var spec = new IndicatorSpec(IndicatorName.WellesWilderMovingAverage, options);
                foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
                {
                    Assert.NotNull(state);
                    using var lifetime = state as IDisposable;
                    for (var replay = 0; replay < 2; replay++)
                    {
                        state.Reset();
                        for (var i = 0; i < fixture.Bars.Count; i++)
                        {
                            var b = fixture.Bars[i];
                            var bar = new OhlcvBar("WW", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                            Assert.Equal(expected[i], state.Update(bar, false, true).Outputs!["Wwma"]);
                            Assert.Equal(expected[i], state.Update(bar, true, true).Value);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task WilderAliasesPreserveSelectedInputAcrossPublicChainsAndFastArms()
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars;
            var source = new Sma(2);
            var aliases = new IndicatorBase[] { new Wwma(3), new Smma(3), new ModifiedMa(3) };
            foreach (var alias in aliases) alias.Of(source);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(new IIndicator[] { source }.Concat(aliases).ToArray()).BuildAsync();
            var selected = run[source].ToArray();
            var expected = BuiltInFormulaReferences.RoundedWilderTrajectory(selected, 3);
            foreach (var alias in aliases) Assert.Equal(expected, run[alias].ToArray());
            foreach (var chained in new[] { false, true })
            {
                var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
                    bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
                    bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
                if (chained) data.CustomValuesList = selected.ToList();
                else data.InputValues = selected.ToList();
                using var context = new ComputeContext();
                using var wilder = IndicatorCompute.ComputeWwmaFast(data, context, 3);
                using var smoothed = IndicatorCompute.ComputeSmmaFast(data, context, 3);
                using var modified = IndicatorCompute.ComputeModifiedMaFast(data, context, 3);
                Assert.Equal(expected, wilder.ToArray());
                Assert.Equal(expected, smoothed.ToArray());
                Assert.Equal(expected, modified.ToArray());
            }
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.WellesWilderMovingAverage)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryWilderAliasReceivesAllNumericalClasses(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(int.MaxValue)]
    public void RecurrenceMatchesIndependentRoundedCorrectionAcrossExponentGrids(int length)
    {
        var random = new Random(246);
        var values = Enumerable.Range(0, 512).Select(_ =>
        {
            var value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            return double.IsFinite(value) ? value : double.Epsilon;
        }).ToArray();
        var expected = BuiltInFormulaReferences.RoundedWilderTrajectory(values, length);
        var actual = new double[values.Length];
        MovingAverageCore.WellesWilderMovingAverage(values, actual, length);
        Assert.Equal(expected, actual);
        var dates = Enumerable.Range(0, values.Length).Select(i => DateTime.UnixEpoch.AddMinutes(i)).ToList();
        var data = new StockData(values.ToList(), values.ToList(), values.ToList(), values.ToList(),
            values.Select(_ => 1d).ToList(), dates);
        Assert.Equal(expected, data.CalculateWellesWilderMovingAverage(length).CustomValuesList);
        var state = new WellesWilderMovingAverageState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < values.Length; i++)
            {
                var v = values[i];
                var bar = new OhlcvBar("WW", BarTimeframe.Minutes(1), dates[i], dates[i], v, v, v, v, 1, true);
                Assert.Equal(expected[i], state.Update(bar, false, true).Value);
                Assert.Equal(expected[i], state.Update(bar, true, true).Value);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    public void ScalarRejectionDoesNotChangeTheRecurrence(int length)
    {
        var state = new WilderState(length); var control = new WilderState(length);
        state.GetNext(double.Epsilon, true); control.GetNext(double.Epsilon, true);
        foreach (var invalid in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity })
        foreach (var commit in new[] { false, true })
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GetNext(invalid, commit));
        Assert.Equal(control.GetNext(7, true), state.GetNext(7, true));
    }
}
