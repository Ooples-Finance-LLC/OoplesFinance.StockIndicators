
using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Volume Momentum
    /// </summary>
    /// <remarks>
    /// The change in volume over <paramref name="length"/> bars, in shares rather than as a proportion. A bar
    /// with no volume that far back publishes zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumeMomentum(this StockData stockData, int length = 10)
    {
        length = Math.Max(length, 1);
        var (_, _, _, _, volumeList) = GetInputValuesList(stockData);
        var count = volumeList.Count;
        List<double> volumeMomentumList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var volumeMomentum = i >= length ? volumeList[i] - volumeList[i - length] : 0;
            volumeMomentumList.Add(volumeMomentum);

            var prevMomentum1 = i >= 1 ? volumeMomentumList[i - 1] : 0;
            var prevMomentum2 = i >= 2 ? volumeMomentumList[i - 2] : 0;
            var signal = GetCompareSignal(volumeMomentum - prevMomentum1, prevMomentum1 - prevMomentum2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "VolumeMomentum", volumeMomentumList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(volumeMomentumList);
        stockData.IndicatorName = IndicatorName.VolumeMomentum;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Rate of Change
    /// </summary>
    /// <remarks>
    /// The change in volume over <paramref name="length"/> bars as a percentage of the earlier volume, which
    /// is the volume momentum expressed as a proportion. A bar with no volume that far back, or one whose
    /// earlier volume was zero and so has no proportion to take, publishes zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumeRateOfChange(this StockData stockData, int length = 12)
    {
        length = Math.Max(length, 1);
        var (_, _, _, _, volumeList) = GetInputValuesList(stockData);
        var count = volumeList.Count;
        List<double> vrocList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            double vroc = 0;
            if (i >= length)
            {
                var prevVolume = volumeList[i - length];
                vroc = RoundedPercentageChange.Of(volumeList[i], prevVolume);
            }

            vrocList.Add(vroc);

            var prevVroc1 = i >= 1 ? vrocList[i - 1] : 0;
            var prevVroc2 = i >= 2 ? vrocList[i - 2] : 0;
            var signal = GetCompareSignal(vroc - prevVroc1, prevVroc1 - prevVroc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vroc", vrocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vrocList);
        stockData.IndicatorName = IndicatorName.VolumeRateOfChange;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Oscillator
    /// </summary>
    /// <remarks>
    /// The gap between a short and a long average of volume, as a percentage of the long one: positive while
    /// volume is running above its longer trend, negative while it is running below. A bar whose long average
    /// has not filled, and so is zero, has nothing to take a percentage of and publishes zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumeOscillator(this StockData stockData, int fastLength = 5, int slowLength = 20)
    {
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        var (inputList, _, _, _, volumes) = GetInputValuesList(stockData);
        var volumeList = stockData.ChainedValues.Count > 0 ? inputList : volumes;
        var count = volumeList.Count;
        List<double> volumeOscillatorList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var volumeSpan = SpanCompat.AsReadOnlySpan(volumeList);
        var fastBuffer = SpanCompat.CreateOutputBuffer(count);
        var slowBuffer = SpanCompat.CreateOutputBuffer(count);
        BollingerArithmetic.Mean(volumeSpan, fastBuffer.Span, fastLength);
        BollingerArithmetic.Mean(volumeSpan, slowBuffer.Span, slowLength);

        for (var i = 0; i < count; i++)
        {
            var slowSma = slowBuffer.Span[i];
            var volumeOscillator = RoundedPercentageChange.Of(fastBuffer.Span[i], slowSma);
            volumeOscillatorList.Add(volumeOscillator);

            var prevOscillator1 = i >= 1 ? volumeOscillatorList[i - 1] : 0;
            var prevOscillator2 = i >= 2 ? volumeOscillatorList[i - 2] : 0;
            var signal = GetCompareSignal(volumeOscillator - prevOscillator1, prevOscillator1 - prevOscillator2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vo", volumeOscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(volumeOscillatorList);
        stockData.IndicatorName = IndicatorName.VolumeOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Momentum Oscillator
    /// </summary>
    /// <remarks>
    /// The gap between a short and a long exponential average of volume, as a percentage of the long one.
    /// Distinct from <see cref="CalculateVolumeOscillator"/>, which measures the same gap between two simple
    /// averages: this one weights recent volume more heavily. Both averages start at the first bar's volume
    /// rather than warming up, and that first bar reads zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="shortLength"></param>
    /// <param name="longLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumeMomentumOscillator(this StockData stockData, int shortLength = 5, int longLength = 20)
    {
        shortLength = Math.Max(shortLength, 1);
        longLength = Math.Max(longLength, 1);
        var (inputList, _, _, _, volumes) = GetInputValuesList(stockData);
        var volumeList = stockData.ChainedValues.Count > 0 ? inputList : volumes;
        var count = volumeList.Count;
        List<double> oscillatorList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var shortEma = count > 0 ? volumeList[0] : 0;
        var longEma = count > 0 ? volumeList[0] : 0;

        for (var i = 0; i < count; i++)
        {
            double oscillator = 0;
            if (i >= 1)
            {
                shortEma = RoundedSeededEma.Next(volumeList[i], shortEma, shortLength);
                longEma = RoundedSeededEma.Next(volumeList[i], longEma, longLength);
                oscillator = RoundedPercentageChange.Of(shortEma, longEma);
            }

            oscillatorList.Add(oscillator);

            var prevOscillator1 = i >= 1 ? oscillatorList[i - 1] : 0;
            var prevOscillator2 = i >= 2 ? oscillatorList[i - 2] : 0;
            var signal = GetCompareSignal(oscillator - prevOscillator1, prevOscillator1 - prevOscillator2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vmo", oscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oscillatorList);
        stockData.IndicatorName = IndicatorName.VolumeMomentumOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Zone Oscillator
    /// </summary>
    /// <remarks>
    /// The share of recent volume that belonged to rising bars, as a percentage: volume signed by the bar's
    /// direction is averaged, and divided by the average of the volume itself. A hundred says every recent
    /// bar rose, minus a hundred that every one fell, and zero that the two sides balance.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumeZoneOscillator(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> oscillatorList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var signedVolume = new double[count];
        for (var i = 1; i < count; i++)
        {
            signedVolume[i] = inputList[i] > inputList[i - 1] ? volumeList[i] : -volumeList[i];
        }

        var volumeSpan = SpanCompat.AsReadOnlySpan(volumeList);
        var signedBuffer = SpanCompat.CreateOutputBuffer(count);
        var totalBuffer = SpanCompat.CreateOutputBuffer(count);
        MovingAverageCore.ExponentialMovingAverage(signedVolume, signedBuffer.Span, length);
        MovingAverageCore.ExponentialMovingAverage(volumeSpan, totalBuffer.Span, length);

        for (var i = 0; i < count; i++)
        {
            var totalVolume = totalBuffer.Span[i];
            var oscillator = RoundedMomentumRatio.Of(signedBuffer.Span[i], totalVolume);
            oscillatorList.Add(oscillator);

            var prevOscillator1 = i >= 1 ? oscillatorList[i - 1] : 0;
            var prevOscillator2 = i >= 2 ? oscillatorList[i - 2] : 0;
            var signal = GetCompareSignal(oscillator - prevOscillator1, prevOscillator1 - prevOscillator2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vzo", oscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oscillatorList);
        stockData.IndicatorName = IndicatorName.VolumeZoneOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Upside Downside Volume
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateUpsideDownsideVolume(this StockData stockData, int length = 50)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new VolumeBalanceWindow(length, VolumeBalanceKind.UpsideDownside);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i], input[i], stockData.Volumes[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            output.Add(value); signals?.Add(GetCompareSignal(value, previous));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Udv", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.UpsideDownsideVolume;
        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Price Confirmation Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumePriceConfirmationIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5, int slowLength = 20, int length = 8)
    {
        List<double> vpciList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var vwmaShortList = GetMovingAverageList(stockData, MovingAvgType.VolumeWeightedMovingAverage, fastLength, inputList);
        var vwmaLongList = GetMovingAverageList(stockData, MovingAvgType.VolumeWeightedMovingAverage, slowLength, inputList);
        var volumeSmaShortList = GetMovingAverageList(stockData, maType, fastLength, volumeList);
        var volumeSmaLongList = GetMovingAverageList(stockData, maType, slowLength, volumeList);
        var smaShortList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var smaLongList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var vwmaLong = vwmaLongList[i];
            var vwmaShort = vwmaShortList[i];
            var volumeSmaLong = volumeSmaLongList[i];
            var volumeSmaShort = volumeSmaShortList[i];
            var smaLong = smaLongList[i];
            var smaShort = smaShortList[i];
            var vpc = vwmaLong - smaLong;
            var vpr = smaShort != 0 ? vwmaShort / smaShort : 0;
            var vm = volumeSmaLong != 0 ? volumeSmaShort / volumeSmaLong : 0;

            var vpci = vpc * vpr * vm;
            vpciList.Add(vpci);
        }

        var vpciSmaList = GetMovingAverageList(stockData, maType, length, vpciList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vpci = vpciList[i];
            var vpciSma = vpciSmaList[i];
            var prevVpci = i >= 1 ? vpciList[i - 1] : 0;
            var prevVpciSma = i >= 1 ? vpciSmaList[i - 1] : 0;

            var signal = GetCompareSignal(vpci - vpciSma, prevVpci - prevVpciSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vpci", vpciList },
            { "Signal", vpciSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vpciList);
        stockData.IndicatorName = IndicatorName.VolumePriceConfirmationIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Positive Negative Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumePositiveNegativeIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 30,
        int smoothLength = 3)
    {
        List<double> vmpList = new(stockData.Count);
        List<double> vmnList = new(stockData.Count);
        List<double> vpnList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum vmpSum = new();
        RollingSum vmnSum = new();
        var (inputList, _, _, _, _, volumeList) = GetInputValuesList(InputName.TypicalPrice, stockData);

        var mavList = GetMovingAverageList(stockData, maType, length, volumeList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var mav = mavList[i];
            mav = mav > 0 ? mav : 1;
            var tp = inputList[i];
            var prevTp = i >= 1 ? inputList[i - 1] : 0;
            var atr = atrList[i];
            var currentVolume = volumeList[i];
            var mf = tp - prevTp;
            var mc = 0.1 * atr;

            var vmp = mf > mc ? currentVolume : 0;
            vmpList.Add(vmp);
            vmpSum.Add(vmp);

            var vmn = mf < -mc ? currentVolume : 0;
            vmnList.Add(vmn);
            vmnSum.Add(vmn);

            var vn = vmnSum.Sum(length);
            var vp = vmpSum.Sum(length);

            var vpn = mav != 0 && length != 0 ? (vp - vn) / mav / length * 100 : 0;
            vpnList.Add(vpn);
        }

        var vpnEmaList = GetMovingAverageList(stockData, maType, smoothLength, vpnList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vpnEma = vpnEmaList[i];
            var prevVpnEma = i >= 1 ? vpnEmaList[i - 1] : 0;

            var signal = GetCompareSignal(vpnEma, prevVpnEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vpni", vpnList },
            { "Signal", vpnEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vpnList);
        stockData.IndicatorName = IndicatorName.VolumePositiveNegativeIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Accumulation Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumeAccumulationOscillator(this StockData stockData, int length = 14)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new VolumeBalanceWindow(length, VolumeBalanceKind.Accumulation);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i], input[i], stockData.Volumes[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            output.Add(value); signals?.Add(GetCompareSignal(value, previous));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Vao", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.VolumeAccumulationOscillator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Accumulation Percent
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumeAccumulationPercent(this StockData stockData, int length = 10)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new MoneyFlowPercentWindow(length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(stockData.HighPrices[i], stockData.LowPrices[i], input[i], stockData.Volumes[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            signals?.Add(GetCompareSignal(value, previous)); output.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Vapc", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.VolumeAccumulationPercent;
        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Flow Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="signalLength"></param>
    /// <param name="smoothLength"></param>
    /// <param name="coef"></param>
    /// <param name="vcoef"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumeFlowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 130, int length2 = 30, int signalLength = 5, int smoothLength = 3,
        double coef = 0.2, double vcoef = 2.5)
    {
        List<double> interList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> vcpList = new(stockData.Count);
        List<double> vcpVaveSumList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum vcpSumWindow = new();
        var (inputList, _, _, _, closeList, volumeList) = GetInputValuesList(InputName.TypicalPrice, stockData);

        var smaVolumeList = GetMovingAverageList(stockData, maType, length1, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var inter = currentValue > 0 && prevValue > 0 ? Math.Log(currentValue) - Math.Log(prevValue) : 0;
            interList.Add(inter);
        }

        stockData.SetCustomValues(interList);

        // The deviation of the window about its own mean, not the mean squared residual from a moving average
        // of it. The cutoff is the close times this deviation times a coefficient, and money flow has to clear
        // it to count, so a deviation about 55% high - which is what CalculateStandardDeviationVolatility is
        // on a typical price series - raised the cutoff by the same factor. Taken over interList by name,
        // which is the log returns this measures. See #190.
        var vinterList = GetStandardDeviationList(interList, length2);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vinter = vinterList[i];
            var currentVolume = volumeList[i];
            var currentClose = closeList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevVave = i >= 1 ? tempList[i - 1] : 0;
            var vave = smaVolumeList[i];
            tempList.Add(vave);

            var cutoff = currentClose * vinter * coef;
            var vmax = prevVave * vcoef;
            var vc = Math.Min(currentVolume, vmax);
            var mf = MinPastValues(i, 1, currentValue - prevValue);

            var vcp = mf > cutoff ? vc : mf < -cutoff ? -vc : 0;
            vcpList.Add(vcp);
            vcpSumWindow.Add(vcp);

            var vcpSum = vcpSumWindow.Sum(length1);
            var vcpVaveSum = vave != 0 ? vcpSum / vave : 0;
            vcpVaveSumList.Add(vcpVaveSum);
        }

        var vfiList = GetMovingAverageList(stockData, maType, smoothLength, vcpVaveSumList);
        var vfiEmaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, signalLength, vfiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vfi = vfiList[i];
            var vfima = vfiEmaList[i];

            var prevD = i >= 1 ? dList[i - 1] : 0;
            var d = vfi - vfima;
            dList.Add(d);

            var signal = GetCompareSignal(d, prevD);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vfi", vfiList },
            { "Signal", vfiEmaList },
            { "Histogram", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vfiList);
        stockData.IndicatorName = IndicatorName.VolumeFlowIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Twiggs Money Flow
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTwiggsMoneyFlow(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 21)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new MoneyFlowPercentWindow(length, maType);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(stockData.HighPrices[i], stockData.LowPrices[i], input[i], stockData.Volumes[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            signals?.Add(GetRsiSignal(value - previous, previous - (i < 2 ? 0 : output[i - 2]), value, previous, .2, -.2)); output.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Tmf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.TwiggsMoneyFlow;
        return stockData;
    }


    /// <summary>
    /// Calculates the Trade Volume Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="minTickValue"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTradeVolumeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, double minTickValue = 0.5)
    {
        List<double> output = new(stockData.Count), signal = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var total = new TradeVolumeTotal(minTickValue);
        var standard = StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides;
        using var average = standard ? new RocBankAverage(maType, length, stockData.Count) : null;
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = total.Next(input[i], stockData.Volumes[i], true);
            output.Add(value.Publish());
            if (standard) signal.Add(average!.Next(value, true).Publish());
        }
        if (!standard) signal = GetMovingAverageList(stockData, maType, length, output);
        for (var i = 0; i < stockData.Count; i++) signals?.Add(GetCompareSignal(output[i] - signal[i], i == 0 ? 0 : output[i - 1] - signal[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Tvi", output }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.TradeVolumeIndex;
        return stockData;
    }


    /// <summary>
    /// Calculates the TFS Volume Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTFSVolumeOscillator(this StockData stockData, int length = 7)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new VolumeBalanceWindow(length, VolumeBalanceKind.Tfs);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i], input[i], stockData.Volumes[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            output.Add(value); signals?.Add(GetCompareSignal(value, previous));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Tfsvo", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.TFSVolumeOscillator;
        return stockData;
    }

}

