using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SequentialMeanNumericalTests
{
    [Fact]
    public void DiscoveryIncludesAllPromotedSequentialComponents()
    {
        Assert.Equal(39, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("sequential-composition/")));
    }

    [Fact]
    public void FullStrictWindowAndFirstPriceSeedAreRequired()
    {
        var prices = new[] { 1d, 2d, 3d, 3d, 4d, 5d, 6d, 7d };
        var actual = new double[prices.Length];
        MovingAverageCore.SequentiallyFilteredMovingAverage(prices, actual, 3);
        Assert.Equal(new[] { 1d, 1d, 1d, 1d, 10d / 3, 4d, 5d, 6d }, actual);
        MovingAverageCore.SequentiallyFilteredMovingAverage(prices, actual, 1);
        Assert.Equal(prices, actual);
        MovingAverageCore.SequentiallyFilteredMovingAverage(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
        using var gate = new OoplesFinance.StockIndicators.Helpers.SequentialMeanGate(3);
        var means = new[] { 1d, 2d, 3d, 3d, 4d, 5d, 6d };
        var expected = new[] { 10d, 10d, 3d, 3d, 3d, 3d, 6d };
        for (var replay = 0; replay < 2; replay++)
        {
            gate.Reset();
            for (var i = 0; i < means.Length; i++)
            {
                gate.Next(double.MaxValue, double.MaxValue, false);
                Assert.Equal(expected[i], gate.Next(10, means[i], false));
                Assert.Equal(expected[i], gate.Next(10, means[i], true));
            }
        }
    }

    [Fact]
    public void ExactSimpleStageHonorsCustomerOverride()
    {
        var values = new[] { 2d, 4d, 6d, 8d, 10d };
        var data = new StockData(values.ToList(), values.ToList(), values.ToList(), values.ToList(),
            values.Select(_ => 1d).ToList(), Enumerable.Range(0, values.Length).Select(i => DateTime.UnixEpoch.AddMinutes(i)).ToList());
        using var scope = ComponentAverage.Arm((input, period) => input.Select(value => value / 2).ToArray());
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeSequentiallyFilteredMovingAverageFast(data, context, 3);
        Assert.Equal(new[] { 2d, 2d, 3d, 4d, 5d }, result.ToArray());
        Assert.Equal(1, ComponentAverage.Requests);
        Assert.Equal(1, ComponentAverage.Substitutions);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task SequentialMeanPreservesSelectedInput(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars;
            var source = new Sma(2);
            var indicator = new SequentiallyFilteredMovingAverage(length, new Sma());
            indicator.Of(source);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(source, indicator).BuildAsync();
            var selected = run[source].ToArray();
            var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
            var expected = BuiltInFormulaReferences.RoundedSequentialMean(projected, length, 1);
            Assert.Equal(expected, run[indicator].ToArray());
            foreach (var chained in new[] { false, true })
            {
                var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
                    bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
                    bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
                if (chained) data.CustomValuesList = selected.ToList();
                else data.InputValues = selected.ToList();
                using var context = new ComputeContext();
                using var result = IndicatorCompute.ComputeSequentiallyFilteredMovingAverageFast(data, context, length);
                Assert.Equal(expected, result.ToArray());
            }
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && BuiltInFormulaReferences.HasBoundedSequentialMean(b))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task DiscoveredConfigurationsReceiveEveryNumericalClass(IndicatorValidationCase testCase)
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
    public async Task SequentialGateMatchesEveryRoute(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        foreach (var referenceKind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })
        {
            var bars = fixture.Bars;
            IMovingAverage average = referenceKind switch
            {
                1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                _ => throw new InvalidOperationException()
            };
            var kind = ((IBuiltInMovingAverage)average).AvgType;
            var expected = BuiltInFormulaReferences.RoundedSequentialMean(bars, length, referenceKind);
            var indicator = new SequentiallyFilteredMovingAverage(length, average);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var close = bars.Select(b => b.Close).ToArray();
            StockData Data() => new(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
                bars.Select(b => b.Low).ToList(), close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, Data().CalculateSequentiallyFilteredMovingAverage(kind, length).CustomValuesList);
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeSequentiallyFilteredMovingAverageFast(Data(), context, length, kind);
            Assert.Equal(expected, buffer.ToArray());
            if (referenceKind == 1)
            {
                var actual = new double[bars.Count];
                MovingAverageCore.SequentiallyFilteredMovingAverage(close, actual, length);
                Assert.Equal(expected, actual);
            }
            using var state = new SequentiallyFilteredMovingAverageState(kind, length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Count; i++)
                {
                    var b = bars[i];
                    var bar = new OhlcvBar("MHL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(expected[i], state.Update(bar, false, true).Outputs!["Sfma"]);
                    Assert.Equal(expected[i], state.Update(bar, true, true).Value);
                }
            }
        }
    }

}
