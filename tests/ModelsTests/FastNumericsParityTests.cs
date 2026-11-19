using OoplesFinance.StockIndicators.Builder;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Indicators.Fast;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// Holds every fast kernel to the exact route: same published series to within floating-point rounding, on
/// every output, from both source shapes and in both history modes.
/// </summary>
[Collection("IndicatorValuesDispatch")]
public sealed class FastNumericsParityTests
{
    // Relative to the larger of the exact value and the price scale, so a value that is legitimately near
    // zero is not held to a relative bound it cannot meet.
    private const double Tolerance = 1e-9;

    /// <summary>One configuration per kernel branch. Every type with a fast kernel must appear here.</summary>
    public static IEnumerable<(string Name, Func<IIndicator> Create)> Cases() =>
    [
        ("Sma(1)", () => new Sma(1)),
        ("Sma(20)", () => new Sma(20)),
        ("Sma(400)", () => new Sma(400)),
        ("FirstValueEma(1)", () => new FirstValueEma(1)),
        ("FirstValueEma(20)", () => new FirstValueEma(20)),
        ("SeededExponentialAverage(20,2,Shared)", () => new SeededExponentialAverage(20, 2, ExponentialSeedMode.Shared)),
        ("SeededExponentialAverage(20,3,Shared)", () => new SeededExponentialAverage(20, 3, ExponentialSeedMode.Shared)),
        ("SeededExponentialAverage(20,2,Cascaded)", () => new SeededExponentialAverage(20, 2, ExponentialSeedMode.Cascaded)),
        ("SeededExponentialAverage(5,3,Cascaded)", () => new SeededExponentialAverage(5, 3, ExponentialSeedMode.Cascaded)),
        ("SeededExponentialAverage(1,3,Cascaded)", () => new SeededExponentialAverage(1, 3, ExponentialSeedMode.Cascaded)),
        ("ClassicMovingAverage(1,Sma)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(2,Sma)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(9,Sma)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(30,Sma)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(14,Sma,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Sma, true, 3)),
        ("ClassicMovingAverage(14,Sma,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Sma, false, 5)),
        ("ClassicPriceOscillator(12,26,Sma)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Sma)),
        ("ClassicPriceOscillator(12,26,Sma,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Sma, true)),
        ("ClassicPriceOscillator(5,9,Sma,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Sma, true, true, 2)),
        ("ClassicMacd(12,26,9,Sma)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Sma, ClassicAverageMethod.Sma, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(1,Ema)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(2,Ema)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(9,Ema)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(30,Ema)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(14,Ema,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Ema, true, 3)),
        ("ClassicMovingAverage(14,Ema,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Ema, false, 5)),
        ("ClassicPriceOscillator(12,26,Ema)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Ema)),
        ("ClassicPriceOscillator(12,26,Ema,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Ema, true)),
        ("ClassicPriceOscillator(5,9,Ema,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Ema, true, true, 2)),
        ("ClassicMacd(12,26,9,Ema)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Ema, ClassicAverageMethod.Ema, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(1,Wma)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(2,Wma)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(9,Wma)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(30,Wma)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(14,Wma,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Wma, true, 3)),
        ("ClassicMovingAverage(14,Wma,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Wma, false, 5)),
        ("ClassicPriceOscillator(12,26,Wma)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Wma)),
        ("ClassicPriceOscillator(12,26,Wma,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Wma, true)),
        ("ClassicPriceOscillator(5,9,Wma,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Wma, true, true, 2)),
        ("ClassicMacd(12,26,9,Wma)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Wma, ClassicAverageMethod.Wma, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(1,Dema)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(2,Dema)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(9,Dema)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(30,Dema)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(14,Dema,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Dema, true, 3)),
        ("ClassicMovingAverage(14,Dema,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Dema, false, 5)),
        ("ClassicPriceOscillator(12,26,Dema)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Dema)),
        ("ClassicPriceOscillator(12,26,Dema,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Dema, true)),
        ("ClassicPriceOscillator(5,9,Dema,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Dema, true, true, 2)),
        ("ClassicMacd(12,26,9,Dema)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Dema, ClassicAverageMethod.Dema, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(1,Tema)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(2,Tema)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(9,Tema)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(30,Tema)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(14,Tema,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Tema, true, 3)),
        ("ClassicMovingAverage(14,Tema,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Tema, false, 5)),
        ("ClassicPriceOscillator(12,26,Tema)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Tema)),
        ("ClassicPriceOscillator(12,26,Tema,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Tema, true)),
        ("ClassicPriceOscillator(5,9,Tema,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Tema, true, true, 2)),
        ("ClassicMacd(12,26,9,Tema)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Tema, ClassicAverageMethod.Tema, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(1,Trima)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Trima)),
        ("ClassicMovingAverage(2,Trima)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Trima)),
        ("ClassicMovingAverage(9,Trima)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Trima)),
        ("ClassicMovingAverage(30,Trima)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Trima)),
        ("ClassicMovingAverage(14,Trima,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Trima, true, 3)),
        ("ClassicMovingAverage(14,Trima,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Trima, false, 5)),
        ("ClassicPriceOscillator(12,26,Trima)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Trima)),
        ("ClassicPriceOscillator(12,26,Trima,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Trima, true)),
        ("ClassicPriceOscillator(5,9,Trima,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Trima, true, true, 2)),
        ("ClassicMacd(12,26,9,Trima)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Trima, ClassicAverageMethod.Trima, ClassicAverageMethod.Trima)),
        ("ClassicMacd(mixed)", () => new ClassicMacd(26, 12, 9, ClassicAverageMethod.Ema, ClassicAverageMethod.Wma, ClassicAverageMethod.Tema)),
        ("ClassicMacd(first,supp)", () => new ClassicMacd(5, 13, 4, ClassicAverageMethod.Ema, ClassicAverageMethod.Dema, ClassicAverageMethod.Ema, true, 2)),
        ("ClassicMacd(supp)", () => new ClassicMacd(5, 13, 4, ClassicAverageMethod.Tema, ClassicAverageMethod.Ema, ClassicAverageMethod.Trima, false, 3)),
        ("SeededAverageTrueRange(1)", () => new SeededAverageTrueRange(1)),
        ("SeededAverageTrueRange(14)", () => new SeededAverageTrueRange(14)),
        ("NormalizedSeededAverageTrueRange(1)", () => new NormalizedSeededAverageTrueRange(1)),
        ("NormalizedSeededAverageTrueRange(14)", () => new NormalizedSeededAverageTrueRange(14)),
        ("SmoothedAccumulationOscillator()", () => new SmoothedAccumulationOscillator()),
        ("SmoothedAccumulationOscillator(details)", () => new SmoothedAccumulationOscillator(3, 10, false, 0, true)),
        ("SmoothedAccumulationOscillator(first,supp,details)", () => new SmoothedAccumulationOscillator(10, 3, true, 4, true)),
        ("SmoothedAccumulationOscillator(1,1)", () => new SmoothedAccumulationOscillator(1, 1)),
        ("WilderStrengthOscillator(14)", () => new WilderStrengthOscillator(14)),
        ("WilderStrengthOscillator(1)", () => new WilderStrengthOscillator(1)),
        ("WilderStrengthOscillator(14,HundredFlat)", () => new WilderStrengthOscillator(14, WilderStrengthConvention.RsiHundredFlat)),
        ("WilderStrengthOscillator(9,Chande,unstable5)", () => new WilderStrengthOscillator(9, WilderStrengthConvention.ChandeZeroFlat, 5)),
        ("TillsonAverage(1,.7,FirstPrice,0)", () => new TillsonAverage(1, .7, TillsonSeed.FirstPrice, 0)),
        ("TillsonAverage(5,.7,FirstPrice,0)", () => new TillsonAverage(5, .7, TillsonSeed.FirstPrice, 0)),
        ("TillsonAverage(14,.7,FirstPrice,3)", () => new TillsonAverage(14, .7, TillsonSeed.FirstPrice, 3)),
        ("TillsonAverage(1,.7,FollowingPrefix,0)", () => new TillsonAverage(1, .7, TillsonSeed.FollowingPrefix, 0)),
        ("TillsonAverage(5,.7,FollowingPrefix,0)", () => new TillsonAverage(5, .7, TillsonSeed.FollowingPrefix, 0)),
        ("TillsonAverage(14,.7,FollowingPrefix,3)", () => new TillsonAverage(14, .7, TillsonSeed.FollowingPrefix, 3)),
        ("TillsonAverage(1,.7,CascadedMeans,0)", () => new TillsonAverage(1, .7, TillsonSeed.CascadedMeans, 0)),
        ("TillsonAverage(5,.7,CascadedMeans,0)", () => new TillsonAverage(5, .7, TillsonSeed.CascadedMeans, 0)),
        ("TillsonAverage(14,.7,CascadedMeans,3)", () => new TillsonAverage(14, .7, TillsonSeed.CascadedMeans, 3)),
        ("TillsonAverage(8,.4,CascadedMeans)", () => new TillsonAverage(8, .4, TillsonSeed.CascadedMeans)),
        ("PriceRoundingTransform(Ceiling)", () => new PriceRoundingTransform(PriceRoundingOperation.Ceiling)),
        ("PriceRoundingTransform(Floor)", () => new PriceRoundingTransform(PriceRoundingOperation.Floor)),
        ("PriceRoundingTransform(SquareRoot)", () => new PriceRoundingTransform(PriceRoundingOperation.SquareRoot)),
        ("PriceCircularTransform(Sine)", () => new PriceCircularTransform(PriceCircularOperation.Sine)),
        ("PriceCircularTransform(Cosine)", () => new PriceCircularTransform(PriceCircularOperation.Cosine)),
        ("PriceCircularTransform(Tangent)", () => new PriceCircularTransform(PriceCircularOperation.Tangent)),
        ("PriceCircularTransform(ArcSine)", () => new PriceCircularTransform(PriceCircularOperation.ArcSine)),
        ("PriceCircularTransform(ArcCosine)", () => new PriceCircularTransform(PriceCircularOperation.ArcCosine)),
        ("PriceCircularTransform(ArcTangent)", () => new PriceCircularTransform(PriceCircularOperation.ArcTangent)),
        ("PriceTranscendentalTransform(NaturalLogarithm)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.NaturalLogarithm)),
        ("PriceTranscendentalTransform(CommonLogarithm)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.CommonLogarithm)),
        ("PriceTranscendentalTransform(Exponential)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.Exponential)),
        ("PriceTranscendentalTransform(HyperbolicSine)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicSine)),
        ("PriceTranscendentalTransform(HyperbolicCosine)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicCosine)),
        ("PriceTranscendentalTransform(HyperbolicTangent)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicTangent)),
        ("TripleExponentialRate()", () => new TripleExponentialRate()),
        ("TripleExponentialRate(1,shared,signal 1)", () => new TripleExponentialRate(1, TripleRateSeed.SharedMean, 1)),
        ("TripleExponentialRate(9,shared,signal 5)", () => new TripleExponentialRate(9, TripleRateSeed.SharedMean, 5)),
        ("TripleExponentialRate(5,means,signal 3,supp 2)", () => new TripleExponentialRate(5, TripleRateSeed.CascadedMeans, 3, 2)),
        ("TripleExponentialRate(14,means)", () => new TripleExponentialRate(14, TripleRateSeed.CascadedMeans)),
        ("TripleExponentialRate(1,means,signal 2)", () => new TripleExponentialRate(1, TripleRateSeed.CascadedMeans, 2)),
        ("TripleExponentialRate(6,first,signal 4)", () => new TripleExponentialRate(6, TripleRateSeed.CascadedFirstPrice, 4)),
        ("TripleExponentialRate(3,first,supp 4)", () => new TripleExponentialRate(3, TripleRateSeed.CascadedFirstPrice, null, 4)),
        ("Directional(PositiveMovement)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveMovement)),
        ("Directional(PositiveMovement,1)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveMovement, 1)),
        ("Directional(PositiveMovement,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveMovement, 5, 3)),
        ("Directional(NegativeMovement)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeMovement)),
        ("Directional(NegativeMovement,1)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeMovement, 1)),
        ("Directional(NegativeMovement,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeMovement, 5, 3)),
        ("Directional(PositiveIndicator)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveIndicator)),
        ("Directional(PositiveIndicator,1)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveIndicator, 1)),
        ("Directional(PositiveIndicator,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveIndicator, 5, 3)),
        ("Directional(NegativeIndicator)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeIndicator)),
        ("Directional(NegativeIndicator,1)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeIndicator, 1)),
        ("Directional(NegativeIndicator,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeIndicator, 5, 3)),
        ("Directional(Index)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Index)),
        ("Directional(Index,2)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Index, 2)),
        ("Directional(Index,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Index, 5, 3)),
        ("Directional(Index,9, 20)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Index, 9, 20)),
        ("Directional(Average)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average)),
        ("Directional(Average,2)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average, 2)),
        ("Directional(Average,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average, 5, 3)),
        ("Directional(Average,9, 20)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average, 9, 20)),
        ("Directional(Rating)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Rating)),
        ("Directional(Rating,2)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Rating, 2)),
        ("Directional(Rating,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Rating, 5, 3)),
        ("Directional(Rating,9, 20)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Rating, 9, 20)),
        ("AlignedEmaMacd()", () => new AlignedEmaMacd()),
        ("AlignedEmaMacd(fixed)", () => new AlignedEmaMacd(12, 26, 9, false, 0, true)),
        ("AlignedEmaMacd(first,supp)", () => new AlignedEmaMacd(5, 13, 4, true, 3)),
        ("AlignedEmaMacd(supp,selection)", () => new AlignedEmaMacd(5, 13, 1, false, 2, false, EmaDifferenceSelection.Oscillator | EmaDifferenceSelection.Histogram)),
    ];

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var (name, _) in Cases()) data.Add(name);
        return data;
    }

    [Fact]
    public void EveryFastKernelTypeHasAParityCase()
    {
        var covered = Cases().Select(c => c.Create().GetType()).ToHashSet();
        var missing = FastExecution.KernelTypes.Where(type => !covered.Contains(type)).Select(type => type.Name).ToArray();
        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task FastAgreesWithExactOnEveryOutput(string name)
    {
        var create = Cases().Single(c => c.Name == name).Create;
        foreach (var (fixture, bars) in Fixtures())
        {
            var exactIndicator = create();
            using var exact = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(exactIndicator).BuildAsync();
            foreach (var columnar in new[] { false, true })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = create();
                var source = columnar
                    ? Bars.FromColumns(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
                        bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(),
                        bars.Select(b => b.Volume).ToArray(), bars.Select(b => b.Time).ToArray())
                    : Bars.From(bars);
                var builder = new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(indicator)
                    .ConfigureNumerics(IndicatorNumerics.Fast).ConfigureHistory(history);
                using var fast = await builder.BuildAsync();
                var where = $"{name} / {fixture} / columnar={columnar} / {history}";
                Assert.True(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal), where + ": did not take the fast route");
                Assert.Equal(bars.Length, fast.BarCount);
                Assert.Equal(exactIndicator.Outputs.Count, indicator.Outputs.Count);
                for (var slot = 0; slot < indicator.Outputs.Count; slot++)
                {
                    var expected = exact[exactIndicator.Outputs[slot]];
                    var actual = fast[indicator.Outputs[slot]];
                    Assert.Equal(expected.Length, actual.Length);
                    for (var i = 0; i < expected.Length; i++)
                    {
                        if (double.IsNaN(expected[i]))
                        {
                            Assert.True(double.IsNaN(actual[i]), $"{where}: slot {slot}, bar {i} expected NaN, was {actual[i]:R}");
                            continue;
                        }
                        var scale = Math.Max(Math.Abs(expected[i]), Math.Abs(bars[i].Close));
                        Assert.True(Math.Abs(expected[i] - actual[i]) <= Tolerance * Math.Max(scale, 1e-300),
                            $"{where}: slot {slot}, bar {i} expected {expected[i]:R}, was {actual[i]:R}");
                    }
                }
                if (bars.Length != 0)
                    Assert.Equal(bars[^1].Close, fast.Latest.Bar.Close);
            }
        }
    }

    [Fact]
    public async Task FastValidatesOnlyTheFieldsItReads()
    {
        var bars = Walk(64, 11);
        bars[10] = new Bar(bars[10].Time, bars[10].Open, bars[10].High, bars[10].Low, bars[10].Close, double.NaN);
        var sma = new Sma(5);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(sma)
            .ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync();
        Assert.Equal(bars.Length, run[sma].Length);

        bars[12] = new Bar(bars[12].Time, bars[12].Open, bars[12].High, bars[12].Low, double.PositiveInfinity, 1);
        foreach (var columnar in new[] { false, true })
        {
            var source = columnar
                ? Bars.FromColumns(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
                    bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray())
                : Bars.From(bars);
            var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder()
                .ConfigureSource(source).ConfigureIndicators(new Sma(5)).ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync());
            Assert.Contains("Close must be finite.", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ExactStaysTheDefaultAndUnsupportedBuildsKeepTheExactRoute()
    {
        var bars = Walk(64, 3);
        var plain = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new Sma(5));
        using (await plain.BuildAsync())
            Assert.False(plain.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));

        // An indicator reading another indicator's series has no fast kernel, so the whole build stays exact.
        var chained = new Sma(5).Of(new Sma(3));
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(chained)
            .ConfigureNumerics(IndicatorNumerics.Fast);
        using (await builder.BuildAsync())
            Assert.False(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FastFailuresArriveThroughTheTask()
    {
        var bars = Walk(32, 7);
        bars[4] = new Bar(bars[4].Time, 1, 1, 1, double.NaN, 1);
        // Calling BuildAsync must not throw; the failure belongs to the task it returns.
        var failing = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new Sma(3))
            .ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => failing);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopped = new StockIndicatorBuilder().ConfigureSource(Bars.From(Walk(32, 7))).ConfigureIndicators(new Sma(3))
            .ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync(cancelled.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopped);
        Assert.True(stopped.IsCanceled);
    }

    [Fact]
    public async Task FastRejectsANonFiniteOutputExactlyAsTheExactRouteDoes()
    {
        // exp(1000) overflows on bar 5 only.
        var bars = Walk(16, 9);
        bars[5] = new Bar(bars[5].Time, 1000, 1000, 1000, 1000, 1);
        Task<IIndicatorRun> Build(IndicatorNumerics numerics) => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new PriceTranscendentalTransform(PriceTranscendentalOperation.Exponential))
            .ConfigureNumerics(numerics).BuildAsync();
        var exact = await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(IndicatorNumerics.Exact));
        var fast = await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(IndicatorNumerics.Fast));
        Assert.Equal(exact.Message, fast.Message);
        Assert.Equal(5, fast.BarIndex);
    }

    [Fact]
    public async Task ColumnSourceServesTheExactRouteAndReplay()
    {
        var bars = Walk(120, 13);
        IBarSource Columns() => Bars.FromColumns(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(),
            bars.Select(b => b.Time).ToArray());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var fromBars = new SeededAverageTrueRange(14);
            var fromColumns = new SeededAverageTrueRange(14);
            using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(fromBars).ConfigureHistory(history).BuildAsync();
            var builder = new StockIndicatorBuilder().ConfigureSource(Columns()).ConfigureIndicators(fromColumns)
                .ConfigureHistory(history);
            using var actual = await builder.BuildAsync();
            Assert.False(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
            Assert.Equal(expected[fromBars].ToArray(), actual[fromColumns].ToArray());
        }

        var replayed = new Sma(5);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Columns()).ConfigureIndicators(replayed)
            .ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync();
        var index = 0;
        await foreach (var snapshot in run)
        {
            Assert.Equal(bars[index].Close, snapshot.Bar.Close);
            Assert.Equal(bars[index].Time, snapshot.Bar.Time);
            Assert.Equal(run[replayed][index], snapshot[replayed]);
            index++;
        }
        Assert.Equal(bars.Length, index);

        await Assert.ThrowsAsync<ArgumentException>(() => Task.Run(() => Bars.FromColumns(new double[2], new double[2],
            new double[2], new double[3], new double[2])));
    }

    [Fact]
    public async Task AKernelThatDoesNotCoverAConfigurationLeavesItToTheExactRoute()
    {
        var bars = Walk(200, 19);
        var exactIndicator = new ClassicMovingAverage(10, ClassicAverageMethod.Kama);
        var fastIndicator = new ClassicMovingAverage(10, ClassicAverageMethod.Kama);
        using var exact = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(exactIndicator).BuildAsync();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(fastIndicator)
            .ConfigureNumerics(IndicatorNumerics.Fast);
        using var fast = await builder.BuildAsync();
        Assert.False(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
        Assert.Equal(exact[exactIndicator].ToArray(), fast[fastIndicator].ToArray());
    }

    [Fact]
    public async Task ALegacyBuildAfterAFastBuildReadsTheSameSource()
    {
        var bars = Enumerable.Range(0, 6).Select(i => new Bar(Day(i), i + 1, i + 1, i + 1, i + 1, 1)).ToArray();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new Sma(3))
            .ConfigureNumerics(IndicatorNumerics.Fast);
        using var typed = await builder.BuildAsync();
        Assert.True(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
        SeriesHandle average = default;
        using var legacy = builder.ConfigureIndicators(catalog => average = catalog.Sma(3)).Build();
        legacy.Start();
        Assert.Equal(new[] { 0d, 0d, 2d, 3d, 4d, 5d }, legacy.GetSeries(average).ToArray());
    }

    private static IEnumerable<(string Name, Bar[] Bars)> Fixtures()
    {
        yield return ("empty", []);
        yield return ("one bar", Walk(1, 5));
        yield return ("shorter than most windows", Walk(7, 5));
        yield return ("walk 300", Walk(300, 17));
        yield return ("walk 2000", Walk(2000, 29));
        yield return ("signed unit range", Walk(400, 41).Select((bar, i) =>
        {
            // Closes in about [-1.6, 1.6], so inverse circular functions, logarithms and square roots meet
            // both sides of their domains. The close is kept off the middle of the range: at the exact middle
            // a money-flow multiplier is zero in real arithmetic, and all that is left to compare is the
            // rounding of the inputs.
            var close = Math.Round(1.6 * Math.Sin(i * 0.37) + (bar.Close % 1 - .5) * .1, 4);
            return new Bar(bar.Time, close - .05, close + .3, close - .2, close, bar.Volume);
        }).ToArray());
        // Zero prices make every ratio zero over zero, which each indicator defines for itself.
        yield return ("zero", Enumerable.Range(0, 60).Select(i => new Bar(Day(i), 0, 0, 0, 0, 0)).ToArray());
        yield return ("flat", Enumerable.Range(0, 250).Select(i => new Bar(Day(i), 50, 50, 50, 50, 1000)).ToArray());
    }

    private static DateTime Day(int index) => new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index);

    /// <summary>A deterministic cent-grid random walk with consistent OHLC and varying volume.</summary>
    private static Bar[] Walk(int count, int seed)
    {
        var random = RandomHelper.CreateSeededRandom(seed);
        var bars = new Bar[count];
        double close = 100;
        for (var i = 0; i < count; i++)
        {
            var open = close;
            close = Math.Round(Math.Max(1, close + (random.NextDouble() - .5) * 2), 2);
            var high = Math.Round(Math.Max(open, close) + random.NextDouble(), 2);
            var low = Math.Round(Math.Max(.01, Math.Min(open, close) - random.NextDouble()), 2);
            bars[i] = new Bar(Day(i), open, high, low, close, 1000 + random.Next(0, 5000));
        }
        return bars;
    }
}
