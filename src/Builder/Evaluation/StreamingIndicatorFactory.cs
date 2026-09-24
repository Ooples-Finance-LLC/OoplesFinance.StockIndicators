using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Factory for creating streaming indicator states.
/// </summary>
internal static class StreamingIndicatorFactory
{
    /// <summary>
    /// Creates a streaming indicator state for the given spec.
    /// </summary>
    public static IStreamingIndicatorState? CreateState(IndicatorSpec spec)
    {
        return spec.Name switch
        {
            IndicatorName.MarketFacilitationIndex => new MarketFacilitationIndexState(),
            IndicatorName.TrueRange => new TrueRangeState(),
            IndicatorName.Range => new RangeState(),
            IndicatorName.MoneyFlowIndex when spec.Options is MfiSpecOptions mfi
                => new MoneyFlowIndexState(mfi.Length),
            IndicatorName.MoneyFlowIndex when spec.Options is MfiCoreSpecOptions mfiCore
                => new MoneyFlowIndexState(mfiCore.Length),
            IndicatorName.LogReturns when spec.Options is LogReturnsSpecOptions logReturns
                => new LogReturnsState(logReturns.Length),
            IndicatorName.TFSTetherLineIndicator when spec.Options is TFSTetherLineIndicatorSpecOptions tether => new TFSTetherLineIndicatorState(tether.Length),
            IndicatorName.TFSTetherLineIndicator when spec.Options is TFSTetherLineSpecOptions tetherAlias => new TFSTetherLineIndicatorState(tetherAlias.Length),
            IndicatorName.RangeIdentifier when spec.Options is RangeIdentifierSpecOptions rangeIdentifier
                => new RangeIdentifierState(rangeIdentifier.Length),
            IndicatorName.MovingAverageEnvelope when spec.Options is MovingAverageEnvelopeSpecOptions envelope
                => new MovingAverageEnvelopeState(envelope.MaType, envelope.Length, envelope.Pct),
            IndicatorName.PriceChannel when spec.Options is PriceChannelSpecOptions priceChannel
                => new PriceChannelState(priceChannel.MaType, priceChannel.Length, priceChannel.Pct),
            IndicatorName.PriceChannel when spec.Options is PriceChannelMiddleSpecOptions priceMiddle => new PriceChannelState(length: priceMiddle.Length),
            IndicatorName.PriceChannel when spec.Options is PriceChannelUpperSpecOptions priceUpper => new PriceChannelState(length: priceUpper.Length),
            IndicatorName.PriceChannel when spec.Options is PriceChannelLowerSpecOptions priceLower => new PriceChannelState(length: priceLower.Length),
            IndicatorName.MoveTracker => new MoveTrackerState(),
            IndicatorName.AverageDayRange when spec.Options is AdrSpecOptions adr
                => new AverageDayRangeState(adr.Length),
            IndicatorName.AverageDayRange when spec.Options is AverageDayRangeSpecOptions dayRange
                => new AverageDayRangeState(dayRange.Length),
            IndicatorName.OnBalanceVolume when spec.Options is ObvSpecOptions obv
                => new OnBalanceVolumeState(obv.Length, obv.MaType),
            IndicatorName.OnBalanceVolume when spec.Options is OnBalanceVolumeSpecOptions obvExpanded
                => new OnBalanceVolumeState(obvExpanded.Length, obvExpanded.MaType),
            IndicatorName.VolumeZoneOscillator when spec.Options is VolumeZoneOscillatorSpecOptions zone
                => new VolumeZoneOscillatorState(zone.Length),
            IndicatorName.CumulativeSum => new CumulativeSumState(),
            IndicatorName.CumulativeVolumeIndex => new CumulativeVolumeIndexState(),
            IndicatorName.HighLowIndex when spec.Options is HighLowIndexSpecOptions highLow
                => new HighLowIndexState(highLow.MaType, highLow.Length),
            IndicatorName.SimpleReturns when spec.Options is SimpleReturnsSpecOptions simple
                => new SimpleReturnsState(simple.Length),
            IndicatorName.NetVolume => new NetVolumeState(),
            IndicatorName.NormalizedVolume when spec.Options is NormalizedVolumeSpecOptions normalized
                => new NormalizedVolumeState(normalized.Length),
            IndicatorName.BalanceOfPower when spec.Options is BalanceOfPowerSpecOptions balance
                => new BalanceOfPowerState(balance.MaType, balance.Length),
            IndicatorName.PriceMomentum when spec.Options is PriceMomentumSpecOptions priceMomentum
                => new PriceMomentumState(priceMomentum.Length),
            IndicatorName.VolumeMomentum when spec.Options is VolumeMomentumSpecOptions volumeMomentum
                => new VolumeMomentumState(volumeMomentum.Length),
            IndicatorName.MomentumOscillator when spec.Options is MomentumSpecOptions mom
                => new MomentumOscillatorState(length: mom.Length),
            IndicatorName.MomentumOscillator when spec.Options is MomentumOscillatorSpecOptions momentumOptions
                => new MomentumOscillatorState(momentumOptions.MaType, momentumOptions.Length),
            IndicatorName.DiNapoliPreferredStochasticOscillator when spec.Options is DiNapoliPreferredStochasticOscillatorSpecOptions diNapoli
                => new DiNapoliPreferredStochasticOscillatorState(diNapoli.Length),
            IndicatorName.DynamicMomentumOscillator when spec.Options is DynamicMomentumOscillatorSpecOptions momentum
                => new DynamicMomentumOscillatorState(momentum.MaType, momentum.Length),
            IndicatorName.DoubleStochasticOscillator when spec.Options is DoubleStochasticOscillatorSpecOptions doubleStochastic
                => new DoubleStochasticOscillatorState(doubleStochastic.MaType, doubleStochastic.Length),
            IndicatorName.RateOfChange when spec.Options is RocSpecOptions roc
                => new RateOfChangeState(roc.Length),
            IndicatorName.RateOfChange when spec.Options is RateOfChangeSpecOptions rate
                => new RateOfChangeState(rate.Length),
            IndicatorName.VolumeRateOfChange when spec.Options is VrocSpecOptions volumeRate
                => new VolumeRateOfChangeState(volumeRate.Length),
            IndicatorName.WilliamsR when spec.Options is WilliamsRSpecOptions williams
                => new WilliamsRState(williams.Length),
            IndicatorName.StochasticFastOscillator when spec.Options is StochasticFastOscillatorSpecOptions fastStoch
                => new StochasticFastOscillatorState(fastStoch.MaType, fastStoch.Length, fastStoch.SmoothLength1, fastStoch.SmoothLength2),
            IndicatorName.StochasticRegular when spec.Options is StochasticRegularSpecOptions regular
                => new StochasticRegularState(regular.MaType, regular.Length1, regular.Length2),
            IndicatorName.StochasticOscillator when spec.Options is StochasticSpecOptions stochastic
                => new StochasticOscillatorState(stochastic.MaType, stochastic.KLength, stochastic.DLength),
            IndicatorName.StochasticOscillator when spec.Options is StochasticKSpecOptions stochasticK
                => new StochasticOscillatorState(stochasticK.MaType, stochasticK.Length, stochasticK.SmoothLength1, stochasticK.SmoothLength2),
            IndicatorName.StochasticOscillator when spec.Options is PricePositionSpecOptions position
                => new StochasticOscillatorState(position.MaType, position.Length, position.SmoothLength1, position.SmoothLength2),
            IndicatorName.StochasticOscillator when spec.Options is StochasticOscillatorSpecOptions stochasticFull
                => new StochasticOscillatorState(stochasticFull.MaType, stochasticFull.Length, stochasticFull.SmoothLength1, stochasticFull.SmoothLength2),
            IndicatorName.StochasticOscillator when spec.Options is StochasticDSpecOptions stochasticD
                => new StochasticOscillatorState(length: stochasticD.Length),
            IndicatorName.ChandeMomentumOscillatorAverage when spec.Options is ChandeMomentumOscillatorAverageSpecOptions
                => new ChandeMomentumOscillatorAverageState(),
            IndicatorName.ChandeMomentumOscillatorAbsoluteAverage when spec.Options is ChandeMomentumOscillatorAbsoluteAverageSpecOptions
                => new ChandeMomentumOscillatorAbsoluteAverageState(),
            IndicatorName.ChandeMomentumOscillatorFilter when spec.Options is ChandeMomentumOscillatorFilterSpecOptions filteredChande
                => new ChandeMomentumOscillatorFilterState(filteredChande.MaType, filteredChande.Length),
            IndicatorName.ChandeMomentumOscillatorAbsolute when spec.Options is ChandeMomentumOscillatorAbsoluteSpecOptions absoluteChande
                => new ChandeMomentumOscillatorAbsoluteState(absoluteChande.Length),
            IndicatorName.VariableIndexDynamicAverage when spec.Options is VidyaSpecOptions vidya
                => new VariableIndexDynamicAverageState(vidya.MaType, vidya.Length),
            IndicatorName.VariableIndexDynamicAverage when spec.Options is VariableIndexDynamicAverageSpecOptions dynamicAverage
                => new VariableIndexDynamicAverageState(dynamicAverage.MaType, dynamicAverage.Length),
            IndicatorName.ChandeMomentumOscillator when spec.Options is ChandeMomentumOscillatorSignalSpecOptions chandeSignal
                => new ChandeMomentumOscillatorState(chandeSignal.MaType, chandeSignal.Length, chandeSignal.SignalLength),
            IndicatorName.ChandeMomentumOscillator when spec.Options is CmoSpecOptions cmo
                => new ChandeMomentumOscillatorState(cmo.MaType, cmo.Length, 3),
            IndicatorName.ChandeMomentumOscillator when spec.Options is ChandeMomentumOscillatorSpecOptions chande
                => new ChandeMomentumOscillatorState(chande.MaType, chande.Length, chande.SignalLength),
            IndicatorName.EhlersHannMovingAverage when spec.Options is EhlersHannMovingAverageSpecOptions hann
                => new EhlersHannMovingAverageState(hann.Length),
            IndicatorName.SineWeightedMovingAverage when spec.Options is SineWmaSpecOptions sine
                => new SineWeightedMovingAverageState(sine.Length),
            IndicatorName.DoubleExponentialMovingAverage when spec.Options is DemaSpecOptions dema
                => new DoubleExponentialMovingAverageState(length: dema.Length),
            IndicatorName.TripleExponentialMovingAverage when spec.Options is TemaSpecOptions tema
                => new TripleExponentialMovingAverageState(length: tema.Length),
            IndicatorName.ZeroLagExponentialMovingAverage when spec.Options is ZlemaSpecOptions zlema
                => new ZeroLagExponentialMovingAverageState(length: zlema.Length),
            IndicatorName.LeoMovingAverage when spec.Options is LeoMovingAverageSpecOptions leo
                => new LeoMovingAverageState(leo.Length),
            IndicatorName.ArnaudLegouxMovingAverage when spec.Options is AlmaSpecOptions alma
                => new ArnaudLegouxMovingAverageState(alma.Length),
            IndicatorName.NaturalMovingAverage when spec.Options is NaturalMaSpecOptions natural
                => new NaturalMovingAverageState(natural.Length),
            IndicatorName.SequentiallyFilteredMovingAverage when spec.Options is SequentiallyFilteredMovingAverageSpecOptions sequential
                => new SequentiallyFilteredMovingAverageState(sequential.MaType, sequential.Length),
            IndicatorName.DistanceWeightedMovingAverage when spec.Options is DistanceWeightedMovingAverageSpecOptions reciprocal
                => new DistanceWeightedMovingAverageState(reciprocal.Length),
            IndicatorName.InverseDistanceWeightedMovingAverage when spec.Options is InverseDistanceWeightedMovingAverageSpecOptions distance
                => new InverseDistanceWeightedMovingAverageState(distance.Length),
            IndicatorName.MiddleHighLowMovingAverage when spec.Options is MiddleHighLowMovingAverageSpecOptions middle
                => new MiddleHighLowMovingAverageState(middle.MaType, middle.Length1, middle.Length2),
            IndicatorName.FareySequenceWeightedMovingAverage when spec.Options is FareySequenceWeightedMovingAverageSpecOptions farey
                => new FareySequenceWeightedMovingAverageState(farey.Length),
            IndicatorName.WellesWilderMovingAverage when spec.Options is WwmaSpecOptions wilder
                => new WellesWilderMovingAverageState(wilder.Length),
            IndicatorName.WellesWilderMovingAverage when spec.Options is SmmaSpecOptions smoothed
                => new WellesWilderMovingAverageState(smoothed.Length),
            IndicatorName.WellesWilderMovingAverage when spec.Options is ModifiedMaSpecOptions modified
                => new WellesWilderMovingAverageState(modified.Length),
            IndicatorName.SymmetricallyWeightedMovingAverage when spec.Options is SymmetricallyWeightedMovingAverageSpecOptions symmetric
                => new SymmetricallyWeightedMovingAverageState(symmetric.Length),
            IndicatorName.EhlersTriangleMovingAverage when spec.Options is EhlersTriangleMovingAverageSpecOptions triangle
                => new EhlersTriangleMovingAverageState(triangle.Length),
            IndicatorName.JsaMovingAverage when spec.Options is JsaMovingAverageSpecOptions paired
                => new JsaMovingAverageState(paired.Length),
            IndicatorName.Midpoint when spec.Options is MidpointSpecOptions midpoint => new MidpointState(midpoint.Length),
            IndicatorName.Midprice when spec.Options is MidpriceSpecOptions midprice => new MidpriceState(midprice.Length),
            IndicatorName.ParabolicWeightedMovingAverage when spec.Options is QuadraticWmaSpecOptions quadratic => new ParabolicWeightedMovingAverageState(quadratic.Length),
            IndicatorName.ParabolicWeightedMovingAverage when spec.Options is ParabolicWmaSpecOptions parabolic => new ParabolicWeightedMovingAverageState(parabolic.Length),
            IndicatorName.CubedWeightedMovingAverage when spec.Options is CubicWmaSpecOptions cubic => new CubedWeightedMovingAverageState(cubic.Length),
            IndicatorName.CubedWeightedMovingAverage when spec.Options is CubedWeightedMovingAverageSpecOptions cubed => new CubedWeightedMovingAverageState(cubed.Length),
            IndicatorName.QuickMovingAverage when spec.Options is QuickMovingAverageSpecOptions quick => new QuickMovingAverageState(quick.Length),
            IndicatorName.FibonacciWeightedMovingAverage when spec.Options is FibonacciWeightedMovingAverageSpecOptions fib => new FibonacciWeightedMovingAverageState(fib.Length),
            IndicatorName.SquareRootWeightedMovingAverage when spec.Options is SquareRootWeightedMovingAverageSpecOptions root => new SquareRootWeightedMovingAverageState(root.Length),
            IndicatorName.KaufmanAdaptiveMovingAverage when spec.Options is KamaSpecOptions kama => new KaufmanAdaptiveMovingAverageState(kama.Length),
            IndicatorName.QuadraticMovingAverage when spec.Options is QuadraticMovingAverageSpecOptions quadraticMean => new QuadraticMovingAverageState(quadraticMean.Length),
            IndicatorName.GeometricMovingAverage when spec.Options is GeoMaSpecOptions geometric => new GeometricMovingAverageState(geometric.Length),
            IndicatorName.GeometricMeanMovingAverage when spec.Options is GeometricMeanMovingAverageSpecOptions positiveGeometric => new GeometricMeanMovingAverageState(positiveGeometric.Length),
            IndicatorName.SimpleMovingAverage => new SimpleMovingAverageState(((SmaSpecOptions)spec.Options).Length),
            IndicatorName.ExponentialMovingAverage => new ExponentialMovingAverageState(((EmaSpecOptions)spec.Options).Length),
            IndicatorName.RelativeStrengthIndex => new RelativeStrengthIndexState(((RsiSpecOptions)spec.Options).Length),
            IndicatorName.MovingAverageConvergenceDivergence => new MovingAverageConvergenceDivergenceState(
                ((MacdSpecOptions)spec.Options).FastLength,
                ((MacdSpecOptions)spec.Options).SlowLength,
                ((MacdSpecOptions)spec.Options).SignalLength),
            IndicatorName.BollingerBands => new BollingerBandsState(
                ((BollingerBandsSpecOptions)spec.Options).Length,
                ((BollingerBandsSpecOptions)spec.Options).StdDevMult),
            IndicatorName.AverageTrueRange => new AverageTrueRangeState(((AtrSpecOptions)spec.Options).Length),
            _ => null
        };
    }

    /// <summary>
    /// Extracts the appropriate value from a streaming update.
    /// </summary>
    public static double ExtractValue(StreamingIndicatorStateUpdate update, IndicatorSpec spec)
    {
        // A named key wins, and is read before the slot: a spec built from a key carries Output = Primary, so
        // the branch below would answer with the state's own value and never look at the named outputs - the
        // batch half of that same mistake is in BuilderArmBinding. See issue #219.
        if (spec.OutputKey is { } named)
        {
            return update.Outputs is not null && update.Outputs.TryGetValue(named, out var namedValue)
                ? namedValue
                : double.NaN;
        }

        // A spec that names no key wants the state's own value.
        return update.Value;
    }
}
