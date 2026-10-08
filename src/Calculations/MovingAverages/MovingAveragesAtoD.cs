using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the arnaud legoux moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="sigma">The sigma.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateArnaudLegouxMovingAverage(this StockData stockData, int length = 9, double offset = 0.85, int sigma = 6)
    {
        List<double> almaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new AlmaWindowMean(length, offset, sigma);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevAlma = GetLastOrDefault(almaList);
            var alma = mean.Next(currentValue, true);
            almaList.Add(alma);

            var signal = GetCompareSignal(currentValue - alma, prevVal - prevAlma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Alma", almaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(almaList);
        stockData.IndicatorName = IndicatorName.ArnaudLegouxMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the ahrens moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAhrensMovingAverage(this StockData stockData, int length = 9)
    {
        List<double> ahmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var window = new AhrensWindow(length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevAhma = GetLastOrDefault(ahmaList);
            var ahma = window.Next(currentValue, true);
            ahmaList.Add(ahma);

            var signal = GetCompareSignal(currentValue - ahma, prevValue - prevAhma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ahma", ahmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ahmaList);
        stockData.IndicatorName = IndicatorName.AhrensMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveMovingAverage(this StockData stockData, int fastLength = 2, int slowLength = 14, int length = 14)
    {
        var input=stockData.ChainedValues.Count>0?stockData.ChainedValues:stockData.InputValues;var high=stockData.HighPrices;var low=stockData.LowPrices;using var window=new AdaptiveRangeMeanWindow(fastLength,slowLength,length);
        List<double> values=new(input.Count);var signals=CreateSignalsList(stockData);
        for(var i=0;i<input.Count;i++)
        {
            var value=window.Next(input[i],high[i],low[i],true);
            signals?.Add(GetCompareSignal(input[i]-value,(i>0?input[i-1]:0)-(i>0?values[i-1]:0)));values.Add(value);
        }
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Ama",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.AdaptiveMovingAverage;return stockData;
    }


    /// <summary>
    /// Calculates the adaptive exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveExponentialMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 10)
    {
        length = Math.Max(1, length);
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var seeds = external ? Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, input) : null;
        using var window = new AdaptiveEmaWindow(maType, length, Math.Max(1, input.Count));
        List<double> line = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var previous = i == 0 ? input[i] : line[i - 1]; var value = window.Next(input[i], high[i], low[i], true, seeds is null ? null : seeds[i]); line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, (i > 0 ? input[i - 1] : 0) - previous));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Aema", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.AdaptiveExponentialMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive autonomous recursive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveAutonomousRecursiveMovingAverage(this StockData stockData, int length = 14, double gamma = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new AdaptiveAutonomousWindow(length, gamma);
        List<double> line = new(input.Count), deviation = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var previous = i == 0 ? input[i] : line[i - 1]; var value = window.Next(input[i], true);
            line.Add(value.Average); deviation.Add(value.Deviation);
            signals?.Add(GetCompareSignal(input[i] - value.Average, (i > 0 ? input[i - 1] : 0) - previous));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "D", deviation }, { "Aarma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.AdaptiveAutonomousRecursiveMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the autonomous recursive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="momLength">Length of the mom.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAutonomousRecursiveMovingAverage(this StockData stockData, int length = 14, int momLength = 7, double gamma = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new AutonomousRecursiveWindow(length, momLength, gamma);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Arma", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.AutonomousRecursiveMovingAverage; return stockData;
    }


    /// <summary>
    /// Calculates the atr filtered exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="atrLength">Length of the atr.</param>
    /// <param name="stdDevLength">Length of the standard dev.</param>
    /// <param name="lbLength">Length of the lb.</param>
    /// <param name="min">The minimum.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAtrFilteredExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 45, int atrLength = 20, int stdDevLength = 10, int lbLength = 20, 
        double min = 5)
    {
        var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new AtrFilterWindow(maType, length, atrLength, stdDevLength, lbLength, min, external);
        var (input, high, low, _, _) = GetInputValuesList(stockData); List<double>? averages = null, squares = null; var customSquares = false;
        if (external)
        {
            var normalized = input.Select((p, i) => AtrFilterWindow.Publish(AtrFilterWindow.RelativeRange(p, high[i], low[i], i == 0 ? p : input[i - 1]))).ToList();
            averages = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(normalized), Math.Max(1, atrLength))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, atrLength), normalized);
            var powered = averages.Select(v => AtrFilterWindow.Publish(AtrFilterWindow.Square(ExactVarianceWindow.Units(v)))).ToList(); var substitutions = Builder.Compute.ComponentAverage.Substitutions;
            squares = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(powered), Math.Max(1, stdDevLength))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, stdDevLength), powered); customSquares = Builder.Compute.ComponentAverage.Substitutions != substitutions;
        }
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], high[i], low[i], true, averages?[i], squares?[i], customSquares); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Afp", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.AtrFilteredExponentialMovingAverage; return stockData;
    }


    /// <summary>
    /// Calculates the adaptive least squares.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="smooth">The smooth.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveLeastSquares(this StockData stockData, int length = 500, double smooth = 1.5)
    {
        using var window = new AdaptiveFitWindow(length, smooth); var (input, high, low, _, _) = GetInputValuesList(stockData); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], high[i], low[i], true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Als", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.AdaptiveLeastSquares; return stockData;
    }


    /// <summary>
    /// Calculates the alpha decreasing exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAlphaDecreasingExponentialMovingAverage(this StockData stockData)
    {
        List<double> emaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var window = new AlphaDecreasingWindow();
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevEma = GetLastOrDefault(emaList);
            var ema = window.Next(currentValue, true);
            emaList.Add(ema);

            var signal = GetCompareSignal(currentValue - ema, prevValue - prevEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ema", emaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emaList);
        stockData.IndicatorName = IndicatorName.AlphaDecreasingExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the automatic filter.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAutoFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 500)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var components = external ? AutoFilterWindow.Components(stockData, input, maType, length) : null; using var window = new AutoFilterWindow(maType, length, external);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true, components?[0][i], components?[1][i]); values.Add(point.Line); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Af", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.AutoFilter; return stockData;
    }


    /// <summary>
    /// Calculates the automatic line.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAutoLine(this StockData stockData, int length = 500)
    {
        List<double> xList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        // The deviation of the window about its own mean, not the mean squared residual from a moving average
        // of it. The line holds until price escapes a band one deviation either side of it, and sigma in a band
        // is the windowed deviation; CalculateStandardDeviationVolatility is a different quantity, about 55%
        // wider on a typical price series, so the band was that much too wide and the line held through moves
        // that should have moved it. See #190.
        //
        // The deviation is 0 until the window fills, which at the default length of 500 is a long warm-up. The
        // band is then zero-width and the line simply follows price, which is the honest answer where no
        // deviation is known yet - and unlike a carried-forward length, it costs nothing later: the line picks
        // up its band as soon as the window fills.
        var devList = GetStandardDeviationList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var dev = devList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevX = i >= 1 ? xList[i - 1] : currentValue;
            var x = currentValue > prevX + dev ? currentValue : currentValue < prevX - dev ? currentValue : prevX;
            xList.Add(x);

            var signal = GetCompareSignal(currentValue - x, prevValue - prevX);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Al", xList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(xList);
        stockData.IndicatorName = IndicatorName.AutoLine;

        return stockData;
    }


    /// <summary>
    /// Calculates the automatic line with drift.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAutoLineWithDrift(this StockData stockData, int length = 500)
    {
        List<double> values=new(stockData.Count);var signals=CreateSignalsList(stockData);var (input,_,_,_,_)=GetInputValuesList(stockData);var window=new AutoDriftWindow(length);
        for(var i=0;i<stockData.Count;i++)
        {
            var value=window.Next(input[i],true);var previous=i>0?values[i-1]:Math.Round(input[i]);var previousPrice=i>0?input[i-1]:0;
            signals?.Add(GetCompareSignal(input[i]-value,previousPrice-previous));values.Add(value);
        }
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Alwd",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.AutoLineWithDrift;return stockData;
    }


    /// <summary>
    /// Calculates the 1LC Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData Calculate1LCLeastSquaresMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); length = Math.Max(1, length);
        var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var means = external ? Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, input) : null;
        using var window = new OneLcWindow(maType, length, external); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true, means?[i]); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "1lsma", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName._1LCLeastSquaresMovingAverage; return stockData;
    }


    /// <summary>
    /// Calculates the 3HMA
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData Calculate3HMA(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 50)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var p = ThreeHullWindow.Period(length);
            var first = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), ThreeHullWindow.Third(p))?.ToList() ?? GetMovingAverageList(stockData, maType, ThreeHullWindow.Third(p), input);
            var second = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), ThreeHullWindow.Half(p))?.ToList() ?? GetMovingAverageList(stockData, maType, ThreeHullWindow.Half(p), input);
            var third = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), p)?.ToList() ?? GetMovingAverageList(stockData, maType, p, input);
            var adjusted = first.Select((v, i) => ThreeHullWindow.Combine(v, second[i], third[i])).ToList();
            line = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(adjusted), p)?.ToList() ?? GetMovingAverageList(stockData, maType, p, adjusted);
        }
        else
        {
            using var window = new ThreeHullWindow(maType, length);
            foreach (var price in input) line.Add(window.Next(price, true));
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? 0 : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "3hma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName._3HMA;
        return stockData;
    }


    /// <summary>
    /// Calculates the Bryant Adaptive Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="maxLength"></param>
    /// <param name="trend"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBryantAdaptiveMovingAverage(this StockData stockData, int length = 14, int maxLength = 100, double trend = -1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new BryantWindow(length, maxLength, trend);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Bama", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.BryantAdaptiveMovingAverage; return stockData;
    }


    /// <summary>
    /// Calculates the Compound Ratio Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCompoundRatioMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 20)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        using var window = new CompoundRatioWindow(maType, length, initializeFallback: false);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var raw = input.Select(price => window.Raw(price, true)).ToList();
            line = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(raw), CompoundRatioWindow.SmoothPeriod(length))?.ToList() ?? GetMovingAverageList(stockData, maType, CompoundRatioWindow.SmoothPeriod(length), raw);
        }
        else foreach (var price in input) line.Add(window.Next(price, true));
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? 0 : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Crma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.CompoundRatioMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Cubed Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCubedWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> cwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new IntegerPowerWindowMean(length, 3);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevCwma = GetLastOrDefault(cwmaList);
            var cwma = mean.Next(currentValue, true);
            cwmaList.Add(cwma);

            var signal = GetCompareSignal(currentValue - cwma, prevVal - prevCwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cwma", cwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cwmaList);
        stockData.IndicatorName = IndicatorName.CubedWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Corrected Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCorrectedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 35)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var means = external ? CorrectedAverageWindow.Components(stockData, input, maType, length) : null; using var window = new CorrectedAverageWindow(maType, length, external);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true, means?[i]); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Cma", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.CorrectedMovingAverage; return stockData;

    }


    /// <summary>
    /// Calculates the Double Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDoubleExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> demaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma = ema1List[i];
            var currentEma2 = ema2List[i];

            var prevDema = GetLastOrDefault(demaList);
            var dema = ExponentialExtrapolation.Double(currentEma, currentEma2);
            demaList.Add(dema);

            var signal = GetCompareSignal(currentValue - dema, prevValue - prevDema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dema", demaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(demaList);
        stockData.IndicatorName = IndicatorName.DoubleExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Damped Sine Wave Weighted Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDampedSineWaveWeightedFilter(this StockData stockData, int length = 50)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new DampedSineWindow(length);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dswwf", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.DampedSineWaveWeightedFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Double Exponential Smoothing
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <param name="gamma"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDoubleExponentialSmoothing(this StockData stockData, double alpha = 0.01, double gamma = 0.9)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new DoubleSmoothingWindow(alpha, gamma);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Des", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.DoubleExponentialSmoothing;
        return stockData;
    }


    /// <summary>
    /// Calculates the Distance Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDistanceWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> dwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new DistanceMassWindowMean(length, reciprocal: true);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevDwma = GetLastOrDefault(dwmaList);
            var dwma = mean.Next(currentValue, true);
            dwmaList.Add(dwma);

            var signal = GetCompareSignal(currentValue - dwma, prevVal - prevDwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dwma", dwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dwmaList);
        stockData.IndicatorName = IndicatorName.DistanceWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Dynamically Adjustable Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDynamicallyAdjustableFilter(this StockData stockData, int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new DynamicFilterWindow(length); var line = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var result = window.Next(input[i], true); var previous = i == 0 ? input[i] : line[i - 1];
            signals?.Add(GetCompareSignal(input[i] - result, (i == 0 ? 0 : input[i - 1]) - previous)); line.Add(result);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Daf", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.DynamicallyAdjustableFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Dynamically Adjustable Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDynamicallyAdjustableMovingAverage(this StockData stockData, int fastLength = 6, int slowLength = 200)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new DynamicAverageWindow(fastLength, slowLength);
        List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dama", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.DynamicallyAdjustableMovingAverage;
        return stockData;
    }

}

