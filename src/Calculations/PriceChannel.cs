//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Price Channel
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="pct">The PCT.</param>
    /// <returns></returns>
    public static StockData CalculatePriceChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21, 
        double pct = 0.06)
    {
        List<double> upperPriceChannelList = new();
        List<double> lowerPriceChannelList = new();
        List<double> midPriceChannelList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var upperPriceChannel = currentEma * (1 + pct);
            upperPriceChannelList.AddRounded(upperPriceChannel);

            var lowerPriceChannel = currentEma * (1 - pct);
            lowerPriceChannelList.AddRounded(lowerPriceChannel);

            var prevMidPriceChannel = midPriceChannelList.LastOrDefault();
            var midPriceChannel = (upperPriceChannel + lowerPriceChannel) / 2;
            midPriceChannelList.AddRounded(midPriceChannel);

            var signal = GetCompareSignal(currentValue - midPriceChannel, prevValue - prevMidPriceChannel);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperChannel", upperPriceChannelList },
            { "LowerChannel", lowerPriceChannelList },
            { "MiddleChannel", midPriceChannelList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.PriceChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the donchian channels.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateDonchianChannels(this StockData stockData, int length = 20)
    {
        List<double> upperChannelList = new();
        List<double> lowerChannelList = new();
        List<double> middleChannelList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var upperChannel = highestList[i];
            upperChannelList.AddRounded(upperChannel);

            var lowerChannel = lowestList[i];
            lowerChannelList.AddRounded(lowerChannel);

            var prevMiddleChannel = middleChannelList.LastOrDefault();
            var middleChannel = (upperChannel + lowerChannel) / 2;
            middleChannelList.AddRounded(middleChannel);

            var signal = GetCompareSignal(currentValue - middleChannel, prevValue - prevMiddleChannel);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperChannel", upperChannelList },
            { "LowerChannel", lowerChannelList },
            { "MiddleChannel", middleChannelList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.DonchianChannels;

        return stockData;
    }

    /// <summary>
    /// Calculates the standard deviation channel.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="stdDevMult">The standard dev mult.</param>
    /// <returns></returns>
    public static StockData CalculateStandardDeviationChannel(this StockData stockData, int length = 40, double stdDevMult = 2)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDeviationList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
        var regressionList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = regressionList[i];
            var currentValue = inputList[i];
            var currentStdDev = stdDeviationList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? regressionList[i - 1] : 0;

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = middleBand + (currentStdDev * stdDevMult);
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = middleBand - (currentStdDev * stdDevMult);
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", regressionList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.StandardDeviationChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the stoller average range channels.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="atrMult">The atr mult.</param>
    /// <returns></returns>
    public static StockData CalculateStollerAverageRangeChannels(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14, double atrMult = 2)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var currentAtr = atrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = middleBand + (currentAtr * atrMult);
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = middleBand - (currentAtr * atrMult);
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.StollerAverageRangeChannels;

        return stockData;
    }

    /// <summary>
    /// Calculates the Moving Average Channel
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> midChannelList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var highMaList = GetMovingAverageList(stockData, maType, length, highList);
        var lowMaList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperChannel = highMaList[i];
            var lowerChannel = lowMaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMidChannel = midChannelList.LastOrDefault();
            var midChannel = (upperChannel + lowerChannel) / 2;
            midChannelList.AddRounded(midChannel);

            var signal = GetCompareSignal(currentValue - midChannel, prevValue - prevMidChannel);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", highMaList },
            { "MiddleBand", midChannelList },
            { "LowerBand", lowMaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.MovingAverageChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Moving Average Envelope
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="mult">The mult.</param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageEnvelope(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 20, double mult = 0.025)
    {
        List<double> upperEnvelopeList = new();
        List<double> lowerEnvelopeList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma20 = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma20 = i >= 1 ? smaList[i - 1] : 0;
            var factor = currentSma20 * mult;

            var upperEnvelope = currentSma20 + factor;
            upperEnvelopeList.AddRounded(upperEnvelope);

            var lowerEnvelope = currentSma20 - factor;
            lowerEnvelopeList.AddRounded(lowerEnvelope);

            var signal = GetCompareSignal(currentValue - currentSma20, prevValue - prevSma20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperEnvelopeList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerEnvelopeList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.MovingAverageEnvelope;

        return stockData;
    }

    /// <summary>
    /// Calculates the fractal chaos bands.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateFractalChaosBands(this StockData stockData)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<double> middleBandList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevHigh1 = i >= 1 ? highList[i - 1] : 0;
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevHigh3 = i >= 3 ? highList[i - 3] : 0;
            var prevLow1 = i >= 1 ? lowList[i - 1] : 0;
            var prevLow2 = i >= 2 ? lowList[i - 2] : 0;
            var prevLow3 = i >= 3 ? lowList[i - 3] : 0;
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            double oklUpper = prevHigh1 < prevHigh2 ? 1 : 0;
            double okrUpper = prevHigh3 < prevHigh2 ? 1 : 0;
            double oklLower = prevLow1 > prevLow2 ? 1 : 0;
            double okrLower = prevLow3 > prevLow2 ? 1 : 0;

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = oklUpper == 1 && okrUpper == 1 ? prevHigh2 : prevUpperBand;
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = oklLower == 1 && okrLower == 1 ? prevLow2 : prevLowerBand;
            lowerBandList.AddRounded(lowerBand);

            var prevMiddleBand = middleBandList.LastOrDefault();
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.AddRounded(middleBand);

            var signal = GetBollingerBandsSignal(currentClose - middleBand, prevClose - prevMiddleBand, currentClose, prevClose, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.FractalChaosBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Average True Range Channel
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="mult">The mult.</param>
    /// <returns></returns>
    public static StockData CalculateAverageTrueRangeChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14, double mult = 2.5)
    {
        List<double> innerTopAtrChannelList = new();
        List<double> innerBottomAtrChannelList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            var prevTopInner = innerTopAtrChannelList.LastOrDefault();
            var topInner = Math.Round(currentValue + (atr * mult));
            innerTopAtrChannelList.AddRounded(topInner);

            var prevBottomInner = innerBottomAtrChannelList.LastOrDefault();
            var bottomInner = Math.Round(currentValue - (atr * mult));
            innerBottomAtrChannelList.AddRounded(bottomInner);

            var signal = GetBollingerBandsSignal(currentValue - sma, prevValue - prevSma, currentValue, prevValue, topInner,
                prevTopInner, bottomInner, prevBottomInner);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", innerTopAtrChannelList },
            { "MiddleBand", smaList },
            { "LowerBand", innerBottomAtrChannelList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.AverageTrueRangeChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ultimate Moving Average Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="minLength"></param>
    /// <param name="maxLength"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateUltimateMovingAverageBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int minLength = 5, int maxLength = 50, double stdDevMult = 2)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var umaList = CalculateUltimateMovingAverage(stockData, maType, minLength, maxLength, 1).CustomValuesList;
        var stdevList = CalculateStandardDeviationVolatility(stockData, maType, minLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;
            var uma = umaList[i];
            var prevUma = i >= 1 ? umaList[i - 1] : 0;
            var stdev = stdevList[i];

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = uma + (stdDevMult * stdev);
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = uma - (stdDevMult * stdev);
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - uma, prevVal - prevUma, currentValue, prevVal, upperBand, prevUpperBand,
                lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", umaList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.UltimateMovingAverageBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Uni Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="ubFac"></param>
    /// <param name="lbFac"></param>
    /// <param name="type1"></param>
    /// <returns></returns>
    public static StockData CalculateUniChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 10, double ubFac = 0.02, double lbFac = 0.02, bool type1 = false)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            var prevUb = upperBandList.LastOrDefault();
            var ub = type1 ? currentSma + ubFac : currentSma + (currentSma * ubFac);
            upperBandList.AddRounded(ub);

            var prevLb = lowerBandList.LastOrDefault();
            var lb = type1 ? currentSma - lbFac : currentSma - (currentSma * lbFac);
            lowerBandList.AddRounded(lb);

            var signal = GetBollingerBandsSignal(currentValue - currentSma, prevValue - prevSma, currentValue, prevValue, ub, prevUb, lb, prevLb);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.UniChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Wilson Relative Price Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="overbought"></param>
    /// <param name="oversold"></param>
    /// <param name="upperNeutralZone"></param>
    /// <param name="lowerNeutralZone"></param>
    /// <returns></returns>
    public static StockData CalculateWilsonRelativePriceChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 34, int smoothLength = 1, double overbought = 70, double oversold = 30, double upperNeutralZone = 55,
        double lowerNeutralZone = 45)
    {
        List<double> rsiOverboughtList = new();
        List<double> rsiOversoldList = new();
        List<double> rsiUpperNeutralZoneList = new();
        List<double> rsiLowerNeutralZoneList = new();
        List<double> s1List = new();
        List<double> s2List = new();
        List<double> u1List = new();
        List<double> u2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length, smoothLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];

            var rsiOverbought = rsi - overbought;
            rsiOverboughtList.AddRounded(rsiOverbought);

            var rsiOversold = rsi - oversold;
            rsiOversoldList.AddRounded(rsiOversold);

            var rsiUpperNeutralZone = rsi - upperNeutralZone;
            rsiUpperNeutralZoneList.AddRounded(rsiUpperNeutralZone);

            var rsiLowerNeutralZone = rsi - lowerNeutralZone;
            rsiLowerNeutralZoneList.AddRounded(rsiLowerNeutralZone);
        }

        var obList = GetMovingAverageList(stockData, maType, smoothLength, rsiOverboughtList);
        var osList = GetMovingAverageList(stockData, maType, smoothLength, rsiOversoldList);
        var nzuList = GetMovingAverageList(stockData, maType, smoothLength, rsiUpperNeutralZoneList);
        var nzlList = GetMovingAverageList(stockData, maType, smoothLength, rsiLowerNeutralZoneList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ob = obList[i];
            var os = osList[i];
            var nzu = nzuList[i];
            var nzl = nzlList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevS1 = s1List.LastOrDefault();
            var s1 = currentValue - (currentValue * os / 100);
            s1List.AddRounded(s1);

            var prevU1 = u1List.LastOrDefault();
            var u1 = currentValue - (currentValue * ob / 100);
            u1List.AddRounded(u1);

            var prevU2 = u2List.LastOrDefault();
            var u2 = currentValue - (currentValue * nzu / 100);
            u2List.AddRounded(u2);

            var prevS2 = s2List.LastOrDefault();
            var s2 = currentValue - (currentValue * nzl / 100);
            s2List.AddRounded(s2);

            var signal = GetBullishBearishSignal(currentValue - Math.Min(u1, u2), prevValue - Math.Min(prevU1, prevU2),
                currentValue - Math.Max(s1, s2), prevValue - Math.Max(prevS1, prevS2));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "S1", s1List },
            { "S2", s2List },
            { "U1", u1List },
            { "U2", u2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.WilsonRelativePriceChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Linear Channels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateLinearChannels(this StockData stockData, int length = 14, double mult = 50)
    {
        List<double> aList = new();
        List<double> upperList = new();
        List<double> lowerList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var s = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var x = currentValue + ((prevA - prevA2) * mult);

            var a = x > prevA + s ? prevA + s : x < prevA - s ? prevA - s : prevA;
            aList.AddRounded(a);

            var up = a + (Math.Abs(a - prevA) * mult);
            var dn = a - (Math.Abs(a - prevA) * mult);

            var prevUpper = upperList.LastOrDefault();
            var upper = up == a ? prevUpper : up;
            upperList.AddRounded(upper);

            var prevLower = lowerList.LastOrDefault();
            var lower = dn == a ? prevLower : dn;
            lowerList.AddRounded(lower);

            var signal = GetBollingerBandsSignal(currentValue - a, prevValue - prevA, currentValue, prevValue, upper, prevUpper, lower, prevLower);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperList },
            { "LowerBand", lowerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.LinearChannels;

        return stockData;
    }

    /// <summary>
    /// Calculates the Interquartile Range Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateInterquartileRangeBands(this StockData stockData, int length = 14, double mult = 1.5)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<double> middleBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var trimeanList = CalculateTrimean(stockData, length);
        var q1List = trimeanList.OutputValues["Q1"];
        var q3List = trimeanList.OutputValues["Q3"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var q1 = q1List[i];
            var q3 = q3List[i];
            var iqr = q3 - q1;

            var upperBand = q3 + (mult * iqr);
            upperBandList.AddRounded(upperBand);

            var lowerBand = q1 - (mult * iqr);
            lowerBandList.AddRounded(lowerBand);

            var prevMiddleBand = middleBandList.LastOrDefault();
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.AddRounded(middleBand);

            var signal = GetCompareSignal(currentValue - middleBand, prevValue - prevMiddleBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.InterquartileRangeBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Narrow Sideways Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateNarrowSidewaysChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, double stdDevMult = 3)
    {
        var narrowChannelList = CalculateBollingerBands(stockData, maType, length, stdDevMult);
        var upperBandList = narrowChannelList.OutputValues["UpperBand"];
        var middleBandList = narrowChannelList.OutputValues["MiddleBand"];
        var lowerBandList = narrowChannelList.OutputValues["LowerBand"];
        var signalsList = narrowChannelList.SignalsList;

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.NarrowSidewaysChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the G Channels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGChannels(this StockData stockData, int length = 100)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> midList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevA = aList.LastOrDefault();
            var prevB = bList.LastOrDefault();
            var factor = length != 0 ? (prevA - prevB) / length : 0;

            var a = Math.Max(currentValue, prevA) - factor;
            aList.AddRounded(a);

            var b = Math.Min(currentValue, prevB) + factor;
            bList.AddRounded(b);

            var prevMid = midList.LastOrDefault();
            var mid = (a + b) / 2;
            midList.AddRounded(mid);

            var signal = GetCompareSignal(currentValue - mid, prevValue - prevMid);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", aList },
            { "MiddleBand", midList },
            { "LowerBand", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.GChannels;

        return stockData;
    }

    /// <summary>
    /// Calculates the High Low Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHighLowMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        List<double> middleBandList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var upperBandList = GetMovingAverageList(stockData, maType, length, highestList);
        var lowerBandList = GetMovingAverageList(stockData, maType, length, lowestList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var upperBand = upperBandList[i];
            var lowerBand = lowerBandList[i];

            var prevMiddleBand = middleBandList.LastOrDefault();
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.AddRounded(middleBand);

            var signal = GetCompareSignal(currentValue - middleBand, prevValue - prevMiddleBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.HighLowMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the High Low Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="pctShift"></param>
    /// <returns></returns>
    public static StockData CalculateHighLowBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double pctShift = 1)
    {
        List<double> highBandList = new();
        List<double> lowBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var tmaList1 = GetMovingAverageList(stockData, maType, length, inputList);
        var tmaList2 = GetMovingAverageList(stockData, maType, length, tmaList1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var tma = tmaList2[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevTma = i >= 1 ? tmaList2[i - 1] : 0;

            var prevHighBand = highBandList.LastOrDefault();
            var highBand = tma + (tma * pctShift / 100);
            highBandList.AddRounded(highBand);

            var prevLowBand = lowBandList.LastOrDefault();
            var lowBand = tma - (tma * pctShift / 100);
            lowBandList.AddRounded(lowBand);

            var signal = GetBollingerBandsSignal(currentValue - tma, prevValue - prevTma, currentValue, prevValue, highBand, prevHighBand,
                lowBand, prevLowBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", highBandList },
            { "MiddleBand", tmaList2 },
            { "LowerBand", lowBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.HighLowBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Hurst Cycle Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="fastMult"></param>
    /// <param name="slowMult"></param>
    /// <returns></returns>
    public static StockData CalculateHurstCycleChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int fastLength = 10, int slowLength = 30, double fastMult = 1, double slowMult = 3)
    {
        List<double> sctList = new();
        List<double> scbList = new();
        List<double> mctList = new();
        List<double> mcbList = new();
        List<double> scmmList = new();
        List<double> mcmmList = new();
        List<double> omedList = new();
        List<double> oshortList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var scl = MinOrMax((int)Math.Ceiling((double)fastLength / 2));
        var mcl = MinOrMax((int)Math.Ceiling((double)slowLength / 2));
        var scl_2 = MinOrMax((int)Math.Ceiling((double)scl / 2));
        var mcl_2 = MinOrMax((int)Math.Ceiling((double)mcl / 2));

        var sclAtrList = CalculateAverageTrueRange(stockData, maType, scl).CustomValuesList;
        var mclAtrList = CalculateAverageTrueRange(stockData, maType, mcl).CustomValuesList;
        var sclRmaList = GetMovingAverageList(stockData, maType, scl, inputList);
        var mclRmaList = GetMovingAverageList(stockData, maType, mcl, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sclAtr = sclAtrList[i];
            var mclAtr = mclAtrList[i];
            var prevSclRma = i >= scl_2 ? sclRmaList[i - scl_2] : currentValue;
            var prevMclRma = i >= mcl_2 ? mclRmaList[i - mcl_2] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var scm_off = fastMult * sclAtr;
            var mcm_off = slowMult * mclAtr;

            var prevSct = sctList.LastOrDefault();
            var sct = prevSclRma + scm_off;
            sctList.AddRounded(sct);

            var prevScb = scbList.LastOrDefault();
            var scb = prevSclRma - scm_off;
            scbList.AddRounded(scb);

            var mct = prevMclRma + mcm_off;
            mctList.AddRounded(mct);

            var mcb = prevMclRma - mcm_off;
            mcbList.AddRounded(mcb);

            var scmm = (sct + scb) / 2;
            scmmList.AddRounded(scmm);

            var mcmm = (mct + mcb) / 2;
            mcmmList.AddRounded(mcmm);

            var omed = mct - mcb != 0 ? (scmm - mcb) / (mct - mcb) : 0;
            omedList.AddRounded(omed);

            var oshort = mct - mcb != 0 ? (currentValue - mcb) / (mct - mcb) : 0;
            oshortList.AddRounded(oshort);

            var signal = GetBullishBearishSignal(currentValue - sct, prevValue - prevSct, currentValue - scb, prevValue - prevScb);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FastUpperBand", sctList },
            { "SlowUpperBand", mctList },
            { "FastMiddleBand", scmmList },
            { "SlowMiddleBand", mcmmList },
            { "FastLowerBand", scbList },
            { "SlowLowerBand", mcbList },
            { "OMed", omedList },
            { "OShort", oshortList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.HurstCycleChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Hurst Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="innerMult"></param>
    /// <param name="outerMult"></param>
    /// <param name="extremeMult"></param>
    /// <returns></returns>
    public static StockData CalculateHurstBands(this StockData stockData, int length = 10, double innerMult = 1.6, double outerMult = 2.6,
        double extremeMult = 4.2)
    {
        List<double> cmaList = new();
        List<double> upperExtremeBandList = new();
        List<double> lowerExtremeBandList = new();
        List<double> upperOuterBandList = new();
        List<double> lowerOuterBandList = new();
        List<double> upperInnerBandList = new();
        List<double> lowerInnerBandList = new();
        List<double> dPriceList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var displacement = MinOrMax((int)Math.Ceiling((double)length / 2) + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevCma1 = i >= 1 ? cmaList[i - 1] : 0;
            var prevCma2 = i >= 2 ? cmaList[i - 2] : 0;

            var dPrice = i >= displacement ? inputList[i - displacement] : 0;
            dPriceList.AddRounded(dPrice);

            var cma = dPrice == 0 ? prevCma1 + (prevCma1 - prevCma2) : dPriceList.TakeLastExt(length).Average();
            cmaList.AddRounded(cma);

            var extremeBand = cma * extremeMult / 100;
            var outerBand = cma * outerMult / 100;
            var innerBand = cma * innerMult / 100;

            var upperExtremeBand = cma + extremeBand;
            upperExtremeBandList.AddRounded(upperExtremeBand);

            var lowerExtremeBand = cma - extremeBand;
            lowerExtremeBandList.AddRounded(lowerExtremeBand);

            var upperInnerBand = cma + innerBand;
            upperInnerBandList.AddRounded(upperInnerBand);

            var lowerInnerBand = cma - innerBand;
            lowerInnerBandList.AddRounded(lowerInnerBand);

            var upperOuterBand = cma + outerBand;
            upperOuterBandList.AddRounded(upperOuterBand);

            var lowerOuterBand = cma - outerBand;
            lowerOuterBandList.AddRounded(lowerOuterBand);

            var signal = GetCompareSignal(currentValue - cma, prevValue - prevCma1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperExtremeBand", upperExtremeBandList },
            { "UpperOuterBand", upperOuterBandList },
            { "UpperInnerBand", upperInnerBandList },
            { "MiddleBand", cmaList },
            { "LowerExtremeBand", lowerExtremeBandList },
            { "LowerOuterBand", lowerOuterBandList },
            { "LowerInnerBand", lowerInnerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.HurstBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Hirashima Sugita RS
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHirashimaSugitaRS(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 1000)
    {
        List<double> d1List = new();
        List<double> absD1List = new();
        List<double> d2List = new();
        List<double> basisList = new();
        List<double> upper1List = new();
        List<double> lower1List = new();
        List<double> upper2List = new();
        List<double> lower2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ema = emaList[i];

            var d1 = currentValue - ema;
            d1List.AddRounded(d1);

            var absD1 = Math.Abs(d1);
            absD1List.AddRounded(absD1);
        }

        var wmaList = GetMovingAverageList(stockData, maType, length, absD1List);
        stockData.CustomValuesList = d1List;
        var s1List = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var s1 = s1List[i];
            var currentValue = inputList[i];
            var x = ema + s1;

            var d2 = currentValue - x;
            d2List.AddRounded(d2);
        }

        stockData.CustomValuesList = d2List;
        var s2List = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var s1 = s1List[i];
            var s2 = s2List[i];
            var prevS2 = i >= 1 ? s2List[i - 1] : 0;
            var wma = wmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevBasis = basisList.LastOrDefault();
            var basis = ema + s1 + (s2 - prevS2);
            basisList.AddRounded(basis);

            var upper1 = basis + wma;
            upper1List.AddRounded(upper1);

            var lower1 = basis - wma;
            lower1List.AddRounded(lower1);

            var upper2 = upper1 + wma;
            upper2List.AddRounded(upper2);

            var lower2 = lower1 - wma;
            lower2List.AddRounded(lower2);

            var signal = GetCompareSignal(currentValue - basis, prevValue - prevBasis);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand1", upper1List },
            { "UpperBand2", upper2List },
            { "MiddleBand", basisList },
            { "LowerBand1", lower1List },
            { "LowerBand2", lower2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.HirashimaSugitaRS;

        return stockData;
    }

    /// <summary>
    /// Calculates the Flagging Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFlaggingBands(this StockData stockData, int length = 14)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> tavgList = new();
        List<double> tsList = new();
        List<double> tosList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var stdDev = stdDevList[i];
            var prevA1 = i >= 1 ? aList[i - 1] : currentValue;
            var prevB1 = i >= 1 ? bList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : currentValue;
            var prevB2 = i >= 2 ? bList[i - 2] : currentValue;
            var prevA3 = i >= 3 ? aList[i - 3] : currentValue;
            var prevB3 = i >= 3 ? bList[i - 3] : currentValue;
            var l = stdDev != 0 ? (double)1 / length * stdDev : 0;

            var a = currentValue > prevA1 ? prevA1 + (currentValue - prevA1) : prevA2 == prevA3 ? prevA2 - l : prevA2;
            aList.AddRounded(a);

            var b = currentValue < prevB1 ? prevB1 + (currentValue - prevB1) : prevB2 == prevB3 ? prevB2 + l : prevB2;
            bList.AddRounded(b);

            var prevTos = tosList.LastOrDefault();
            var tos = currentValue > prevA2 ? 1 : currentValue < prevB2 ? 0 : prevTos;
            tosList.AddRounded(tos);

            var prevTavg = tavgList.LastOrDefault();
            var avg = (a + b) / 2;
            var tavg = tos == 1 ? (a + avg) / 2 : (b + avg) / 2;
            tavgList.AddRounded(tavg);

            var ts = (tos * b) + ((1 - tos) * a);
            tsList.AddRounded(ts);

            var signal = GetCompareSignal(currentValue - tavg, prevValue - prevTavg);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", aList },
            { "MiddleBand", tavgList },
            { "LowerBand", bList },
            { "TrailingStop", tsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.FlaggingBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kirshenbaum Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="stdDevFactor"></param>
    /// <returns></returns>
    public static StockData CalculateKirshenbaumBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 30, int length2 = 20, double stdDevFactor = 1)
    {
        List<double> topList = new();
        List<double> bottomList = new();
        List<double> tempInputList = new();
        List<double> tempLinRegList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var linRegList = CalculateLinearRegression(stockData, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentValue = inputList[i];
            tempInputList.AddRounded(currentValue);

            var currentLinReg = linRegList[i];
            tempLinRegList.AddRounded(currentLinReg);

            var stdError = GoodnessOfFit.PopulationStandardError(tempLinRegList.TakeLastExt(length2).Select(x => (double)x),
                tempInputList.TakeLastExt(length2).Select(x => (double)x));
            stdError = IsValueNullOrInfinity(stdError) ? 0 : stdError;
            var ratio = (double)stdError * stdDevFactor;

            var prevTop = topList.LastOrDefault();
            var top = currentEma + ratio;
            topList.AddRounded(top);

            var prevBottom = bottomList.LastOrDefault();
            var bottom = currentEma - ratio;
            bottomList.AddRounded(bottom);

            var signal = GetBullishBearishSignal(currentValue - top, prevValue - prevTop, currentValue - bottom, prevValue - prevBottom);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", topList },
            { "MiddleBand", emaList },
            { "LowerBand", bottomList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.KirshenbaumBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kaufman Adaptive Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="stdDevFactor"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanAdaptiveBands(this StockData stockData, int length = 100, double stdDevFactor = 3)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<double> powMaList = new();
        List<double> middleBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var er = Pow(erList[i], stdDevFactor);
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMiddleBand = middleBandList.LastOrDefault();
            var middleBand = (currentValue * er) + ((1 - er) * prevMiddleBand);
            middleBandList.AddRounded(middleBand);

            var prevPowMa = powMaList.LastOrDefault();
            var powMa = (Pow(currentValue, 2) * er) + ((1 - er) * prevPowMa);
            powMaList.AddRounded(powMa);

            var kaufmanDev = powMa - Pow(middleBand, 2) >= 0 ? Sqrt(powMa - Pow(middleBand, 2)) : 0;
            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = middleBand + kaufmanDev;
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = middleBand - kaufmanDev;
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand,
                prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Keltner Channels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="multFactor"></param>
    /// <returns></returns>
    public static StockData CalculateKeltnerChannels(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 20, int length2 = 10, double multFactor = 2)
    {
        List<double> upperChannelList = new();
        List<double> lowerChannelList = new();
        List<double> midChannelList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma20Day = emaList[i];
            var currentAtr10Day = atrList[i];

            var upperChannel = currentEma20Day + (multFactor * currentAtr10Day);
            upperChannelList.AddRounded(upperChannel);

            var lowerChannel = currentEma20Day - (multFactor * currentAtr10Day);
            lowerChannelList.AddRounded(lowerChannel);

            var prevMidChannel = midChannelList.LastOrDefault();
            var midChannel = (upperChannel + lowerChannel) / 2;
            midChannelList.AddRounded(midChannel);

            var signal = GetCompareSignal(currentValue - midChannel, prevValue - prevMidChannel);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperChannelList },
            { "MiddleBand", midChannelList },
            { "LowerBand", lowerChannelList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.KeltnerChannels;

        return stockData;
    }

    /// <summary>
    /// Calculates the Vortex Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVortexBands(this StockData stockData, MovingAvgType maType = MovingAvgType.McNichollMovingAverage,
        int length = 20)
    {
        List<double> upperList = new();
        List<double> lowerList = new();
        List<double> diffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var basisList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var basis = basisList[i];
            var currentValue = inputList[i];

            var diff = currentValue - basis;
            diffList.AddRounded(diff);
        }

        var diffMaList = GetMovingAverageList(stockData, maType, length, diffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diffMa = diffMaList[i];
            var basis = basisList[i];
            var dev = 2 * diffMa;

            var upper = basis + dev;
            upperList.AddRounded(upper);

            var lower = basis - dev;
            lowerList.AddRounded(lower);

            var signal = GetConditionSignal(upper > lower && upper > basis, lower > upper && lower > basis);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperList },
            { "MiddleBand", basisList },
            { "LowerBand", lowerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.VortexBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Adaptive Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeAdaptiveBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> upList = new();
        List<double> dnList = new();
        List<double> middleBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var aList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var a = Math.Max(aList[i], 1);
            var b = a * -1;

            var prevUp = i >= 1 ? upList[i - 1] : currentValue;
            var up = a != 0 ? (prevUp + (currentValue * a)) / a : 0;
            upList.AddRounded(up);

            var prevDn = i >= 1 ? dnList[i - 1] : currentValue;
            var dn = b != 0 ? (prevDn + (currentValue * b)) / b : 0;
            dnList.AddRounded(dn);
        }

        var upSmaList = GetMovingAverageList(stockData, maType, length, upList);
        var dnSmaList = GetMovingAverageList(stockData, maType, length, dnList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperBand = upSmaList[i];
            var lowerBand = dnSmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMiddleBand = middleBandList.LastOrDefault();
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.AddRounded(middleBand);

            var signal = GetCompareSignal(currentValue - middleBand, prevValue - prevMiddleBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upSmaList },
            { "MiddleBand", middleBandList },
            { "LowerBand", dnSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.VolumeAdaptiveBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Variable Moving Average Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateVariableMovingAverageBands(this StockData stockData, MovingAvgType maType = MovingAvgType.VariableMovingAverage,
        int length = 6, double mult = 1.5)
    {
        List<double> ubandList = new();
        List<double> lbandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentAtr = atrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var vma = maList[i];
            var prevVma = i >= 1 ? maList[i - 1] : 0;
            var o = mult * currentAtr;

            var prevUband = ubandList.LastOrDefault();
            var uband = vma + o;
            ubandList.AddRounded(uband);

            var prevLband = lbandList.LastOrDefault();
            var lband = vma - o;
            lbandList.AddRounded(lband);

            var signal = GetBollingerBandsSignal(currentValue - vma, prevValue - prevVma, currentValue, prevValue, uband, prevUband, lband, prevLband);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", ubandList },
            { "MiddleBand", maList },
            { "LowerBand", lbandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.VariableMovingAverageBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Vervoort Volatility Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="devMult"></param>
    /// <param name="lowBandMult"></param>
    /// <returns></returns>
    public static StockData CalculateVervoortVolatilityBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 8, int length2 = 13, double devMult = 3.55, double lowBandMult = 0.9)
    {
        List<double> typicalList = new();
        List<double> deviationList = new();
        List<double> ubList = new();
        List<double> lbList = new();
        List<double> tempList = new();
        List<double> medianAvgSmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, lowList, _, _) = GetInputValuesList(stockData);

        var medianAvgList = GetMovingAverageList(stockData, maType, length1, inputList);
        var medianAvgEmaList = GetMovingAverageList(stockData, maType, length1, medianAvgList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var medianAvg = medianAvgList[i];
            tempList.AddRounded(medianAvg);

            var currentValue = inputList[i];
            var currentLow = lowList[i];
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var typical = currentValue >= prevValue ? currentValue - prevLow : prevValue - currentLow;
            typicalList.AddRounded(typical);

            var typicalSma = typicalList.TakeLastExt(length2).Average();
            var deviation = devMult * typicalSma;
            deviationList.AddRounded(deviation);

            var medianAvgSma = tempList.TakeLastExt(length1).Average();
            medianAvgSmaList.AddRounded(medianAvgSma);
        }

        var devHighList = GetMovingAverageList(stockData, maType, length1, deviationList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var devHigh = devHighList[i];
            var midline = medianAvgSmaList[i];
            var medianAvgEma = medianAvgEmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMidline = i >= 1 ? medianAvgSmaList[i - 1] : 0;
            var devLow = lowBandMult * devHigh;

            var prevUb = ubList.LastOrDefault();
            var ub = medianAvgEma + devHigh;
            ubList.AddRounded(ub);

            var prevLb = lbList.LastOrDefault();
            var lb = medianAvgEma - devLow;
            lbList.AddRounded(lb);

            var signal = GetBollingerBandsSignal(currentValue - midline, prevValue - prevMidline, currentValue, prevValue, ub, prevUb, lb, prevLb);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", ubList },
            { "MiddleBand", medianAvgSmaList },
            { "LowerBand", lbList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.VervoortVolatilityBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Price Headley Acceleration Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculatePriceHeadleyAccelerationBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 20, double factor = 0.001)
    {
        List<double> ubList = new();
        List<double> lbList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var middleBandList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var mult = currentHigh + currentLow != 0 ? 4 * factor * 1000 * (currentHigh - currentLow) / (currentHigh + currentLow) : 0;

            var outerUb = currentHigh * (1 + mult);
            ubList.AddRounded(outerUb);

            var outerLb = currentLow * (1 - mult);
            lbList.AddRounded(outerLb);
        }

        var suList = GetMovingAverageList(stockData, maType, length, ubList);
        var slList = GetMovingAverageList(stockData, maType, length, lbList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var middleBand = middleBandList[i];
            var prevMiddleBand = i >= 1 ? middleBandList[i - 1] : 0;
            var outerUbSma = suList[i];
            var prevOuterUbSma = i >= 1 ? suList[i - 1] : 0;
            var outerLbSma = slList[i];
            var prevOuterLbSma = i >= 1 ? slList[i - 1] : 0;

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                outerUbSma, prevOuterUbSma, outerLbSma, prevOuterLbSma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", suList },
            { "MiddleBand", middleBandList },
            { "LowerBand", slList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.PriceHeadleyAccelerationBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Pseudo Polynomial Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="morph"></param>
    /// <returns></returns>
    public static StockData CalculatePseudoPolynomialChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14, double morph = 0.9)
    {
        List<double> kList = new();
        List<double> yK1List = new();
        List<double> indexList = new();
        List<double> middleBandList = new();
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var y = inputList[i];
            var prevK = i >= length ? kList[i - length] : y;
            var prevK2 = i >= length * 2 ? kList[i - (length * 2)] : y;
            var prevIndex = i >= length ? indexList[i - length] : 0;
            var prevIndex2 = i >= length * 2 ? indexList[i - (length * 2)] : 0;
            var ky = (morph * prevK) + ((1 - morph) * y);
            var ky2 = (morph * prevK2) + ((1 - morph) * y);

            double index = i;
            indexList.AddRounded(i);

            var k = prevIndex2 - prevIndex != 0 ? ky + ((index - prevIndex) / (prevIndex2 - prevIndex) * (ky2 - ky)) : 0;
            kList.AddRounded(k);
        }

        var k1List = GetMovingAverageList(stockData, maType, length, kList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var k1 = k1List[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var yk1 = Math.Abs(currentValue - k1);
            yK1List.AddRounded(yk1);

            var er = i != 0 ? yK1List.Sum() / i : 0;
            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = k1 + er;
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = k1 - er;
            lowerBandList.AddRounded(lowerBand);

            var prevMiddleBand = middleBandList.LastOrDefault();
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.AddRounded(middleBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.PseudoPolynomialChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Projected Support and Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateProjectedSupportAndResistance(this StockData stockData, int length = 25)
    {
        List<double> support1List = new();
        List<double> resistance1List = new();
        List<double> support2List = new();
        List<double> resistance2List = new();
        List<double> middleList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var range = highestHigh - lowestLow;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var support1 = lowestLow - (0.25 * range);
            support1List.AddRounded(support1);

            var support2 = lowestLow - (0.5 * range);
            support2List.AddRounded(support2);

            var resistance1 = highestHigh + (0.25 * range);
            resistance1List.AddRounded(resistance1);

            var resistance2 = highestHigh + (0.5 * range);
            resistance2List.AddRounded(resistance2);

            var prevMiddle = middleList.LastOrDefault();
            var middle = (support1 + support2 + resistance1 + resistance2) / 4;
            middleList.AddRounded(middle);

            var signal = GetCompareSignal(currentValue - middle, prevValue - prevMiddle);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Support1", support1List },
            { "Support2", support2List },
            { "Resistance1", resistance1List },
            { "Resistance2", resistance2List },
            { "MiddleBand", middleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.ProjectedSupportAndResistance;

        return stockData;
    }

    /// <summary>
    /// Calculates the Prime Number Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePrimeNumberBands(this StockData stockData, int length = 5)
    {
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        stockData.CustomValuesList = highList;
        var pnoUpBandList = CalculatePrimeNumberOscillator(stockData, length).CustomValuesList;
        stockData.CustomValuesList = lowList;
        var pnoDnBandList = CalculatePrimeNumberOscillator(stockData, length).CustomValuesList;
        var (upperBandList, _) = GetMaxAndMinValuesList(pnoUpBandList, length);
        var (_, lowerBandList) = GetMaxAndMinValuesList(pnoDnBandList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevUpBand1 = i >= 1 ? upperBandList[i - 1] : 0;
            var prevUpBand2 = i >= 2 ? upperBandList[i - 1] : 0;
            var prevDnBand1 = i >= 1 ? lowerBandList[i - 1] : 0;
            var prevDnBand2 = i >= 2 ? lowerBandList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var signal = GetBullishBearishSignal(close - prevUpBand1, prevClose - prevUpBand2, close - prevDnBand1, prevClose - prevDnBand2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.PrimeNumberBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Periodic Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculatePeriodicChannel(this StockData stockData, int length1 = 500, int length2 = 2)
    {
        List<double> tempList = new();
        List<double> indexList = new();
        List<double> corrList = new();
        List<double> absIndexCumDiffList = new();
        List<double> sinList = new();
        List<double> inSinList = new();
        List<double> absSinCumDiffList = new();
        List<double> absInSinCumDiffList = new();
        List<double> absDiffList = new();
        List<double> kList = new();
        List<double> absKDiffList = new();
        List<double> osList = new();
        List<double> apList = new();
        List<double> bpList = new();
        List<double> cpList = new();
        List<double> alList = new();
        List<double> blList = new();
        List<double> clList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = tempList.LastOrDefault();
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            double index = i;
            indexList.AddRounded(index);

            var indexCum = i != 0 ? indexList.Sum() / i : 0;
            var indexCumDiff = i - indexCum;
            var absIndexCumDiff = Math.Abs(i - indexCum);
            absIndexCumDiffList.AddRounded(absIndexCumDiff);

            var absIndexCum = i != 0 ? absIndexCumDiffList.Sum() / i : 0;
            var z = absIndexCum != 0 ? indexCumDiff / absIndexCum : 0;

            var corr = GoodnessOfFit.R(indexList.TakeLastExt(length2).Select(x => (double)x), tempList.TakeLastExt(length2).Select(x => (double)x));
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.AddRounded((double)corr);

            double s = i * Math.Sign(corrList.Sum());
            var sin = Math.Sin(s / length1);
            sinList.AddRounded(sin);

            var inSin = Math.Sin(s / length1) * -1;
            inSinList.AddRounded(inSin);

            var sinCum = i != 0 ? sinList.Sum() / i : 0;
            var inSinCum = i != 0 ? inSinList.Sum() / i : 0;
            var sinCumDiff = sin - sinCum;
            var inSinCumDiff = inSin - inSinCum;

            var absSinCumDiff = Math.Abs(sin - sinCum);
            absSinCumDiffList.AddRounded(absSinCumDiff);

            var absSinCum = i != 0 ? absSinCumDiffList.Sum() / i : 0;
            var absInSinCumDiff = Math.Abs(inSin - inSinCum);
            absInSinCumDiffList.AddRounded(absInSinCumDiff);

            var absInSinCum = i != 0 ? absInSinCumDiffList.Sum() / i : 0;
            var zs = absSinCum != 0 ? sinCumDiff / absSinCum : 0;
            var inZs = absInSinCum != 0 ? inSinCumDiff / absInSinCum : 0;
            var cum = i != 0 ? tempList.Sum() / i : 0;

            var absDiff = Math.Abs(currentValue - cum);
            absDiffList.AddRounded(absDiff);

            var absDiffCum = i != 0 ? absDiffList.Sum() / i : 0;
            var prevK = kList.LastOrDefault();
            var k = cum + ((z + zs) * absDiffCum);
            kList.AddRounded(k);

            var inK = cum + ((z + inZs) * absDiffCum);
            var absKDiff = Math.Abs(currentValue - k);
            absKDiffList.AddRounded(absKDiff);

            var absInKDiff = Math.Abs(currentValue - inK);
            var os = i != 0 ? absKDiffList.Sum() / i : 0;
            osList.AddRounded(os);

            var ap = k + os;
            apList.AddRounded(ap);

            var bp = ap + os;
            bpList.AddRounded(bp);

            var cp = bp + os;
            cpList.AddRounded(cp);

            var al = k - os;
            alList.AddRounded(al);

            var bl = al - os;
            blList.AddRounded(bl);

            var cl = bl - os;
            clList.AddRounded(cl);

            var signal = GetCompareSignal(currentValue - k, prevValue - prevK);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "K", kList },
            { "Os", osList },
            { "Ap", apList },
            { "Bp", bpList },
            { "Cp", cpList },
            { "Al", alList },
            { "Bl", blList },
            { "Cl", clList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.PeriodicChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Price Line Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePriceLineChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 100)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> sizeAList = new();
        List<double> sizeBList = new();
        List<double> sizeCList = new();
        List<double> midList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var atr = atrList[i];
            var prevA1 = i >= 1 ? aList[i - 1] : currentValue;
            var prevB1 = i >= 1 ? bList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : 0;
            var prevB2 = i >= 2 ? bList[i - 2] : 0;
            var prevSizeA = i >= 1 ? sizeAList[i - 1] : atr / length;
            var prevSizeB = i >= 1 ? sizeBList[i - 1] : atr / length;
            var prevSizeC = i >= 1 ? sizeCList[i - 1] : atr / length;

            var sizeA = prevA1 - prevA2 > 0 ? atr : prevSizeA;
            sizeAList.AddRounded(sizeA);

            var sizeB = prevB1 - prevB2 < 0 ? atr : prevSizeB;
            sizeBList.AddRounded(sizeB);

            var sizeC = prevA1 - prevA2 > 0 || prevB1 - prevB2 < 0 ? atr : prevSizeC;
            sizeCList.AddRounded(sizeC);

            var a = Math.Max(currentValue, prevA1) - (sizeA / length);
            aList.AddRounded(a);

            var b = Math.Min(currentValue, prevB1) + (sizeB / length);
            bList.AddRounded(b);

            var prevMid = midList.LastOrDefault();
            var mid = (a + b) / 2;
            midList.AddRounded(mid);

            var signal = GetCompareSignal(currentValue - mid, prevValue - prevMid);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", aList },
            { "MiddleBand", midList },
            { "LowerBand", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.PriceLineChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Price Curve Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePriceCurveChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 100)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> sizeList = new();
        List<double> aChgList = new();
        List<double> bChgList = new();
        List<double> midList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var atr = atrList[i];
            var prevA1 = i >= 1 ? aList[i - 1] : currentValue;
            var prevB1 = i >= 1 ? bList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : 0;
            var prevB2 = i >= 2 ? bList[i - 2] : 0;
            var prevSize = i >= 1 ? sizeList[i - 1] : atr / length;

            var size = prevA1 - prevA2 > 0 || prevB1 - prevB2 < 0 ? atr : prevSize;
            sizeList.AddRounded(size);

            double aChg = prevA1 > prevA2 ? 1 : 0;
            aChgList.AddRounded(aChg);

            double bChg = prevB1 < prevB2 ? 1 : 0;
            bChgList.AddRounded(bChg);

            var maxIndexA = aChgList.LastIndexOf(1);
            var maxIndexB = bChgList.LastIndexOf(1);
            var barsSinceA = aChgList.Count - 1 - maxIndexA;
            var barsSinceB = bChgList.Count - 1 - maxIndexB;

            var a = Math.Max(currentValue, prevA1) - (size / Pow(length, 2) * (barsSinceA + 1));
            aList.AddRounded(a);

            var b = Math.Min(currentValue, prevB1) + (size / Pow(length, 2) * (barsSinceB + 1));
            bList.AddRounded(b);

            var prevMid = midList.LastOrDefault();
            var mid = (a + b) / 2;
            midList.AddRounded(mid);

            var signal = GetCompareSignal(currentValue - mid, prevValue - prevMid);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", aList },
            { "MiddleBand", midList },
            { "LowerBand", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.PriceCurveChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Projection Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateProjectionBands(this StockData stockData, int length = 14)
    {
        List<double> puList = new();
        List<double> plList = new();
        List<double> middleBandList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        stockData.CustomValuesList = lowList;
        var lowSlopeList = CalculateLinearRegression(stockData, length).OutputValues["Slope"];
        stockData.CustomValuesList = highList;
        var highSlopeList = CalculateLinearRegression(stockData, length).OutputValues["Slope"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevPu = i >= 1 ? puList[i - 1] : 0;
            var prevPl = i >= 1 ? plList[i - 1] : 0;

            double pu = currentHigh, pl = currentLow;
            for (var j = 1; j <= length; j++)
            {
                var highSlope = i >= j ? highSlopeList[i - j] : 0;
                var lowSlope = i >= j ? lowSlopeList[i - j] : 0;
                var pHigh = i >= j - 1 ? highList[i - (j - 1)] : 0;
                var pLow = i >= j - 1 ? lowList[i - (j - 1)] : 0;
                var vHigh = pHigh + (highSlope * j);
                var vLow = pLow + (lowSlope * j);
                pu = Math.Max(pu, vHigh);
                pl = Math.Min(pl, vLow);
            }
            puList.AddRounded(pu);
            plList.AddRounded(pl);

            var prevMiddleBand = middleBandList.LastOrDefault();
            var middleBand = (pu + pl) / 2;
            middleBandList.AddRounded(middleBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, pu, prevPu, pl, prevPl);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", puList },
            { "MiddleBand", middleBandList },
            { "LowerBand", plList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.ProjectionBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Trader Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <param name="bandStep"></param>
    /// <returns></returns>
    public static StockData CalculateTrendTraderBands(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 21, double mult = 3, double bandStep = 20)
    {
        List<double> retList = new();
        List<double> outerUpperBandList = new();
        List<double> outerLowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevHighest = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest = i >= 1 ? lowestList[i - 1] : 0;
            var prevAtr = i >= 1 ? atrList[i - 1] : 0;
            var atrMult = prevAtr * mult;
            var highLimit = prevHighest - atrMult;
            var lowLimit = prevLowest + atrMult;

            var ret = close > highLimit && close > lowLimit ? highLimit : close < lowLimit && close < highLimit ? lowLimit : retList.LastOrDefault();
            retList.AddRounded(ret);
        }

        var retEmaList = GetMovingAverageList(stockData, maType, length, retList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var retEma = retEmaList[i];
            var close = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevRetEma = i >= 1 ? retEmaList[i - 1] : 0;

            var prevOuterUpperBand = outerUpperBandList.LastOrDefault();
            var outerUpperBand = retEma + bandStep;
            outerUpperBandList.AddRounded(outerUpperBand);

            var prevOuterLowerBand = outerLowerBandList.LastOrDefault();
            var outerLowerBand = retEma - bandStep;
            outerLowerBandList.AddRounded(outerLowerBand);

            var signal = GetBollingerBandsSignal(close - retEma, prevClose - prevRetEma, close, prevClose, outerUpperBand, 
                prevOuterUpperBand, outerLowerBand, prevOuterLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", outerUpperBandList },
            { "MiddleBand", retEmaList },
            { "LowerBand", outerLowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.TrendTraderBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Time and Money Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateTimeAndMoneyChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 41, int length2 = 82)
    {
        List<double> yomList = new();
        List<double> yomSquaredList = new();
        List<double> varyomList = new();
        List<double> somList = new();
        List<double> chPlus1List = new();
        List<double> chMinus1List = new();
        List<double> chPlus2List = new();
        List<double> chMinus2List = new();
        List<double> chPlus3List = new();
        List<double> chMinus3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var halfLength = MinOrMax((int)Math.Ceiling((double)length1 / 2));

        var smaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevBasis = i >= halfLength ? smaList[i - halfLength] : 0;

            var yom = prevBasis != 0 ? 100 * (currentValue - prevBasis) / prevBasis : 0;
            yomList.AddRounded(yom);

            var yomSquared = Pow(yom, 2);
            yomSquaredList.AddRounded(yomSquared);
        }

        var avyomList = GetMovingAverageList(stockData, maType, length2, yomList);
        var yomSquaredSmaList = GetMovingAverageList(stockData, maType, length2, yomSquaredList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevVaryom = i >= halfLength ? varyomList[i - halfLength] : 0;
            var avyom = avyomList[i];
            var yomSquaredSma = yomSquaredSmaList[i];

            var varyom = yomSquaredSma - (avyom * avyom);
            varyomList.AddRounded(varyom);

            var som = prevVaryom >= 0 ? Sqrt(prevVaryom) : 0;
            somList.AddRounded(som);
        }

        var sigomList = GetMovingAverageList(stockData, maType, length1, somList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var som = somList[i];
            var prevSom = i >= 1 ? somList[i - 1] : 0;
            var sigom = sigomList[i];
            var prevSigom = i >= 1 ? sigomList[i - 1] : 0;
            var basis = smaList[i];

            var chPlus1 = basis * (1 + (0.01 * sigom));
            chPlus1List.AddRounded(chPlus1);

            var chMinus1 = basis * (1 - (0.01 * sigom));
            chMinus1List.AddRounded(chMinus1);

            var chPlus2 = basis * (1 + (0.02 * sigom));
            chPlus2List.AddRounded(chPlus2);

            var chMinus2 = basis * (1 - (0.02 * sigom));
            chMinus2List.AddRounded(chMinus2);

            var chPlus3 = basis * (1 + (0.03 * sigom));
            chPlus3List.AddRounded(chPlus3);

            var chMinus3 = basis * (1 - (0.03 * sigom));
            chMinus3List.AddRounded(chMinus3);

            var signal = GetCompareSignal(som - sigom, prevSom - prevSigom);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ch+1", chPlus1List },
            { "Ch-1", chMinus1List },
            { "Ch+2", chPlus2List },
            { "Ch-2", chMinus2List },
            { "Ch+3", chPlus3List },
            { "Ch-3", chMinus3List },
            { "Median", sigomList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.TrendTraderBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Tirone Levels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTironeLevels(this StockData stockData, int length = 20)
    {
        List<double> tlhList = new();
        List<double> clhList = new();
        List<double> blhList = new();
        List<double> amList = new();
        List<double> ehList = new();
        List<double> elList = new();
        List<double> rhList = new();
        List<double> rlList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hh = highestList[i];
            var ll = lowestList[i];

            var tlh = hh - ((hh - ll) / 3);
            tlhList.AddRounded(tlh);

            var clh = ll + ((hh - ll) / 2);
            clhList.AddRounded(clh);

            var blh = ll + ((hh - ll) / 3);
            blhList.AddRounded(blh);

            var prevAm = amList.LastOrDefault();
            var am = (hh + ll + currentValue) / 3;
            amList.AddRounded(am);

            var eh = am + (hh - ll);
            ehList.AddRounded(eh);

            var el = am - (hh - ll);
            elList.AddRounded(el);

            var rh = (2 * am) - ll;
            rhList.AddRounded(rh);

            var rl = (2 * am) - hh;
            rlList.AddRounded(rl);

            var signal = GetCompareSignal(currentValue - am, prevValue - prevAm);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tlh", tlhList },
            { "Clh", clhList },
            { "Blh", blhList },
            { "Am", amList },
            { "Eh", ehList },
            { "El", elList },
            { "Rh", rhList },
            { "Rl", rlList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.TironeLevels;

        return stockData;
    }

    /// <summary>
    /// Calculates the Time Series Forecast
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTimeSeriesForecast(this StockData stockData, int length = 500)
    {
        List<double> absDiffList = new();
        List<double> aList = new();
        List<double> bList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var tsList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ts = tsList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevTs = i >= 1 ? tsList[i - 1] : 0;

            var absDiff = Math.Abs(currentValue - ts);
            absDiffList.AddRounded(absDiff);

            var e = i != 0 ? absDiffList.Sum() / i : 0;
            var prevA = aList.LastOrDefault();
            var a = ts + e;
            aList.AddRounded(a);

            var prevB = bList.LastOrDefault();
            var b = ts - e;
            bList.AddRounded(b);

            var signal = GetBollingerBandsSignal(currentValue - ts, prevValue - prevTs, currentValue, prevValue, a, prevA, b, prevB);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", aList },
            { "MiddleBand", tsList },
            { "LowerBand", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tsList;
        stockData.IndicatorName = IndicatorName.TimeSeriesForecast;

        return stockData;
    }

    /// <summary>
    /// Calculates the Range Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevFactor"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRangeBands(this StockData stockData, double stdDevFactor = 1, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(smaList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;
            var rangeDev = highest - lowest;

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = middleBand + (rangeDev * stdDevFactor);
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = middleBand - (rangeDev * stdDevFactor);
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.RangeBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Range Identifier
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRangeIdentifier(this StockData stockData, int length = 34)
    {
        List<double> upList = new();
        List<double> downList = new();
        List<double> midList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevUp = upList.LastOrDefault();
            var prevDown = downList.LastOrDefault();

            var up = currentValue < prevUp && currentValue > prevDown ? prevUp : currentHigh;
            upList.AddRounded(up);

            var down = currentValue < prevUp && currentValue > prevDown ? prevDown : currentLow;
            downList.AddRounded(down);

            var prevMid = midList.LastOrDefault();
            var mid = (up + down) / 2;
            midList.AddRounded(mid);

            var signal = GetCompareSignal(currentValue - mid, prevValue - prevMid);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upList },
            { "MiddleBand", midList },
            { "LowerBand", downList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.RangeIdentifier;

        return stockData;
    }

    /// <summary>
    /// Calculates the Rate Of Change Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRateOfChangeBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 12, int smoothLength = 3)
    {
        List<double> rocSquaredList = new();
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();

        var rocList = CalculateRateOfChange(stockData, length).CustomValuesList;
        var middleBandList = GetMovingAverageList(stockData, maType, smoothLength, rocList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var roc = rocList[i];
            var middleBand = middleBandList[i];
            var prevMiddleBand1 = i >= 1 ? middleBandList[i - 1] : 0;
            var prevMiddleBand2 = i >= 2 ? middleBandList[i - 2] : 0;

            var rocSquared = Pow(roc, 2);
            rocSquaredList.AddRounded(rocSquared);

            var squaredAvg = rocSquaredList.TakeLastExt(length).Average();
            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = Sqrt(squaredAvg);
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = -upperBand;
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(middleBand - prevMiddleBand1, prevMiddleBand1 - prevMiddleBand2, middleBand, prevMiddleBand1, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.RateOfChangeBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Root Moving Average Squared Error Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevFactor"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRootMovingAverageSquaredErrorBands(this StockData stockData, double stdDevFactor = 1, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<double> powList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var currentValue = inputList[i];

            var pow = Pow(currentValue - sma, 2);
            powList.AddRounded(pow);
        }

        var powSmaList = GetMovingAverageList(stockData, maType, length, powList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var powSma = powSmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;
            var rmaseDev = Sqrt(powSma);

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = middleBand + (rmaseDev * stdDevFactor);
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = middleBand - (rmaseDev * stdDevFactor);
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.RootMovingAverageSquaredErrorBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Moving Average Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 50, double mult = 1)
    {
        List<double> sqList = new();
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastMaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowMaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastMa = fastMaList[i];
            var slowMa = slowMaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFastMa = i >= 1 ? fastMaList[i - 1] : 0;

            var sq = Pow(slowMa - fastMa, 2);
            sqList.AddRounded(sq);

            var dev = Sqrt(sqList.TakeLastExt(fastLength).Average()) * mult;
            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = slowMa + dev;
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = slowMa - dev;
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - fastMa, prevValue - prevFastMa, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", fastMaList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.MovingAverageBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Moving Average Support Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageSupportResistance(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 10, double factor = 2)
    {
        List<double> topList = new();
        List<double> bottomList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var supportLevel = 1 + (factor / 100);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            var top = currentSma * supportLevel;
            topList.AddRounded(top);

            var bottom = supportLevel != 0 ? currentSma / supportLevel : 0;
            bottomList.AddRounded(bottom);

            var signal = GetCompareSignal(currentValue - currentSma, prevValue - prevSma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", topList },
            { "MiddleBand", smaList },
            { "LowerBand", bottomList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.MovingAverageSupportResistance;

        return stockData;
    }

    /// <summary>
    /// Calculates the Motion To Attraction Channels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMotionToAttractionChannels(this StockData stockData, int length = 14)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> cList = new();
        List<double> dList = new();
        List<double> aMaList = new();
        List<double> bMaList = new();
        List<double> avgMaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevAMa = i >= 1 ? aMaList[i - 1] : currentValue;
            var prevBMa = i >= 1 ? bMaList[i - 1] : currentValue;

            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var a = currentValue > prevAMa ? currentValue : prevA;
            aList.AddRounded(a);

            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var b = currentValue < prevBMa ? currentValue : prevB;
            bList.AddRounded(b);

            var prevC = cList.LastOrDefault();
            var c = b - prevB != 0 ? prevC + alpha : a - prevA != 0 ? 0 : prevC;
            cList.AddRounded(c);

            var prevD = dList.LastOrDefault();
            var d = a - prevA != 0 ? prevD + alpha : b - prevB != 0 ? 0 : prevD;
            dList.AddRounded(d);

            var avg = (a + b) / 2;
            var aMa = (c * avg) + ((1 - c) * a);
            aMaList.AddRounded(aMa);

            var bMa = (d * avg) + ((1 - d) * b);
            bMaList.AddRounded(bMa);

            var prevAvgMa = avgMaList.LastOrDefault();
            var avgMa = (aMa + bMa) / 2;
            avgMaList.AddRounded(avgMa);

            var signal = GetCompareSignal(currentValue - avgMa, prevValue - prevAvgMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", aMaList },
            { "MiddleBand", avgMaList },
            { "LowerBand", bMaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.MotionToAttractionChannels;

        return stockData;
    }

    /// <summary>
    /// Calculates the Mean Absolute Error Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevFactor"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMeanAbsoluteErrorBands(this StockData stockData, double stdDevFactor = 1, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<double> devList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;

            var dev = Math.Abs(currentValue - middleBand);
            devList.AddRounded(dev);

            var maeDev = i != 0 ? devList.Sum() / i : 0;
            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = middleBand + (maeDev * stdDevFactor);
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = middleBand - (maeDev * stdDevFactor);
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.MeanAbsoluteErrorBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Mean Absolute Deviation Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevFactor"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMeanAbsoluteDeviationBands(this StockData stockData, double stdDevFactor = 2, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var devList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var currentStdDeviation = devList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = middleBand + (currentStdDeviation * stdDevFactor);
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = middleBand - (currentStdDeviation * stdDevFactor);
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.MeanAbsoluteErrorBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Moving Average Displaced Envelope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="pct"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageDisplacedEnvelope(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 9, int length2 = 13, double pct = 0.5)
    {
        List<double> upperEnvelopeList = new();
        List<double> lowerEnvelopeList = new();
        List<double> middleEnvelopeList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= length2 ? emaList[i - length2] : 0;

            var prevUpperEnvelope = upperEnvelopeList.LastOrDefault();
            var upperEnvelope = prevEma * ((100 + pct) / 100);
            upperEnvelopeList.AddRounded(upperEnvelope);

            var prevLowerEnvelope = lowerEnvelopeList.LastOrDefault();
            var lowerEnvelope = prevEma * ((100 - pct) / 100);
            lowerEnvelopeList.AddRounded(lowerEnvelope);

            var prevMiddleEnvelope = middleEnvelopeList.LastOrDefault();
            var middleEnvelope = (upperEnvelope + lowerEnvelope) / 2;
            middleEnvelopeList.AddRounded(middleEnvelope);

            var signal = GetBollingerBandsSignal(currentValue - middleEnvelope, prevValue - prevMiddleEnvelope, currentValue, prevValue,
                upperEnvelope, prevUpperEnvelope, lowerEnvelope, prevLowerEnvelope);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperEnvelopeList },
            { "MiddleBand", middleEnvelopeList },
            { "LowerBand", lowerEnvelopeList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.MovingAverageDisplacedEnvelope;

        return stockData;
    }

    /// <summary>
    /// Calculates the Dema 2 Lines
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateDema2Lines(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 40)
    {
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, slowLength, inputList);
        var dema1List = GetMovingAverageList(stockData, maType, fastLength, ema1List);
        var dema2List = GetMovingAverageList(stockData, maType, slowLength, ema2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var dema1 = dema1List[i];
            var dema2 = dema2List[i];
            var prevDema1 = i >= 1 ? dema1List[i - 1] : 0;
            var prevDema2 = i >= 1 ? dema2List[i - 1] : 0;

            var signal = GetCompareSignal(dema1 - dema2, prevDema1 - prevDema2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dema1", dema1List },
            { "Dema2", dema2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.Dema2Lines;

        return stockData;
    }

    /// <summary>
    /// Calculates the Dynamic Support and Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDynamicSupportAndResistance(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 25)
    {
        List<double> supportList = new();
        List<double> resistanceList = new();
        List<double> middleList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var mult = Sqrt(length);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentAvgTrueRange = atrList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var support = highestHigh - (currentAvgTrueRange * mult);
            supportList.AddRounded(support);

            var resistance = lowestLow + (currentAvgTrueRange * mult);
            resistanceList.AddRounded(resistance);

            var prevMiddle = middleList.LastOrDefault();
            var middle = (support + resistance) / 2;
            middleList.AddRounded(middle);

            var signal = GetCompareSignal(currentValue - middle, prevValue - prevMiddle);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Support", supportList },
            { "Resistance", resistanceList },
            { "MiddleBand", middleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.DynamicSupportAndResistance;

        return stockData;
    }

    /// <summary>
    /// Calculates the Daily Average Price Delta
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDailyAveragePriceDelta(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 21)
    {
        List<double> topList = new();
        List<double> bottomList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var smaHighList = GetMovingAverageList(stockData, maType, length, highList);
        var smaLowList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            var low = lowList[i];
            var highSma = smaHighList[i];
            var lowSma = smaLowList[i];
            var dapd = highSma - lowSma;

            var prevTop = topList.LastOrDefault();
            var top = high + dapd;
            topList.AddRounded(top);

            var prevBottom = bottomList.LastOrDefault();
            var bottom = low - dapd;
            bottomList.AddRounded(bottom);

            var signal = GetConditionSignal(high > prevTop, low < prevBottom);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", topList },
            { "LowerBand", bottomList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.DailyAveragePriceDelta;

        return stockData;
    }

    /// <summary>
    /// Calculates the D Envelope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="devFactor"></param>
    /// <returns></returns>
    public static StockData CalculateDEnvelope(this StockData stockData, int length = 20, double devFactor = 2)
    {
        List<double> mtList = new();
        List<double> utList = new();
        List<double> dtList = new();
        List<double> mt2List = new();
        List<double> ut2List = new();
        List<double> butList = new();
        List<double> bltList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alp = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMt = mtList.LastOrDefault();
            var mt = (alp * currentValue) + ((1 - alp) * prevMt);
            mtList.AddRounded(mt);

            var prevUt = utList.LastOrDefault();
            var ut = (alp * mt) + ((1 - alp) * prevUt);
            utList.AddRounded(ut);

            var prevDt = dtList.LastOrDefault();
            var dt = (2 - alp) * (mt - ut) / (1 - alp);
            dtList.AddRounded(dt);

            var prevMt2 = mt2List.LastOrDefault();
            var mt2 = (alp * Math.Abs(currentValue - dt)) + ((1 - alp) * prevMt2);
            mt2List.AddRounded(mt2);

            var prevUt2 = ut2List.LastOrDefault();
            var ut2 = (alp * mt2) + ((1 - alp) * prevUt2);
            ut2List.AddRounded(ut2);

            var dt2 = (2 - alp) * (mt2 - ut2) / (1 - alp);
            var prevBut = butList.LastOrDefault();
            var but = dt + (devFactor * dt2);
            butList.AddRounded(but);

            var prevBlt = bltList.LastOrDefault();
            var blt = dt - (devFactor * dt2);
            bltList.AddRounded(blt);

            var signal = GetBollingerBandsSignal(currentValue - dt, prevValue - prevDt, currentValue, prevValue, but, prevBut, blt, prevBlt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", butList },
            { "MiddleBand", dtList },
            { "LowerBand", bltList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.DEnvelope;

        return stockData;
    }

    /// <summary>
    /// Calculates the Smart Envelope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateSmartEnvelope(this StockData stockData, int length = 14, double factor = 1)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> aSignalList = new();
        List<double> bSignalList = new();
        List<double> avgList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var prevASignal = aSignalList.LastOrDefault();
            var prevBSignal = bSignalList.LastOrDefault();
            var diff = Math.Abs(MinPastValues(i, 1, currentValue - prevValue));

            var a = Math.Max(currentValue, prevA) - (Math.Min(Math.Abs(currentValue - prevA), diff) / length * prevASignal);
            aList.AddRounded(a);

            var b = Math.Min(currentValue, prevB) + (Math.Min(Math.Abs(currentValue - prevB), diff) / length * prevBSignal);
            bList.AddRounded(b);

            var aSignal = b < prevB ? -factor : factor;
            aSignalList.AddRounded(aSignal);

            var bSignal = a > prevA ? -factor : factor;
            bSignalList.AddRounded(bSignal);

            var prevAvg = avgList.LastOrDefault();
            var avg = (a + b) / 2;
            avgList.AddRounded(avg);

            var signal = GetCompareSignal(currentValue - avg, prevValue - prevAvg);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", aList },
            { "MiddleBand", avgList },
            { "LowerBand", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.SmartEnvelope;

        return stockData;
    }

    /// <summary>
    /// Calculates the Support Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSupportResistance(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> resList = new();
        List<double> suppList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = i >= 1 ? smaList[i - 1] : 0;
            var crossAbove = prevValue < sma && currentValue >= sma;
            var crossBelow = prevValue > sma && currentValue <= sma;

            var prevRes = resList.LastOrDefault();
            var res = crossBelow ? highest : i >= 1 ? prevRes : highest;
            resList.AddRounded(res);

            var prevSupp = suppList.LastOrDefault();
            var supp = crossAbove ? lowest : i >= 1 ? prevSupp : lowest;
            suppList.AddRounded(supp);

            var signal = GetBullishBearishSignal(currentValue - res, prevValue - prevRes, currentValue - supp, prevValue - prevSupp);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Support", suppList },
            { "Resistance", resList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.SupportResistance;

        return stockData;
    }

    /// <summary>
    /// Calculates the Stationary Extrapolated Levels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateStationaryExtrapolatedLevels(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200)
    {
        List<double> extList = new();
        List<double> yList = new();
        List<double> xList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var priorY = i >= length ? yList[i - length] : 0;
            var priorY2 = i >= length * 2 ? yList[i - (length * 2)] : 0;
            var priorX = i >= length ? xList[i - length] : 0;
            var priorX2 = i >= length * 2 ? xList[i - (length * 2)] : 0;

            double x = i;
            xList.AddRounded(i);

            var y = currentValue - sma;
            yList.AddRounded(y);

            var ext = priorX2 - priorX != 0 && priorY2 - priorY != 0 ? (priorY + ((x - priorX) / (priorX2 - priorX) * (priorY2 - priorY))) / 2 : 0;
            extList.AddRounded(ext);
        }

        var (highestList1, lowestList1) = GetMaxAndMinValuesList(extList, length);
        var (upperBandList, lowerBandList) = GetMaxAndMinValuesList(highestList1, lowestList1, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var y = yList[i];
            var ext = extList[i];
            var prevY = i >= 1 ? yList[i - 1] : 0;
            var prevExt = i >= 1 ? extList[i - 1] : 0;

            var signal = GetCompareSignal(y - ext, prevY - prevExt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", yList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.StationaryExtrapolatedLevels;

        return stockData;
    }

    /// <summary>
    /// Calculates the Scalper's Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateScalpersChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 15, 
        int length2 = 20)
    {
        List<double> scalperList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        var smaList = GetMovingAverageList(stockData, maType, length2, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentSma = smaList[i];
            var currentAtr = atrList[i];

            var prevScalper = scalperList.LastOrDefault();
            var scalper = Math.PI * currentAtr > 0 ? currentSma - Math.Log(Math.PI * currentAtr) : currentSma;
            scalperList.AddRounded(scalper);

            var signal = GetCompareSignal(currentValue - scalper, prevValue - prevScalper);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", highestList },
            { "MiddleBand", scalperList },
            { "LowerBand", lowestList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.ScalpersChannel;

        return stockData;
    }

    /// <summary>
    /// Calculates the Smoothed Volatility Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="deviation"></param>
    /// <param name="bandAdjust"></param>
    /// <returns></returns>
    public static StockData CalculateSmoothedVolatilityBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 20, int length2 = 21, double deviation = 2.4, double bandAdjust = 0.9)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrPeriod = (length1 * 2) - 1;

        var atrList = CalculateAverageTrueRange(stockData, maType, atrPeriod).CustomValuesList;
        var maList = GetMovingAverageList(stockData, maType, length1, inputList);
        var middleBandList = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var atr = atrList[i];
            var middleBand = middleBandList[i];
            var ma = maList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? middleBandList[i - 1] : 0;
            var atrBuf = atr * deviation;

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = currentValue != 0 ? ma + (ma * atrBuf / currentValue) : ma;
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = currentValue != 0 ? ma - (ma * atrBuf * bandAdjust / currentValue) : ma;
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue,
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.SmoothedVolatilityBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Extended Recursive Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateExtendedRecursiveBands(this StockData stockData, int length = 100)
    {
        List<double> aClassicList = new();
        List<double> bClassicList = new();
        List<double> cClassicList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var sc = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevAClassic = i >= 1 ? aClassicList[i - 1] : currentValue;
            var prevBClassic = i >= 1 ? bClassicList[i - 1] : currentValue;

            var aClassic = Math.Max(prevAClassic, currentValue) - (sc * Math.Abs(currentValue - prevAClassic));
            aClassicList.AddRounded(aClassic);

            var bClassic = Math.Min(prevBClassic, currentValue) + (sc * Math.Abs(currentValue - prevBClassic));
            bClassicList.AddRounded(bClassic);

            var prevCClassic = cClassicList.LastOrDefault();
            var cClassic = (aClassic + bClassic) / 2;
            cClassicList.AddRounded(cClassic);

            var signal = GetCompareSignal(currentValue - cClassic, prevValue - prevCClassic);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", aClassicList },
            { "MiddleBand", cClassicList },
            { "LowerBand", bClassicList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.ExtendedRecursiveBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Efficient Trend Step Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateEfficientTrendStepChannel(this StockData stockData, int length = 100, int fastLength = 50, int slowLength = 200)
    {
        List<double> val2List = new();
        List<double> upperList = new();
        List<double> lowerList = new();
        List<double> aList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var val2 = currentValue * 2;
            val2List.AddRounded(val2);
        }

        stockData.CustomValuesList = val2List;
        var stdDevFastList = CalculateStandardDeviationVolatility(stockData, length: fastLength).CustomValuesList;
        stockData.CustomValuesList = val2List;
        var stdDevSlowList = CalculateStandardDeviationVolatility(stockData, length: slowLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var er = erList[i];
            var fastStdDev = stdDevFastList[i];
            var slowStdDev = stdDevSlowList[i];
            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var dev = (er * fastStdDev) + ((1 - er) * slowStdDev);

            var a = currentValue > prevA + dev ? currentValue : currentValue < prevA - dev ? currentValue : prevA;
            aList.AddRounded(a);

            var prevUpper = upperList.LastOrDefault();
            var upper = a + dev;
            upperList.AddRounded(upper);

            var prevLower = lowerList.LastOrDefault();
            var lower = a - dev;
            lowerList.AddRounded(lower);

            var signal = GetBollingerBandsSignal(currentValue - a, prevValue - prevA, currentValue, prevValue, upper, prevUpper, lower, prevLower);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperList },
            { "MiddleBand", aList },
            { "LowerBand", lowerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EfficientTrendStepChannel;

        return stockData;
    }
}
