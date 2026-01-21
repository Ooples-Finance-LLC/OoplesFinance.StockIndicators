using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder.Catalogs;

/// <summary>
/// Catalog of available indicators for the builder.
/// Provides typed methods for common indicators and generic access to all 750+ indicators.
/// </summary>
public sealed class IndicatorCatalog
{
    private readonly StockIndicatorBuilder _builder;

    internal IndicatorCatalog(StockIndicatorBuilder builder)
    {
        _builder = builder;
    }

    #region Generic Indicator Access

    /// <summary>
    /// Calculates any indicator by name using reflection-based dispatch.
    /// This provides access to all 750+ indicators in the library.
    /// </summary>
    /// <param name="name">The indicator name from the IndicatorName enum.</param>
    /// <param name="parameters">Optional parameters for the indicator (varies by indicator).</param>
    /// <param name="input">Optional input series. If null, uses default price series.</param>
    /// <param name="key">Optional key for named lookup.</param>
    /// <returns>A SeriesHandle for the primary output of the indicator.</returns>
    /// <remarks>
    /// Use GetIndicatorParameters() to discover what parameters an indicator accepts.
    /// Common parameter types include: int (length), double (multiplier), MovingAvgType.
    /// </remarks>
    public SeriesHandle Calculate(IndicatorName name, object[]? parameters = null, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(name, new GenericIndicatorOptions(parameters ?? Array.Empty<object>()), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Gets the parameter information for a specific indicator.
    /// </summary>
    /// <param name="name">The indicator name.</param>
    /// <returns>Array of parameter info, or null if indicator not found.</returns>
    public static System.Reflection.ParameterInfo[]? GetIndicatorParameters(IndicatorName name)
    {
        return IndicatorInvoker.GetParameters(name);
    }

    /// <summary>
    /// Checks if an indicator is supported by the generic Calculate method.
    /// </summary>
    /// <param name="name">The indicator name.</param>
    /// <returns>True if the indicator can be calculated.</returns>
    public static bool IsIndicatorSupported(IndicatorName name)
    {
        return IndicatorInvoker.IsSupported(name);
    }

    /// <summary>
    /// Gets all supported indicator names.
    /// </summary>
    /// <returns>Collection of all supported indicator names.</returns>
    public static IReadOnlyCollection<IndicatorName> GetSupportedIndicators()
    {
        return IndicatorInvoker.GetSupportedIndicators();
    }

    #endregion

    /// <summary>
    /// Gets the default price series handle.
    /// </summary>
    public SeriesHandle Price()
    {
        return _builder.GetDefaultSeriesHandle();
    }

    /// <summary>
    /// Creates a chain for a specific symbol and timeframe.
    /// </summary>
    public IndicatorChain For(SymbolId symbol, BarTimeframe timeframe)
    {
        var key = new SeriesKey(symbol, timeframe);
        var baseSeries = _builder.GetOrCreateBaseSeries(key);
        return new IndicatorChain(this, baseSeries);
    }

    /// <summary>
    /// Creates a chain for a specific symbol with default timeframe.
    /// </summary>
    public IndicatorChain For(SymbolId symbol)
    {
        var defaultKey = _builder.ResolveDefaultSeriesKey();
        return For(symbol, defaultKey.Timeframe);
    }

    /// <summary>
    /// Creates a chain from an existing series.
    /// </summary>
    public IndicatorChain Then(SeriesHandle input)
    {
        return new IndicatorChain(this, input);
    }

    /// <summary>
    /// Calculates Simple Moving Average.
    /// </summary>
    public SeriesHandle Sma(int length, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        return _builder.AddIndicator(IndicatorSpecs.Sma(length), series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Exponential Moving Average.
    /// </summary>
    public SeriesHandle Ema(int length, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        return _builder.AddIndicator(IndicatorSpecs.Ema(length), series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Relative Strength Index.
    /// </summary>
    public SeriesHandle Rsi(int length = 14, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        return _builder.AddIndicator(IndicatorSpecs.Rsi(length), series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates MACD (Moving Average Convergence Divergence).
    /// </summary>
    public MacdSeries Macd(int fastLength = 12, int slowLength = 26, int signalLength = 9,
        SeriesHandle? input = null, IndicatorKey? primaryKey = null, IndicatorKey? signalKey = null, IndicatorKey? histogramKey = null)
    {
        var series = input ?? Price();
        var seriesKey = _builder.ResolveSeriesKey(series);
        var primary = _builder.AddIndicator(IndicatorSpecs.Macd(fastLength, slowLength, signalLength, IndicatorOutput.Primary),
            series, seriesKey, primaryKey);
        var signal = _builder.AddIndicator(IndicatorSpecs.Macd(fastLength, slowLength, signalLength, IndicatorOutput.Signal),
            series, seriesKey, signalKey);
        var histogram = _builder.AddIndicator(IndicatorSpecs.Macd(fastLength, slowLength, signalLength, IndicatorOutput.Histogram),
            series, seriesKey, histogramKey);

        return new MacdSeries(primary, signal, histogram);
    }

    /// <summary>
    /// Calculates Bollinger Bands.
    /// </summary>
    public BollingerBandsSeries BollingerBands(int length = 20, double stdDevMult = 2, SeriesHandle? input = null,
        IndicatorKey? upperKey = null, IndicatorKey? middleKey = null, IndicatorKey? lowerKey = null)
    {
        var series = input ?? Price();
        var seriesKey = _builder.ResolveSeriesKey(series);
        var upper = _builder.AddIndicator(IndicatorSpecs.BollingerBands(length, stdDevMult, IndicatorOutput.UpperBand),
            series, seriesKey, upperKey);
        var middle = _builder.AddIndicator(IndicatorSpecs.BollingerBands(length, stdDevMult, IndicatorOutput.MiddleBand),
            series, seriesKey, middleKey);
        var lower = _builder.AddIndicator(IndicatorSpecs.BollingerBands(length, stdDevMult, IndicatorOutput.LowerBand),
            series, seriesKey, lowerKey);

        return new BollingerBandsSeries(upper, middle, lower);
    }

    /// <summary>
    /// Calculates Average True Range.
    /// </summary>
    public SeriesHandle Atr(int length = 14, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        return _builder.AddIndicator(IndicatorSpecs.Atr(length), series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Average Directional Index.
    /// </summary>
    public SeriesHandle Adx(int length = 14, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        return _builder.AddIndicator(IndicatorSpecs.Adx(length), series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Stochastic Oscillator.
    /// </summary>
    public StochasticSeries Stochastic(int kLength = 14, int dLength = 3, SeriesHandle? input = null,
        IndicatorKey? kKey = null, IndicatorKey? dKey = null)
    {
        var series = input ?? Price();
        var seriesKey = _builder.ResolveSeriesKey(series);
        var k = _builder.AddIndicator(IndicatorSpecs.Stochastic(kLength, dLength, IndicatorOutput.Primary),
            series, seriesKey, kKey);
        var d = _builder.AddIndicator(IndicatorSpecs.Stochastic(kLength, dLength, IndicatorOutput.Signal),
            series, seriesKey, dKey);
        return new StochasticSeries(k, d);
    }

    /// <summary>
    /// Calculates Weighted Moving Average.
    /// </summary>
    public SeriesHandle Wma(int length, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.WeightedMovingAverage, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Hull Moving Average.
    /// </summary>
    public SeriesHandle Hma(int length, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.HullMovingAverage, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Triple Exponential Moving Average (TEMA).
    /// </summary>
    public SeriesHandle Tema(int length, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.TripleExponentialMovingAverage, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Double Exponential Moving Average (DEMA).
    /// </summary>
    public SeriesHandle Dema(int length, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.DoubleExponentialMovingAverage, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Commodity Channel Index.
    /// </summary>
    public SeriesHandle Cci(int length = 20, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.CommodityChannelIndex, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Williams %R.
    /// </summary>
    public SeriesHandle WilliamsR(int length = 14, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.WilliamsR, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Rate of Change.
    /// </summary>
    public SeriesHandle Roc(int length = 12, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.RateOfChange, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Momentum Oscillator.
    /// </summary>
    public SeriesHandle Momentum(int length = 10, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.MomentumOscillator, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Parabolic SAR.
    /// </summary>
    public SeriesHandle Sar(double accelerationStart = 0.02, double accelerationMax = 0.2, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.ParabolicSAR, new GenericIndicatorOptions(new object[] { accelerationStart, accelerationMax }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Keltner Channels.
    /// </summary>
    public KeltnerChannelSeries KeltnerChannels(int length = 20, double multiplier = 2, SeriesHandle? input = null,
        IndicatorKey? upperKey = null, IndicatorKey? middleKey = null, IndicatorKey? lowerKey = null)
    {
        var series = input ?? Price();
        var seriesKey = _builder.ResolveSeriesKey(series);
        var spec = IndicatorSpecs.Create(IndicatorName.KeltnerChannels, new GenericIndicatorOptions(new object[] { length, multiplier }), IndicatorOutput.Primary);
        var upper = _builder.AddIndicator(
            IndicatorSpecs.Create(IndicatorName.KeltnerChannels, new GenericIndicatorOptions(new object[] { length, multiplier }), IndicatorOutput.UpperBand),
            series, seriesKey, upperKey);
        var middle = _builder.AddIndicator(
            IndicatorSpecs.Create(IndicatorName.KeltnerChannels, new GenericIndicatorOptions(new object[] { length, multiplier }), IndicatorOutput.MiddleBand),
            series, seriesKey, middleKey);
        var lower = _builder.AddIndicator(
            IndicatorSpecs.Create(IndicatorName.KeltnerChannels, new GenericIndicatorOptions(new object[] { length, multiplier }), IndicatorOutput.LowerBand),
            series, seriesKey, lowerKey);
        return new KeltnerChannelSeries(upper, middle, lower);
    }

    /// <summary>
    /// Calculates Donchian Channels.
    /// </summary>
    public DonchianChannelSeries DonchianChannels(int length = 20, SeriesHandle? input = null,
        IndicatorKey? upperKey = null, IndicatorKey? middleKey = null, IndicatorKey? lowerKey = null)
    {
        var series = input ?? Price();
        var seriesKey = _builder.ResolveSeriesKey(series);
        var upper = _builder.AddIndicator(
            IndicatorSpecs.Create(IndicatorName.DonchianChannels, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.UpperBand),
            series, seriesKey, upperKey);
        var middle = _builder.AddIndicator(
            IndicatorSpecs.Create(IndicatorName.DonchianChannels, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.MiddleBand),
            series, seriesKey, middleKey);
        var lower = _builder.AddIndicator(
            IndicatorSpecs.Create(IndicatorName.DonchianChannels, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.LowerBand),
            series, seriesKey, lowerKey);
        return new DonchianChannelSeries(upper, middle, lower);
    }

    /// <summary>
    /// Calculates VWAP (Volume Weighted Average Price).
    /// </summary>
    public SeriesHandle Vwap(SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.VolumeWeightedAveragePrice, new GenericIndicatorOptions(Array.Empty<object>()), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates On Balance Volume.
    /// </summary>
    public SeriesHandle Obv(SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.OnBalanceVolume, new GenericIndicatorOptions(Array.Empty<object>()), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Money Flow Index.
    /// </summary>
    public SeriesHandle Mfi(int length = 14, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.MoneyFlowIndex, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates Ichimoku Cloud.
    /// </summary>
    public IchimokuSeries Ichimoku(int tenkanLength = 9, int kijunLength = 26, int senkouBLength = 52, SeriesHandle? input = null)
    {
        var series = input ?? Price();
        var seriesKey = _builder.ResolveSeriesKey(series);
        var opts = new GenericIndicatorOptions(new object[] { tenkanLength, kijunLength, senkouBLength });
        var tenkanSen = _builder.AddIndicator(IndicatorSpecs.Create(IndicatorName.IchimokuCloud, opts, IndicatorOutput.Primary), series, seriesKey, null);
        var kijunSen = _builder.AddIndicator(IndicatorSpecs.Create(IndicatorName.IchimokuCloud, opts, IndicatorOutput.Signal), series, seriesKey, null);
        var senkouSpanA = _builder.AddIndicator(IndicatorSpecs.Create(IndicatorName.IchimokuCloud, opts, IndicatorOutput.UpperBand), series, seriesKey, null);
        var senkouSpanB = _builder.AddIndicator(IndicatorSpecs.Create(IndicatorName.IchimokuCloud, opts, IndicatorOutput.LowerBand), series, seriesKey, null);
        var chikouSpan = _builder.AddIndicator(IndicatorSpecs.Create(IndicatorName.IchimokuCloud, opts, IndicatorOutput.Histogram), series, seriesKey, null);
        return new IchimokuSeries(tenkanSen, kijunSen, senkouSpanA, senkouSpanB, chikouSpan);
    }

    /// <summary>
    /// Calculates Standard Deviation.
    /// </summary>
    public SeriesHandle StdDev(int length = 20, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.StandardDeviation, new GenericIndicatorOptions(new object[] { length }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Calculates True Strength Index.
    /// </summary>
    public SeriesHandle Tsi(int longLength = 25, int shortLength = 13, SeriesHandle? input = null, IndicatorKey? key = null)
    {
        var series = input ?? Price();
        var spec = IndicatorSpecs.Create(IndicatorName.TrueStrengthIndex, new GenericIndicatorOptions(new object[] { longLength, shortLength }), IndicatorOutput.Primary);
        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);
    }

    /// <summary>
    /// Creates a formula combining two series.
    /// </summary>
    public SeriesHandle Formula(SeriesHandle left, SeriesHandle right, FormulaOp op)
    {
        return _builder.AddFormula(left, right, op switch
        {
            FormulaOp.Add => (x, y) => x + y,
            FormulaOp.Subtract => (x, y) => x - y,
            FormulaOp.Multiply => (x, y) => x * y,
            FormulaOp.Divide => (x, y) => x / y,
            _ => (x, y) => double.NaN
        });
    }

    /// <summary>
    /// Creates a formula combining two series with a custom function.
    /// </summary>
    public SeriesHandle Formula(SeriesHandle left, SeriesHandle right, Func<double, double, double> formula)
    {
        return _builder.AddFormula(left, right, formula);
    }

    internal void ApplyDefaults(IndicatorSelection selection)
    {
        if (selection == null)
        {
            return;
        }

        switch (selection.Preset)
        {
            case IndicatorPreset.All:
            case IndicatorPreset.Core:
                var price = Price();
                Sma(20, price, IndicatorKey.Sma);
                Rsi(14, price, IndicatorKey.Rsi);
                Macd(12, 26, 9, price, IndicatorKey.Macd, IndicatorKey.MacdSignal, IndicatorKey.MacdHistogram);
                BollingerBands(20, 2, price, IndicatorKey.BollingerUpper, IndicatorKey.BollingerMiddle, IndicatorKey.BollingerLower);
                break;
            case IndicatorPreset.Only:
                if (selection.Include == null)
                {
                    return;
                }

                var baseSeries = Price();
                for (var i = 0; i < selection.Include.Count; i++)
                {
                    switch (selection.Include[i])
                    {
                        case IndicatorName.SimpleMovingAverage:
                            Sma(20, baseSeries, IndicatorKey.Sma);
                            break;
                        case IndicatorName.RelativeStrengthIndex:
                            Rsi(14, baseSeries, IndicatorKey.Rsi);
                            break;
                        case IndicatorName.MovingAverageConvergenceDivergence:
                            Macd(12, 26, 9, baseSeries, IndicatorKey.Macd, IndicatorKey.MacdSignal, IndicatorKey.MacdHistogram);
                            break;
                        case IndicatorName.BollingerBands:
                            BollingerBands(20, 2, baseSeries, IndicatorKey.BollingerUpper, IndicatorKey.BollingerMiddle,
                                IndicatorKey.BollingerLower);
                            break;
                    }
                }

                break;
        }
    }
}

/// <summary>
/// Fluent chain for indicators on a specific series.
/// </summary>
public readonly struct IndicatorChain
{
    private readonly IndicatorCatalog _catalog;
    private readonly SeriesHandle _input;

    internal IndicatorChain(IndicatorCatalog catalog, SeriesHandle input)
    {
        _catalog = catalog;
        _input = input;
    }

    /// <summary>
    /// Gets the input series handle.
    /// </summary>
    public SeriesHandle Input => _input;

    /// <summary>
    /// Calculates Simple Moving Average.
    /// </summary>
    public SeriesHandle Sma(int length, IndicatorKey? key = null)
    {
        return _catalog.Sma(length, _input, key);
    }

    /// <summary>
    /// Calculates Exponential Moving Average.
    /// </summary>
    public SeriesHandle Ema(int length, IndicatorKey? key = null)
    {
        return _catalog.Ema(length, _input, key);
    }

    /// <summary>
    /// Calculates Relative Strength Index.
    /// </summary>
    public SeriesHandle Rsi(int length = 14, IndicatorKey? key = null)
    {
        return _catalog.Rsi(length, _input, key);
    }

    /// <summary>
    /// Calculates MACD.
    /// </summary>
    public MacdSeries Macd(int fastLength = 12, int slowLength = 26, int signalLength = 9,
        IndicatorKey? primaryKey = null, IndicatorKey? signalKey = null, IndicatorKey? histogramKey = null)
    {
        return _catalog.Macd(fastLength, slowLength, signalLength, _input, primaryKey, signalKey, histogramKey);
    }

    /// <summary>
    /// Calculates Bollinger Bands.
    /// </summary>
    public BollingerBandsSeries BollingerBands(int length = 20, double stdDevMult = 2,
        IndicatorKey? upperKey = null, IndicatorKey? middleKey = null, IndicatorKey? lowerKey = null)
    {
        return _catalog.BollingerBands(length, stdDevMult, _input, upperKey, middleKey, lowerKey);
    }

    /// <summary>
    /// Calculates ATR.
    /// </summary>
    public SeriesHandle Atr(int length = 14, IndicatorKey? key = null)
    {
        return _catalog.Atr(length, _input, key);
    }

    /// <summary>
    /// Calculates ADX.
    /// </summary>
    public SeriesHandle Adx(int length = 14, IndicatorKey? key = null)
    {
        return _catalog.Adx(length, _input, key);
    }
}
