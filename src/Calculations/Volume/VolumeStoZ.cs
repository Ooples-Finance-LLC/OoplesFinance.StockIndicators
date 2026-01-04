
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Upside Downside Volume
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateUpsideDownsideVolume(this StockData stockData, int length = 50)
    {
        List<double> upVolList = new(stockData.Count);
        List<double> downVolList = new(stockData.Count);
        List<double> upDownVolumeList = new(stockData.Count);
        var upVolSumWindow = new RollingSum();
        var downVolSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var upVol = currentValue > prevValue ? currentVolume : 0;
            upVolList.Add(upVol);
            upVolSumWindow.Add(upVol);

            var downVol = currentValue < prevValue ? currentVolume * -1 : 0;
            downVolList.Add(downVol);
            downVolSumWindow.Add(downVol);

            var upVolSum = upVolSumWindow.Sum(length);
            var downVolSum = downVolSumWindow.Sum(length);

            var prevUpDownVol = i >= 1 ? upDownVolumeList[i - 1] : 0;
            var upDownVol = downVolSum != 0 ? upVolSum / downVolSum : 0;
            upDownVolumeList.Add(upDownVol);

            var signal = GetCompareSignal(upDownVol, prevUpDownVol);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Udv", upDownVolumeList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(upDownVolumeList);
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
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateVolumePositiveNegativeIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, InputName inputName = InputName.TypicalPrice, int length = 30,
        int smoothLength = 3)
    {
        List<double> vmpList = new(stockData.Count);
        List<double> vmnList = new(stockData.Count);
        List<double> vpnList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum vmpSum = new();
        RollingSum vmnSum = new();
        var (inputList, _, _, _, _, volumeList) = GetInputValuesList(inputName, stockData);

        var mavList = GetMovingAverageList(stockData, maType, length, volumeList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

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
    public static StockData CalculateVolumeAccumulationOscillator(this StockData stockData, int length = 14)
    {
        List<double> vaoList = new(stockData.Count);
        List<double> vaoSumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum vaoSumWindow = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i];
            var medianValue = (currentHigh + currentLow) / 2;

            var vao = currentValue != medianValue ? currentVolume * (currentValue - medianValue) : currentVolume;
            vaoList.Add(vao);
            vaoSumWindow.Add(vao);

            var prevVaoSum = i >= 1 ? vaoSumList[i - 1] : 0;
            var vaoSum = vaoSumWindow.Average(length);
            vaoSumList.Add(vaoSum);

            var signal = GetCompareSignal(vaoSum, prevVaoSum);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vao", vaoSumList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vaoSumList);
        stockData.IndicatorName = IndicatorName.VolumeAccumulationOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Accumulation Percent
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeAccumulationPercent(this StockData stockData, int length = 10)
    {
        List<double> vapcList = new(stockData.Count);
        List<double> tvaList = new(stockData.Count);
        List<double> tempVolumeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum volumeSumWindow = new();
        RollingSum tvaSumWindow = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];

            var currentVolume = volumeList[i];
            tempVolumeList.Add(currentVolume);
            volumeSumWindow.Add(currentVolume);

            var xt = currentHigh - currentLow != 0 ? ((2 * currentClose) - currentHigh - currentLow) / (currentHigh - currentLow) : 0;
            var tva = currentVolume * xt;
            tvaList.Add(tva);
            tvaSumWindow.Add(tva);

            var volumeSum = volumeSumWindow.Sum(length);
            var tvaSum = tvaSumWindow.Sum(length);

            var prevVapc = i >= 1 ? vapcList[i - 1] : 0;
            var vapc = volumeSum != 0 ? MinOrMax(100 * tvaSum / volumeSum, 100, 0) : 0;
            vapcList.Add(vapc);

            var signal = GetCompareSignal(vapc, prevVapc);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vapc", vapcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vapcList);
        stockData.IndicatorName = IndicatorName.VolumeAccumulationPercent;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Flow Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="signalLength"></param>
    /// <param name="smoothLength"></param>
    /// <param name="coef"></param>
    /// <param name="vcoef"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeFlowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        InputName inputName = InputName.TypicalPrice, int length1 = 130, int length2 = 30, int signalLength = 5, int smoothLength = 3,
        double coef = 0.2, double vcoef = 2.5)
    {
        List<double> interList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> vcpList = new(stockData.Count);
        List<double> vcpVaveSumList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum vcpSumWindow = new();
        var (inputList, _, _, _, closeList, volumeList) = GetInputValuesList(inputName, stockData);

        var smaVolumeList = GetMovingAverageList(stockData, maType, length1, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var inter = currentValue > 0 && prevValue > 0 ? Math.Log(currentValue) - Math.Log(prevValue) : 0;
            interList.Add(inter);
        }

        stockData.SetCustomValues(interList);
        var vinterList = CalculateStandardDeviationVolatility(stockData, maType, length2).CustomValuesList;
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

            var vcp = mf > cutoff ? vc : mf < cutoff * -1 ? vc * -1 : mf > 0 ? vc : mf < 0 ? vc * -1 : 0;
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
    public static StockData CalculateTwiggsMoneyFlow(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 21)
    {
        List<double> adList = new(stockData.Count);
        List<double> tmfList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        var volumeEmaList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i];
            var prevPrice = i >= 1 ? inputList[i - 1] : 0;
            var trh = Math.Max(currentHigh, prevPrice);
            var trl = Math.Min(currentLow, prevPrice);

            var ad = trh - trl != 0 && currentVolume != 0 ? (currentPrice - trl - (trh - currentPrice)) / (trh - trl) * currentVolume : 0;
            adList.Add(ad);
        }

        var smoothAdList = GetMovingAverageList(stockData, maType, length, adList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEmaVolume = volumeEmaList[i];
            var smoothAd = smoothAdList[i];
            var prevTmf1 = i >= 1 ? tmfList[i - 1] : 0;
            var prevTmf2 = i >= 2 ? tmfList[i - 2] : 0;

            var tmf = currentEmaVolume != 0 ? MinOrMax(smoothAd / currentEmaVolume, 1, -1) : 0;
            tmfList.Add(tmf);

            var signal = GetRsiSignal(tmf - prevTmf1, prevTmf1 - prevTmf2, tmf, prevTmf1, 0.2, -0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tmf", tmfList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tmfList);
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
    public static StockData CalculateTradeVolumeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, double minTickValue = 0.5)
    {
        List<double> tviList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var currentVolume = volumeList[i];
            var prevPrice = i >= 1 ? inputList[i - 1] : 0;
            var priceChange = currentPrice - prevPrice;

            var prevTvi = i >= 1 ? tviList[i - 1] : 0;
            var tvi = priceChange > minTickValue ? prevTvi + currentVolume : priceChange < -minTickValue ?
                prevTvi - currentVolume : prevTvi;
            tviList.Add(tvi);
        }

        var tviSignalList = GetMovingAverageList(stockData, maType, length, tviList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tvi = tviList[i];
            var tviSignal = tviSignalList[i];
            var prevTvi = i >= 1 ? tviList[i - 1] : 0;
            var prevTviSignal = i >= 1 ? tviSignalList[i - 1] : 0;

            var signal = GetCompareSignal(tvi - tviSignal, prevTvi - prevTviSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tvi", tviList },
            { "Signal", tviSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tviList);
        stockData.IndicatorName = IndicatorName.TradeVolumeIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the TFS Volume Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTFSVolumeOscillator(this StockData stockData, int length = 7)
    {
        List<double> totvList = new(stockData.Count);
        List<double> tfsvoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum totvSumWindow = new();
        var (inputList, _, _, openList, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var open = openList[i];
            var close = inputList[i];
            var volume = volumeList[i];

            var totv = close > open ? volume : close < open ? -volume : 0;
            totvList.Add(totv);
            totvSumWindow.Add(totv);

            var totvSum = totvSumWindow.Sum(length);
            var prevTfsvo = i >= 1 ? tfsvoList[i - 1] : 0;
            var tfsvo = length != 0 ? totvSum / length : 0;
            tfsvoList.Add(tfsvo);

            var signal = GetCompareSignal(tfsvo, prevTfsvo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tfsvo", tfsvoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tfsvoList);
        stockData.IndicatorName = IndicatorName.TFSVolumeOscillator;

        return stockData;
    }

}

