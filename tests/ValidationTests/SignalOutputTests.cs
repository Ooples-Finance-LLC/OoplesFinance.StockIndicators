using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// A named signal output has to be the signal. A fast arm that dispatches on the options type alone answers
/// every request with the indicator's primary series, so asking for the signal returned the series it is
/// supposed to smooth - silently, and in more than one indicator. Each one here is held against the v1
/// calculation that publishes both series.
/// </summary>
public sealed class SignalOutputTests
{
    private static List<Bar> Walk(int count = 120)
    {
        var random = new Random(31);
        var bars = new List<Bar>(count);
        var last = 100d;

        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new Bar(new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc).AddMinutes(i),
                open, Math.Max(open, close) + 0.5, Math.Min(open, close) - 0.5, close, 1000));
            last = close;
        }

        return bars;
    }

    [Fact]
    public void TheSignalLineIsAnAverageOfTheLineAndNotTheLine()
    {
        var bars = Walk();
        var adl = new AccumulationDistributionLine();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(adl)
            .BuildAsync().GetAwaiter().GetResult();

        var line = run[adl.Value].ToArray();
        var signal = run[adl.AdlSignal].ToArray();

        // The v1 calculation is the reference both routes reproduce, so the signal is held to it rather
        // than to a number read off a chart.
        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateAccumulationDistributionLine();

        line.Should().Equal(batch.OutputValues["Adl"].ToArray(), "the line itself was never in doubt");
        signal.Should().Equal(batch.OutputValues["AdlSignal"].ToArray(),
            "the signal is the average of the line, which is what the batch publishes");
        signal.Should().NotEqual(line, "an average of the line is not the line");
    }

    [Theory]
    [InlineData("Floor")]
    [InlineData("Fibonacci")]
    [InlineData("Woodie")]
    public void TheRemainingPivotTypesPublishEveryLevelPerPeriod(string family)
    {
        var bars = Walk(400);
        var batchInput = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList());

        var threeDeep = new[] { "Pivot", "S1", "S2", "S3", "R1", "R2", "R3", "M1", "M2", "M3", "M4", "M5", "M6" };
        var (indicator, batch, keys) = family switch
        {
            "Floor" => ((IIndicator)new FloorPivotPoint(), batchInput.CalculateFloorPivotPoints(), threeDeep),
            "Fibonacci" => (new FibonacciPivotPoint(), batchInput.CalculateFibonacciPivotPoints(), threeDeep),
            _ => (new WoodiePivotPoint(), batchInput.CalculateWoodiePivotPoints(),
                new[] { "Pivot", "S1", "S2", "S3", "S4", "R1", "R2", "R3", "R4", "M1", "M2", "M3", "M4" }),
        };

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync().GetAwaiter().GetResult();

        indicator.Outputs.Should().HaveCount(keys.Length);
        for (var slot = 0; slot < keys.Length; slot++)
        {
            run[indicator.Outputs[slot]].ToArray().Should().Equal(
                batch.OutputValues[keys[slot]].ToArray(), family + "'s " + keys[slot] + " is published on its own key");
        }

        // A level belongs to a period, so it holds across that period's bars; and the supports sit below
        // the pivot with the resistances above it, nesting outward. Every key used to carry the pivot,
        // which holds none of this.
        var pivot = run[indicator.Outputs[0]].ToArray();
        pivot.Distinct().Should().HaveCountLessThan(pivot.Length / 2);

        var support1 = run[indicator.Outputs[Array.IndexOf(keys, "S1")]].ToArray();
        var support2 = run[indicator.Outputs[Array.IndexOf(keys, "S2")]].ToArray();
        var resistance1 = run[indicator.Outputs[Array.IndexOf(keys, "R1")]].ToArray();
        var resistance2 = run[indicator.Outputs[Array.IndexOf(keys, "R2")]].ToArray();
        for (var i = 0; i < pivot.Length; i++)
        {
            support2[i].Should().BeLessThanOrEqualTo(support1[i]);
            support1[i].Should().BeLessThanOrEqualTo(resistance1[i]);
            resistance1[i].Should().BeLessThanOrEqualTo(resistance2[i]);
        }
    }

    [Fact]
    public void CamarillaPublishesAllSeventeenOfItsLevels()
    {
        var bars = Walk(400);
        var camarilla = new CamarillaPivotPoint();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(camarilla)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateCamarillaPivotPoints();

        var keys = new[] { "Pivot", "S1", "S2", "S3", "S4", "S5", "R1", "R2", "R3", "R4", "R5", "M1", "M2", "M3", "M4", "M5", "M6" };
        camarilla.Outputs.Should().HaveCount(keys.Length);

        for (var slot = 0; slot < keys.Length; slot++)
        {
            run[camarilla.Outputs[slot]].ToArray().Should().Equal(
                batch.OutputValues[keys[slot]].ToArray(), "the " + keys[slot] + " level is published on its own key");
        }

        // The supports and resistances fan out symmetrically around the close, so the bands are ordered.
        // Every one of these keys used to carry the pivot, which satisfies no ordering at all.
        var s1 = run[camarilla.S1].ToArray();
        var s4 = run[camarilla.S4].ToArray();
        var r1 = run[camarilla.R1].ToArray();
        var r4 = run[camarilla.R4].ToArray();
        for (var i = 0; i < s1.Length; i++)
        {
            s4[i].Should().BeLessThanOrEqualTo(s1[i]);
            r1[i].Should().BeLessThanOrEqualTo(r4[i]);
            s1[i].Should().BeLessThanOrEqualTo(r1[i]);
        }
    }

    [Fact]
    public void DemarkPivotsAreComputedPerPeriodAndNotPerBar()
    {
        var bars = Walk(400);
        var demark = new DemarkPivotPoint();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(demark)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateDemarkPivotPoints();

        var pivot = run[demark.Value].ToArray();
        var support = run[demark.S1].ToArray();
        var resistance = run[demark.R1].ToArray();

        pivot.Should().Equal(batch.OutputValues["Pivot"].ToArray());
        support.Should().Equal(batch.OutputValues["S1"].ToArray());
        resistance.Should().Equal(batch.OutputValues["R1"].ToArray());

        // A level belongs to a period, so it holds across that period's bars rather than moving on every
        // one of them. The routine this replaced recomputed it per bar, which no amount of comparing the
        // named keys against the primary could have caught - all three were wrong together.
        pivot.Distinct().Should().HaveCountLessThan(pivot.Length / 2);

        // Resistance sits above support, because it subtracts the period's low where support subtracts its
        // high, and they were both being answered with the pivot.
        for (var i = 0; i < pivot.Length; i++)
        {
            resistance[i].Should().BeGreaterThanOrEqualTo(support[i]);
        }
    }

    [Fact]
    public void TheErgodicMacdPublishesItsSignalAndHistogram()
    {
        var bars = Walk(300);
        var ergodic = new ErgodicMovingAverageConvergenceDivergence();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(ergodic)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateErgodicMovingAverageConvergenceDivergence();

        var macd = run[ergodic.Value].ToArray();
        var signal = run[ergodic.Signal].ToArray();
        var histogram = run[ergodic.Histogram].ToArray();

        macd.Should().Equal(batch.OutputValues["Macd"].ToArray());
        signal.Should().Equal(batch.OutputValues["Signal"].ToArray());
        histogram.Should().Equal(batch.OutputValues["Histogram"].ToArray());

        // The histogram is the line less its own smoothing, which is zero for every bar when one series
        // answers all three keys - so the relation is checked together with the line and signal differing.
        for (var i = 0; i < macd.Length; i++)
        {
            histogram[i].Should().BeApproximately(macd[i] - signal[i], 1e-9);
        }

        macd.Should().NotEqual(signal);
    }

    [Fact]
    public void TrenderPublishesBothStopsAndNotTheLineThreeTimes()
    {
        var bars = Walk(300);
        var trender = new Trender();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(trender)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateTrender(length: new Trender().Length);

        var up = run[trender.TrendUp].ToArray();
        var down = run[trender.TrendDn].ToArray();
        var line = run[trender.Value].ToArray();

        up.Should().Equal(batch.OutputValues["TrendUp"].ToArray());
        down.Should().Equal(batch.OutputValues["TrendDn"].ToArray());
        line.Should().Equal(batch.OutputValues["Trender"].ToArray());

        // The line alternates between the two stops, so on every bar it equals one of them. When all three
        // keys carried the line that held trivially, so it is checked alongside the batch rather than alone:
        // the stops must also differ from each other somewhere, which a single repeated series cannot do.
        for (var i = 0; i < line.Length; i++)
        {
            (Math.Abs(line[i] - up[i]) < 1e-9 || Math.Abs(line[i] - down[i]) < 1e-9).Should().BeTrue();
        }

        up.Should().NotEqual(down);
    }

    [Fact]
    public void TheSimpleDecyclerPublishesItsBandsAndNotTheDecyclerThreeTimes()
    {
        var bars = Walk(400);
        var decycler = new EhlersSimpleDecycler();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(decycler)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateEhlersSimpleDecycler(length: new EhlersSimpleDecycler().Length);

        var upper = run[decycler.UpperBand].ToArray();
        var middle = run[decycler.Value].ToArray();
        var lower = run[decycler.LowerBand].ToArray();

        upper.Should().Equal(batch.OutputValues["UpperBand"].ToArray());
        middle.Should().Equal(batch.OutputValues["MiddleBand"].ToArray());
        lower.Should().Equal(batch.OutputValues["LowerBand"].ToArray());

        // The bands are the decycler scaled by half a percent either way, so each sits at a fixed ratio to
        // the middle rather than a fixed distance. All three keys used to carry the middle, which satisfies
        // no ratio but one.
        for (var i = 0; i < middle.Length; i++)
        {
            upper[i].Should().BeApproximately(middle[i] * 1.005, 1e-9);
            lower[i].Should().BeApproximately(middle[i] * 0.995, 1e-9);
        }
    }

    [Fact]
    public void TheTwoKaufmanDerivedAveragesPublishTheirDiagnosticSeries()
    {
        var bars = Walk(200);
        var powered = new PoweredKaufmanAdaptiveMovingAverage();
        var autonomous = new AdaptiveAutonomousRecursiveMovingAverage();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(powered, autonomous)
            .BuildAsync().GetAwaiter().GetResult();

        StockData Batch() => new(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList());

        var poweredBatch = Batch().CalculatePoweredKaufmanAdaptiveMovingAverage(length: new PoweredKaufmanAdaptiveMovingAverage().Length);
        var per = run[powered.Per].ToArray();
        per.Should().Equal(poweredBatch.OutputValues["Per"].ToArray());
        run[powered.Value].ToArray().Should().Equal(poweredBatch.OutputValues["Pkama"].ToArray());

        // The powered efficiency ratio is a fraction raised to a power, so it stays within the unit
        // interval where the average tracks price.
        per.Should().OnlyContain(x => x >= 0 && x <= 1);

        var autonomousBatch = Batch().CalculateAdaptiveAutonomousRecursiveMovingAverage(
            length: new AdaptiveAutonomousRecursiveMovingAverage().Length);
        var deviation = run[autonomous.D].ToArray();
        deviation.Should().Equal(autonomousBatch.OutputValues["D"].ToArray());
        run[autonomous.Value].ToArray().Should().Equal(autonomousBatch.OutputValues["Aarma"].ToArray());

        // A band width is a distance, so it is never negative - which the average it used to answer with is
        // no guarantee of.
        deviation.Should().OnlyContain(x => x >= 0);
    }

    [Fact]
    public void KamaPublishesItsEfficiencyRatioAndNotTheAverageTwice()
    {
        var bars = Walk(150);
        var kama = new Kama();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(kama)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateKaufmanAdaptiveMovingAverage(length: new Kama().Length);

        var er = run[kama.Er].ToArray();

        er.Should().Equal(batch.OutputValues["Er"].ToArray());
        run[kama.Value].ToArray().Should().Equal(batch.OutputValues["Kama"].ToArray());

        // The efficiency ratio is a fraction of the distance walked, so it is bounded where the average
        // tracks price. The builder used to answer this key with the average.
        er.Should().OnlyContain(x => x >= 0 && x <= 1);
    }

    [Fact]
    public void ConnorsRsiPublishesItsThreePartsAndNotTheirAverageFourTimes()
    {
        var bars = Walk(200);
        var connors = new ConnorsRsi();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(connors)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateConnorsRelativeStrengthIndex(length2: new ConnorsRsi().Length);

        var rsi = run[connors.Rsi].ToArray();
        var pctRank = run[connors.PctRank].ToArray();
        var streakRsi = run[connors.StreakRsi].ToArray();
        var value = run[connors.Value].ToArray();

        rsi.Should().Equal(batch.OutputValues["Rsi"].ToArray());
        pctRank.Should().Equal(batch.OutputValues["PctRank"].ToArray());
        streakRsi.Should().Equal(batch.OutputValues["StreakRsi"].ToArray());
        value.Should().Equal(batch.OutputValues["ConnorsRsi"].ToArray());

        // The published series is the average of the three parts, so all four keys carrying one series -
        // which is what the builder used to answer - is arithmetically impossible.
        for (var i = 0; i < value.Length; i++)
        {
            value[i].Should().BeApproximately(
                Math.Clamp((rsi[i] + pctRank[i] + streakRsi[i]) / 3, 0, 100), 1e-9);
        }
    }

    [Fact]
    public void TrimeanPublishesItsThreeQuartilesAndNotTheTrimeanFourTimes()
    {
        var bars = Walk(150);
        var trimean = new Trimean();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(trimean)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateTrimean();

        var value = run[trimean.Value].ToArray();
        var q1 = run[trimean.Q1].ToArray();
        var median = run[trimean.Median].ToArray();
        var q3 = run[trimean.Q3].ToArray();

        value.Should().Equal(batch.OutputValues["Trimean"].ToArray());
        q1.Should().Equal(batch.OutputValues["Q1"].ToArray());
        median.Should().Equal(batch.OutputValues["Median"].ToArray());
        q3.Should().Equal(batch.OutputValues["Q3"].ToArray());

        // The trimean weights the median double against the two quartiles, so it cannot equal any of them
        // across the series - which is what the builder used to return for all four keys.
        for (var i = 0; i < value.Length; i++)
        {
            value[i].Should().BeApproximately((q1[i] + (2 * median[i]) + q3[i]) / 4, 1e-9);
        }
    }

    [Fact]
    public void AdxPublishesItsTwoDirectionalIndicatorsAndNotTheAdxLineThreeTimes()
    {
        var bars = Walk(150);
        var adx = new Adx();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(adx)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateAverageDirectionalIndex();

        run[adx.DiPlus].ToArray().Should().Equal(batch.OutputValues["DiPlus"].ToArray());
        run[adx.DiMinus].ToArray().Should().Equal(batch.OutputValues["DiMinus"].ToArray());
        run[adx.Value].ToArray().Should().Equal(batch.OutputValues["Adx"].ToArray());
    }

    [Fact]
    public void AroonPublishesItsTwoLegsAndNotTheOscillatorThreeTimes()
    {
        var bars = Walk(150);
        var aroon = new Aroon();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(aroon)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateAroonOscillator();

        var value = run[aroon.Value].ToArray();
        var up = run[aroon.AroonUp].ToArray();
        var down = run[aroon.AroonDown].ToArray();

        value.Should().Equal(batch.OutputValues["Aroon"].ToArray());
        up.Should().Equal(batch.OutputValues["AroonUp"].ToArray());
        down.Should().Equal(batch.OutputValues["AroonDown"].ToArray());

        // The oscillator is the difference of the two legs, so all three carrying one series - which is
        // what the builder used to answer - is arithmetically impossible rather than merely imprecise.
        for (var i = 0; i < value.Length; i++)
        {
            value[i].Should().BeApproximately(up[i] - down[i], 1e-9);
        }
    }

    [Fact]
    public void TheStochasticRsiSignalIsTheSecondSmoothingAndNotTheFirst()
    {
        var bars = Walk(150);
        var srsi = new StochasticRelativeStrengthIndex();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(srsi)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateStochasticRelativeStrengthIndex();

        var series = run[srsi.StochRsi].ToArray();
        var signal = run[srsi.Signal].ToArray();

        // FastD is the first smoothing of the stochastic and SlowD the second, so the signal is a smoothing
        // of the series rather than the series itself.
        series.Should().Equal(batch.OutputValues["StochRsi"].ToArray());
        signal.Should().Equal(batch.OutputValues["Signal"].ToArray());
        signal.Should().NotEqual(series, "the second smoothing is not the first");
    }
}
