using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Ppo Moving Average.
    /// </summary>
    /// <remarks>
    /// The gap between a fast and a slow exponential average of the series, as a percentage of the slow one.
    /// It is the percentage price oscillator without a signal line, and unlike
    /// <see cref="CalculatePercentagePriceOscillator"/> it always averages exponentially rather than taking
    /// the average a caller asks for. A bar whose slow average is zero has nothing to take a percentage of.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePpoMovingAverage(this StockData stockData, int fastLength = 12, int slowLength = 26)
    {
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> ppoMaList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var fastBuffer = SpanCompat.CreateOutputBuffer(count);
        var slowBuffer = SpanCompat.CreateOutputBuffer(count);
        MovingAverageCore.ExponentialMovingAverage(inputSpan, fastBuffer.Span, fastLength);
        MovingAverageCore.ExponentialMovingAverage(inputSpan, slowBuffer.Span, slowLength);

        for (var i = 0; i < count; i++)
        {
            var slowEma = slowBuffer.Span[i];
            var ppoMa = slowEma != 0 ? (fastBuffer.Span[i] - slowEma) / slowEma * 100 : 0;
            ppoMaList.Add(ppoMa);

            var prevPpoMa1 = i >= 1 ? ppoMaList[i - 1] : 0;
            var prevPpoMa2 = i >= 2 ? ppoMaList[i - 2] : 0;
            var signal = GetCompareSignal(ppoMa - prevPpoMa1, prevPpoMa1 - prevPpoMa2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "PpoMa", ppoMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ppoMaList);
        stockData.IndicatorName = IndicatorName.PpoMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Price Momentum.
    /// </summary>
    /// <remarks>
    /// The change in the series over <paramref name="length"/> bars, in the price's own units rather than as
    /// a proportion. A bar with no value that far back publishes zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePriceMomentum(this StockData stockData, int length = 10)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> momentumList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var momentum = i >= length ? inputList[i] - inputList[i - length] : 0;
            momentumList.Add(momentum);

            var prevMomentum1 = i >= 1 ? momentumList[i - 1] : 0;
            var prevMomentum2 = i >= 2 ? momentumList[i - 2] : 0;
            var signal = GetCompareSignal(momentum - prevMomentum1, prevMomentum1 - prevMomentum2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pm", momentumList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(momentumList);
        stockData.IndicatorName = IndicatorName.PriceMomentum;

        return stockData;
    }

    /// <summary>
    /// Calculates the powered kaufman adaptive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePoweredKaufmanAdaptiveMovingAverage(this StockData stockData, int length = 100, double factor = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new PoweredKaufmanWindow(length, factor);
        List<double> line = new(input.Count), power = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var previous = i == 0 ? input[i] : line[i - 1]; var value = window.Next(input[i], true);
            line.Add(value.Average); power.Add(value.Power);
            signals?.Add(GetCompareSignal(input[i] - value.Average, (i > 0 ? input[i - 1] : 0) - previous));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Per", power }, { "Pkama", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.PoweredKaufmanAdaptiveMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Quick Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateQuickMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> qmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new QuickWindowMean(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevQma = GetLastOrDefault(qmaList);
            var qma = mean.Next(currentValue, true);
            qmaList.Add(qma);

            var signal = GetCompareSignal(currentValue - qma, prevVal - prevQma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Qma", qmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(qmaList);
        stockData.IndicatorName = IndicatorName.QuickMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quadratic Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateQuadraticMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> qmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        using var mean = new RollingRootMeanSquare(length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevQma = GetLastOrDefault(qmaList);
            var qma = mean.Next(currentValue, true);
            qmaList.Add(qma);

            var signal = GetCompareSignal(currentValue - qma, prevValue - prevQma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Qma", qmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(qmaList);
        stockData.IndicatorName = IndicatorName.QuadraticMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quadruple Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateQuadrupleExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> qemaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);
        var ema4List = GetMovingAverageList(stockData, maType, length, ema3List);
        var ema5List = GetMovingAverageList(stockData, maType, length, ema4List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];
            var ema3 = ema3List[i];
            var ema4 = ema4List[i];
            var ema5 = ema5List[i];

            var prevQema = GetLastOrDefault(qemaList);
            var qema = BinomialCascadeWindow.Combine(false, ema1, ema2, ema3, ema4, ema5);
            qemaList.Add(qema);

            var signal = GetCompareSignal(currentValue - qema, prevValue - prevQema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Qema", qemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(qemaList);
        stockData.IndicatorName = IndicatorName.QuadrupleExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quadratic Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="forecastLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateQuadraticLeastSquaresMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, int forecastLength = 14)
    {
        if (maType == MovingAvgType.SimpleMovingAverage && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var (prices, _, _, _, _) = GetInputValuesList(stockData);
            using var exact = new Streaming.QuadraticLeastSquaresWindow(length);
            List<double> values = new(prices.Count), forecasts = new(prices.Count); var signals = CreateSignalsList(stockData);
            for (var i = 0; i < prices.Count; i++)
            {
                var point = exact.Next(prices[i], forecastLength, true); values.Add(point.Value); forecasts.Add(point.Forecast);
                signals?.Add(GetCompareSignal(prices[i] - point.Value, (i > 0 ? prices[i - 1] : 0) - (i > 0 ? values[i - 1] : 0)));
            }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Qlma", values }, { "Forecast", forecasts } });
            stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.QuadraticLeastSquaresMovingAverage;
            return stockData;
        }
        List<double> nList = new(stockData.Count);
        List<double> n2List = new(stockData.Count);
        List<double> nn2List = new(stockData.Count);
        List<double> nn2CovList = new(stockData.Count);
        List<double> n2vList = new(stockData.Count);
        List<double> n2vCovList = new(stockData.Count);
        List<double> nvList = new(stockData.Count);
        List<double> nvCovList = new(stockData.Count);
        List<double> qlsmaList = new(stockData.Count);
        List<double> fcastList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var stableFit = maType == MovingAvgType.SimpleMovingAverage && !Builder.Compute.ComponentAverage.HasOverrides ? new Streaming.QuadraticLeastSquaresWindow(length) : null;
        var smaList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(inputList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double n = i;
            nList.Add(n);

            var n2 = Pow(n, 2);
            n2List.Add(n2);

            var nn2 = n * n2;
            nn2List.Add(nn2);

            var n2v = n2 * currentValue;
            n2vList.Add(n2v);

            var nv = n * currentValue;
            nvList.Add(nv);
        }

        var nSmaList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(nList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, nList);
        var n2SmaList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(n2List), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, n2List);
        var n2vSmaList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(n2vList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, n2vList);
        var nvSmaList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(nvList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, nvList);
        var nn2SmaList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(nn2List), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, nn2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nSma = nSmaList[i];
            var n2Sma = n2SmaList[i];
            var n2vSma = n2vSmaList[i];
            var nvSma = nvSmaList[i];
            var nn2Sma = nn2SmaList[i];
            var sma = smaList[i];

            var nn2Cov = nn2Sma - (nSma * n2Sma);
            nn2CovList.Add(nn2Cov);

            var n2vCov = n2vSma - (n2Sma * sma);
            n2vCovList.Add(n2vCov);

            var nvCov = nvSma - (nSma * sma);
            nvCovList.Add(nvCov);
        }

        // norm below is the determinant of a 2x2 covariance matrix, and nn2Cov, n2vCov and nvCov are all
        // genuine covariances over these windows. So these two terms have to be the variances of the same
        // windows. CalculateStandardDeviationVolatility is neither a variance nor a deviation about the
        // window's own mean - it is the mean squared residual from the moving-average line, rooted - so the
        // normal equations were being solved against mismatched quantities. See issue #223.
        var nDevList = GetStandardDeviationList(nList, length);
        var n2DevList = GetStandardDeviationList(n2List, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var n2Dev = n2DevList[i];
            var nDev = nDevList[i];
            var n2Variance = n2Dev * n2Dev;
            var nVariance = nDev * nDev;
            var nn2Cov = nn2CovList[i];
            var n2vCov = n2vCovList[i];
            var nvCov = nvCovList[i];
            var sma = smaList[i];
            var n2Sma = n2SmaList[i];
            var nSma = nSmaList[i];
            var n2 = n2List[i];
            var norm = (n2Variance * nVariance) - Pow(nn2Cov, 2);
            var a = norm != 0 ? ((n2vCov * nVariance) - (nvCov * nn2Cov)) / norm : 0;
            var b = norm != 0 ? ((nvCov * n2Variance) - (n2vCov * nn2Cov)) / norm : 0;
            var c = sma - (a * n2Sma) - (b * nSma);

            var prevQlsma = GetLastOrDefault(qlsmaList);
            var qlsma = (a * n2) + (b * i) + c;
            var stable = stableFit?.Next(currentValue, forecastLength, true);
            if (stable.HasValue) qlsma = stable.Value.Value;
            qlsmaList.Add(qlsma);

            var fcast = (a * Pow(i + forecastLength, 2)) + (b * (i + forecastLength)) + c;
            if (stable.HasValue) fcast = stable.Value.Forecast;
            fcastList.Add(fcast);

            var signal = GetCompareSignal(currentValue - qlsma, prevValue - prevQlsma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Qlma", qlsmaList },
            { "Forecast", fcastList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(qlsmaList);
        stockData.IndicatorName = IndicatorName.QuadraticLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quadratic Regression
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateQuadraticRegression(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 500)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); length = Math.Max(1, length);
        List<double>? xm = null, qm = null, ym = null;
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var indices = Enumerable.Range(0, input.Count).Select(i => (double)i).ToList(); var squares = indices.Select(i => i * i).ToList();
            xm = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(indices), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, indices);
            qm = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(squares), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, squares);
            ym = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, input);
        }
        using var window = new QuadraticProjectionWindow(maType, length, Math.Max(1, input.Count));
        List<double> values = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var fit = window.Next(input[i], true, xm?[i], qm?[i], ym?[i]);
            signals?.Add(GetCompareSignal(input[i] - fit, i == 0 ? 0 : input[i - 1] - values[i - 1])); values.Add(fit);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "QuadReg", values } }); stockData.SetSignals(signals);
        stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.QuadraticRegression; return stockData;
    }


    /// <summary>
    /// Calculates the Optimal Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateOptimalWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        using var window = new OptimalWeightedWindow(length);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Owma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.OptimalWeightedMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Overshoot Reduction Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateOvershootReductionMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        length = Math.Max(1, length);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new OvershootWindow(maType, length, initializeFallback: false);
        List<double> line = new(input.Count); var signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var indices = Enumerable.Range(0, input.Count).Select(i => (double)i).ToList();
            var indexMean = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(indices), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, indices);
            var priceMean = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, input);
            for (var i = 0; i < input.Count; i++) line.Add(window.Next(input[i], true, priceMean[i], indexMean[i]));
        }
        else foreach (var price in input) line.Add(window.Next(price, true));
        for (var i = 0; i < input.Count; i++)
        {
            var previousPrice = i == 0 ? 0 : input[i - 1]; var previous = i > 0 && line[i - 1] != 0 ? line[i - 1] : previousPrice;
            signals?.Add(GetCompareSignal(input[i] - line[i], previousPrice - previous));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Orma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.OvershootReductionMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNaturalMovingAverage(this StockData stockData, int length = 40)
    {
        List<double> nmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        using var mean = new NaturalWindowMean(Math.Max(1, Math.Min(length, stockData.Count)));
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevNma = GetLastOrDefault(nmaList);
            var nma = mean.Next(currentValue, true);
            nmaList.Add(nma);
            signalsList?.Add(GetCompareSignal(currentValue - nma, prevValue - prevNma));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nma", nmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmaList);
        stockData.IndicatorName = IndicatorName.NaturalMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the McNicholl Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMcNichollMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20)
    {
        length = Math.Max(2, length);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var custom = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? first = null, second = null;
        using var window = new McNichollWindow(maType, length, initializeFallback: !custom);
        if (custom)
        {
            first = GetMovingAverageList(stockData, maType, length, input);
            second = GetMovingAverageList(stockData, maType, length, first);
        }
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true, first?[i], second?[i]);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Mnma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.McNichollMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Pentuple Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePentupleExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> pemaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);
        var ema4List = GetMovingAverageList(stockData, maType, length, ema3List);
        var ema5List = GetMovingAverageList(stockData, maType, length, ema4List);
        var ema6List = GetMovingAverageList(stockData, maType, length, ema5List);
        var ema7List = GetMovingAverageList(stockData, maType, length, ema6List);
        var ema8List = GetMovingAverageList(stockData, maType, length, ema7List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];
            var ema3 = ema3List[i];
            var ema4 = ema4List[i];
            var ema5 = ema5List[i];
            var ema6 = ema6List[i];
            var ema7 = ema7List[i];
            var ema8 = ema8List[i];

            var prevPema = GetLastOrDefault(pemaList);
            var pema = BinomialCascadeWindow.Combine(true, ema1, ema2, ema3, ema4, ema5, ema6, ema7, ema8);
            pemaList.Add(pema);

            var signal = GetCompareSignal(currentValue - pema, prevValue - prevPema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pema", pemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pemaList);
        stockData.IndicatorName = IndicatorName.PentupleExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Polynomial Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePolynomialLeastSquaresMovingAverage(this StockData stockData, int length = 100)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new PolynomialCellWindow(length); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Plsma", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.PolynomialLeastSquaresMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Parametric Corrective Linear Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <param name="per"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateParametricCorrectiveLinearMovingAverage(this StockData stockData, int length = 50, double alpha = 1,
        double per = 35)
    {
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var first = new ParametricCorrectiveWindow(length, alpha, per); var second = new ParametricCorrectiveWindow(length, alpha, per, true);
        double previousDifference = 0;
        for (var i = 0; i < input.Count; i++)
        {
            var value = first.Next(input[i], true); var other = second.Next(input[i], true); var difference = value - other;
            signals?.Add(GetCompareSignal(difference, previousDifference)); previousDifference = difference; line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Pclma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.ParametricCorrectiveLinearMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Parabolic Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateParabolicWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> pwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new IntegerPowerWindowMean(length, 2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevPwma = GetLastOrDefault(pwmaList);
            var pwma = mean.Next(currentValue, true);
            pwmaList.Add(pwma);

            var signal = GetCompareSignal(currentValue - pwma, prevVal - prevPwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pwma", pwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pwmaList);
        stockData.IndicatorName = IndicatorName.ParabolicWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Parametric Kalman Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateParametricKalmanFilter(this StockData stockData, int length = 50)
    {
        List<double> errList = new(stockData.Count);
        List<double> estList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : currentValue;
            var priorEst = i >= length ? estList[i - length] : prevValue;
            var errMea = Math.Abs(priorEst - currentValue);
            var errPrv = Math.Abs(MinPastValues(i, 1, currentValue - prevValue) * -1);
            var prevErr = i >= 1 ? errList[i - 1] : errPrv;
            // A gain of prevErr / (prevErr + errMea) is 0/0 when neither the estimate nor the measurement
            // carries any error - on a series that never moves, every bar. Holding the prior estimate there
            // pins the filter to whatever it was seeded with; with nothing to disbelieve, take the measurement.
            var kg = prevErr + errMea != 0 ? prevErr / (prevErr + errMea) : 1;
            var prevEst = i >= 1 ? estList[i - 1] : prevValue;

            var est = prevEst + (kg * (currentValue - prevEst));
            estList.Add(est);

            var err = (1 - kg) * errPrv;
            errList.Add(err);

            var signal = GetCompareSignal(currentValue - est, prevValue - prevEst);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pkf", estList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(estList);
        stockData.IndicatorName = IndicatorName.ParametricKalmanFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the R2 Adaptive Regression
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateR2AdaptiveRegression(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> outList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> x2List = new(stockData.Count);
        List<double> x2PowList = new(stockData.Count);
        List<double> y1List = new(stockData.Count);
        List<double> y2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation x2CorrWindow = new();
        RollingCorrelation y1CorrWindow = new();
        RollingCorrelation y2CorrWindow = new();
        RollingSum x2SumWindow = new();
        RollingSum x2PowSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var linregList = CalculateLinearRegression(stockData, length).ChainedValues;
        // The slope below is sigma_y * r^2 / sigma_x, and sigma_x is built a few lines down as the root of
        // the mean squared distance from that window's own mean. Taking the numerator from
        // CalculateStandardDeviationVolatility made the two halves of one ratio two different quantities -
        // the method already demonstrates which one it wants. Reading the price window's deviation directly
        // also removes the capture/restore that existed only to keep the regression line out of this call.
        // See issue #223.
        var stdDevList = GetStandardDeviationList(inputList, length);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDev = stdDevList[i];
            var sma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentValue = inputList[i];
            tempList.Add(currentValue);
            var y1 = linregList[i];
            y1List.Add(y1);
            y1CorrWindow.Add(y1, currentValue);

            var x2 = i >= 1 ? outList[i - 1] : currentValue;
            x2List.Add(x2);
            x2SumWindow.Add(x2);
            x2CorrWindow.Add(x2, currentValue);

            var r2x2 = x2CorrWindow.R(length);
            r2x2 = IsValueNullOrInfinity(r2x2) ? 0 : r2x2;
            var x2Avg = x2SumWindow.Average(length);
            var x2Dev = x2 - x2Avg;

            var x2Pow = Pow(x2Dev, 2);
            x2PowList.Add(x2Pow);
            x2PowSumWindow.Add(x2Pow);

            var x2PowAvg = x2PowSumWindow.Average(length);
            var x2StdDev = x2PowAvg >= 0 ? Sqrt(x2PowAvg) : 0;
            var a = x2StdDev != 0 ? stdDev * (double)r2x2 / x2StdDev : 0;
            var b = sma - (a * x2Avg);

            var y2 = (a * x2) + b;
            y2List.Add(y2);
            y2CorrWindow.Add(y2, currentValue);

            var ry1 = Math.Pow(y1CorrWindow.R(length), 2);
            ry1 = IsValueNullOrInfinity(ry1) ? 0 : ry1;
            var ry2 = Math.Pow(y2CorrWindow.R(length), 2);
            ry2 = IsValueNullOrInfinity(ry2) ? 0 : ry2;

            var prevOutVal = GetLastOrDefault(outList);
            var outval = ((double)ry1 * y1) + ((double)ry2 * y2) + ((1 - (double)(ry1 + ry2)) * x2);
            outList.Add(outval);

            var signal = GetCompareSignal(currentValue - outval, prevValue - prevOutVal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "R2ar", outList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(outList);
        stockData.IndicatorName = IndicatorName.R2AdaptiveRegression;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ratio OCHL Averager
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRatioOCHLAverager(this StockData stockData)
    {
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var b = currentHigh - currentLow != 0 ? Math.Abs(currentValue - currentOpen) / (currentHigh - currentLow) : 0;
            var c = b > 1 ? 1 : b;

            var prevD = i >= 1 ? dList[i - 1] : currentValue;
            var d = (c * currentValue) + ((1 - c) * prevD);
            dList.Add(d);

            var signal = GetCompareSignal(currentValue - d, prevValue - prevD);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rochla", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dList);
        stockData.IndicatorName = IndicatorName.RatioOCHLAverager;

        return stockData;
    }


    /// <summary>
    /// Calculates the Regularized Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lambda"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRegularizedExponentialMovingAverage(this StockData stockData, int length = 14, double lambda = 0.5)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new RegularizedWindow(length, lambda);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rema", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.RegularizedExponentialMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Repulsion Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRepulsionMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 100)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var first = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), RepulsionWindow.Period(length, 1))?.ToList() ?? GetMovingAverageList(stockData, maType, RepulsionWindow.Period(length, 1), input);
            var second = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), RepulsionWindow.Period(length, 2))?.ToList() ?? GetMovingAverageList(stockData, maType, RepulsionWindow.Period(length, 2), input);
            var third = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), RepulsionWindow.Period(length, 3))?.ToList() ?? GetMovingAverageList(stockData, maType, RepulsionWindow.Period(length, 3), input);
            line = first.Select((v, i) => RepulsionWindow.Combine(v, second[i], third[i])).ToList();
        }
        else
        {
            using var window = new RepulsionWindow(maType, length);
            foreach (var price in input) line.Add(window.Next(price, true));
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? 0 : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.RepulsionMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Retention Acceleration Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRetentionAccelerationFilter(this StockData stockData, int length = 50)
    {
        List<double> altmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(highList, lowList, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList, lowList, length * 2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highest1 = highestList1[i];
            var lowest1 = lowestList1[i];
            var highest2 = highestList2[i];
            var lowest2 = lowestList2[i];
            var ar = 2 * (highest1 - lowest1);
            var br = 2 * (highest2 - lowest2);
            var k1 = ar != 0 ? (1 - ar) / ar : 0;
            var k2 = br != 0 ? (1 - br) / br : 0;
            var alpha = k1 != 0 ? k2 / k1 : 0;
            var r1 = alpha != 0 && highest1 >= 0 ? Sqrt(highest1) / 4 * ((alpha - 1) / alpha) * (k2 / (k2 + 1)) : 0;
            var r2 = highest2 >= 0 ? Sqrt(highest2) / 4 * (alpha - 1) * (k1 / (k1 + 1)) : 0;
            var factor = r1 != 0 ? r2 / r1 : 0;
            var altk = Pow(factor >= 1 ? 1 : factor, Sqrt(length)) * ((double)1 / length);

            var prevAltma = i >= 1 ? altmaList[i - 1] : currentValue;
            var altma = (altk * currentValue) + ((1 - altk) * prevAltma);
            altmaList.Add(altma);

            var signal = GetCompareSignal(currentValue - altma, prevValue - prevAltma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Raf", altmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(altmaList);
        stockData.IndicatorName = IndicatorName.RetentionAccelerationFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Reverse Engineering Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="rsiLevel"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateReverseEngineeringRelativeStrengthIndex(this StockData stockData, int length = 14, double rsiLevel = 50)
    {
        List<double> aucList = new(stockData.Count);
        List<double> adcList = new(stockData.Count);
        List<double> revRsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double expPeriod = (2 * length) - 1;
        var k = 2 / (expPeriod + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevAuc = i >= 1 ? aucList[i - 1] : 1;
            var prevAdc = i >= 1 ? adcList[i - 1] : 1;

            var auc = currentValue > prevValue ? (k * MinPastValues(i, 1, currentValue - prevValue)) + ((1 - k) * prevAuc) : (1 - k) * prevAuc;
            aucList.Add(auc);

            var adc = currentValue > prevValue ? ((1 - k) * prevAdc) : (k * MinPastValues(i, 1, prevValue - currentValue)) + ((1 - k) * prevAdc);
            adcList.Add(adc);

            var rsiValue = (length - 1) * ((adc * rsiLevel / (100 - rsiLevel)) - auc);
            var prevRevRsi = GetLastOrDefault(revRsiList);
            var revRsi = rsiValue >= 0 ? currentValue + rsiValue : currentValue + (rsiValue * (100 - rsiLevel) / rsiLevel);
            revRsiList.Add(revRsi);

            var signal = GetCompareSignal(currentValue - revRsi, prevValue - prevRevRsi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rersi", revRsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(revRsiList);
        stockData.IndicatorName = IndicatorName.ReverseEngineeringRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Right Sided Ricker Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="pctWidth"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRightSidedRickerMovingAverage(this StockData stockData, int length = 50, double pctWidth = 60)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new RickerWindow(length, pctWidth); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rsrma", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.RightSidedRickerMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Recursive Moving Trend Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRecursiveMovingTrendAverage(this StockData stockData, int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new RecursiveTrendWindow(length);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? -input[i] : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rmta", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.RecursiveMovingTrendAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Reverse Moving Average Convergence Divergence
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="macdLevel"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateReverseMovingAverageConvergenceDivergence(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 12, int slowLength = 26, int signalLength = 9,
        double macdLevel = 0)
    {
        List<double> pMacdEqList = new(stockData.Count);
        List<double> histogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastAlpha = 2d / (1d + Math.Max(1, fastLength));
        var slowAlpha = 2d / (1d + Math.Max(1, slowLength));

        var fastEmaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, fastLength) : GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, slowLength) : GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevFastEma = i >= 1 ? fastEmaList[i - 1] : 0;
            var prevSlowEma = i >= 1 ? slowEmaList[i - 1] : 0;

            var pMacdEq = RoundedReverseMacd.Equilibrium(prevFastEma, prevSlowEma, fastAlpha, slowAlpha);
            pMacdEqList.Add(pMacdEq);
        }

        var finiteInput = FiniteSignalInput.Create(pMacdEqList, out var finiteCount);
        var pMacdEqSignalList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(finiteInput, signalLength)
            : GetMovingAverageList(stockData, maType, signalLength, finiteInput);
        for (var i = finiteCount; i < pMacdEqSignalList.Count; i++) pMacdEqSignalList[i] = double.NaN;
        for (var i = 0; i < stockData.Count; i++)
        {
            var pMacdEq = pMacdEqList[i];
            var pMacdEqSignal = pMacdEqSignalList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevPMacdEq = i >= 1 ? pMacdEqList[i - 1] : 0;

            var macdHistogram = pMacdEq - pMacdEqSignal;
            histogramList.Add(macdHistogram);

            var signal = GetCompareSignal(currentValue - pMacdEq, prevValue - prevPMacdEq);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rmacd", pMacdEqList },
            { "Signal", pMacdEqSignalList },
            { "Histogram", histogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pMacdEqList);
        stockData.IndicatorName = IndicatorName.ReverseMovingAverageConvergenceDivergence;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average Adaptive Q
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastAlpha"></param>
    /// <param name="slowAlpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMovingAverageAdaptiveQ(this StockData stockData, int length = 10, double fastAlpha = 0.667, 
        double slowAlpha = 0.0645)
    {
        List<double> maaqList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).ChainedOutputs["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMaaq = i >= 1 ? maaqList[i - 1] : currentValue;
            var er = erList[i];
            var temp = (er * fastAlpha) + slowAlpha;

            var maaq = prevMaaq + (Pow(temp, 2) * (currentValue - prevMaaq));
            maaqList.Add(maaq);

            var signal = GetCompareSignal(currentValue - maaq, prevValue - prevMaaq);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Maaq", maaqList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(maaqList);
        stockData.IndicatorName = IndicatorName.MovingAverageAdaptiveQ;

        return stockData;
    }


    /// <summary>
    /// Calculates the McGinley Dynamic Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMcGinleyDynamicIndicator(this StockData stockData, int length = 14, double k = 0.6)
    {
        List<double> mdiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevMdi = i >= 1 ? GetLastOrDefault(mdiList) : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ratio = prevMdi != 0 ? currentValue / prevMdi : 0;
            var bottom = k * length * Pow(ratio, 4);

            var mdi = bottom != 0 ? prevMdi + ((currentValue - prevMdi) / Math.Max(bottom, 1)) : currentValue;
            mdiList.Add(mdi);

            var signal = GetCompareSignal(currentValue - mdi, prevValue - prevMdi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mdi", mdiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mdiList);
        stockData.IndicatorName = IndicatorName.McGinleyDynamicIndicator;

        return stockData;
    }

    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMiddleHighLowMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 14, int length2 = 10)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mhlList = CalculateMidpoint(stockData, length2).ChainedValues;
        List<double> mhlMaList;
        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            using var smoother = new Streaming.RoundedSimpleMovingAverageSmoother(length1);
            mhlMaList = mhlList.Select(value => smoother.Next(value, true)).ToList();
        }
        else mhlMaList = GetMovingAverageList(stockData, maType, length1, mhlList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentMhlMa = mhlMaList[i];
            var prevMhlma = i >= 1 ? mhlMaList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - currentMhlMa, prevValue - prevMhlma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mhlma", mhlMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mhlMaList);
        stockData.IndicatorName = IndicatorName.MiddleHighLowMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average V3
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMovingAverageV3(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 14, int length2 = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var first = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), length1)?.ToList() ?? GetMovingAverageList(stockData, maType, length1, input);
            var second = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), length2)?.ToList() ?? GetMovingAverageList(stockData, maType, length2, input);
            for (var i = 0; i < input.Count; i++) line.Add(MovingAverageV3Window.Combine(first[i], second[i], length1, length2));
        }
        else
        {
            using var window = new MovingAverageV3Window(maType, length1, length2);
            foreach (var price in input) line.Add(window.Next(price, true));
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? 0 : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Mav3", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.MovingAverageV3;
        return stockData;
    }


    /// <summary>
    /// Calculates the Multi Depth Zero Lag Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMultiDepthZeroLagExponentialMovingAverage(this StockData stockData, int length = 50)
    {
        List<double> alpha1List = new(stockData.Count);
        List<double> beta1List = new(stockData.Count);
        List<double> alpha2List = new(stockData.Count);
        List<double> beta2List = new(stockData.Count);
        List<double> alpha3List = new(stockData.Count);
        List<double> beta3List = new(stockData.Count);
        List<double> mda1List = new(stockData.Count);
        List<double> mda2List = new(stockData.Count);
        List<double> mda3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = (double)2 / (length + 1);
        var a2 = Exp(-Sqrt(2) * Math.PI / length);
        var a3 = Exp(-Math.PI / length);
        var b2 = 2 * a2 * Math.Cos(Sqrt(2) * Math.PI / length);
        var b3 = 2 * a3 * Math.Cos(Sqrt(3) * Math.PI / length);
        var c = Exp(-2 * Math.PI / length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevAlpha1 = i >= 1 ? alpha1List[i - 1] : currentValue;
            var alpha1 = (a1 * currentValue) + ((1 - a1) * prevAlpha1);
            alpha1List.Add(alpha1);

            var prevAlpha2 = i >= 1 ? alpha2List[i - 1] : currentValue;
            var priorAlpha2 = i >= 2 ? alpha2List[i - 2] : currentValue;
            var alpha2 = (b2 * prevAlpha2) - (a2 * a2 * priorAlpha2) + ((1 - b2 + (a2 * a2)) * currentValue);
            alpha2List.Add(alpha2);

            var prevAlpha3 = i >= 1 ? alpha3List[i - 1] : currentValue;
            var prevAlpha3_2 = i >= 2 ? alpha3List[i - 2] : currentValue;
            var prevAlpha3_3 = i >= 3 ? alpha3List[i - 3] : currentValue;
            var alpha3 = ((b3 + c) * prevAlpha3) - ((c + (b3 * c)) * prevAlpha3_2) + (c * c * prevAlpha3_3) + ((1 - b3 + c) * (1 - c) * currentValue);
            alpha3List.Add(alpha3);

            var detrend1 = currentValue - alpha1;
            var detrend2 = currentValue - alpha2;
            var detrend3 = currentValue - alpha3;

            var prevBeta1 = i >= 1 ? beta1List[i - 1] : 0;
            var beta1 = (a1 * detrend1) + ((1 - a1) * prevBeta1);
            beta1List.Add(beta1);

            var prevBeta2 = i >= 1 ? beta2List[i - 1] : 0;
            var prevBeta2_2 = i >= 2 ? beta2List[i - 2] : 0;
            var beta2 = (b2 * prevBeta2) - (a2 * a2 * prevBeta2_2) + ((1 - b2 + (a2 * a2)) * detrend2);
            beta2List.Add(beta2);

            var prevBeta3 = i >= 1 ? beta3List[i - 1] : 0;
            var prevBeta3_2 = i >= 2 ? beta3List[i - 2] : 0;
            var prevBeta3_3 = i >= 3 ? beta3List[i - 3] : 0;
            var beta3 = ((b3 + c) * prevBeta3) - ((c + (b3 * c)) * prevBeta3_2) + (c * c * prevBeta3_3) + ((1 - b3 + c) * (1 - c) * detrend3);
            beta3List.Add(beta3);

            var mda1 = alpha1 + ((double)1 / 1 * beta1);
            mda1List.Add(mda1);

            var prevMda2 = GetLastOrDefault(mda2List);
            var mda2 = alpha2 + ((double)1 / 2 * beta2);
            mda2List.Add(mda2);

            var mda3 = alpha3 + ((double)1 / 3 * beta3);
            mda3List.Add(mda3);

            var signal = GetCompareSignal(currentValue - mda2, prevValue - prevMda2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Md2Pole", mda2List },
            { "Md1Pole", mda1List },
            { "Md3Pole", mda3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mda2List);
        stockData.IndicatorName = IndicatorName.MultiDepthZeroLagExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Modular Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="beta"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateModularFilter(this StockData stockData, int length = 200, double beta = 0.8, double z = 0.5)
    {
        List<double> b2List = new(stockData.Count);
        List<double> c2List = new(stockData.Count);
        List<double> os2List = new(stockData.Count);
        List<double> ts2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevB2 = i >= 1 ? b2List[i - 1] : currentValue;
            var b2 = currentValue > (alpha * currentValue) + ((1 - alpha) * prevB2) ? currentValue : (alpha * currentValue) + ((1 - alpha) * prevB2);
            b2List.Add(b2);

            var prevC2 = i >= 1 ? c2List[i - 1] : currentValue;
            var c2 = currentValue < (alpha * currentValue) + ((1 - alpha) * prevC2) ? currentValue : (alpha * currentValue) + ((1 - alpha) * prevC2);
            c2List.Add(c2);

            var prevOs2 = GetLastOrDefault(os2List);
            var os2 = currentValue == b2 ? 1 : currentValue == c2 ? 0 : prevOs2;
            os2List.Add(os2);

            var upper2 = (beta * b2) + ((1 - beta) * c2);
            var lower2 = (beta * c2) + ((1 - beta) * b2);

            var prevTs2 = GetLastOrDefault(ts2List);
            var ts2 = (os2 * upper2) + ((1 - os2) * lower2);
            ts2List.Add(ts2);

            var signal = GetCompareSignal(currentValue - ts2, prevValue - prevTs2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mf", ts2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ts2List);
        stockData.IndicatorName = IndicatorName.ModularFilter;

        return stockData;
    }

}

