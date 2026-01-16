
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Martin Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateMartinRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 30, double bmk = 0.02)
    {
        List<double> martinList = new(stockData.Count);
        List<double> benchList = new(stockData.Count);
        List<double> retList = new(stockData.Count);

        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var bench = Pow(1 + bmk, length / barsPerYr) - 1;
            benchList.Add(bench);

            var ret = prevValue != 0 ? (100 * (currentValue / prevValue)) - 1 - (bench * 100) : 0;
            retList.Add(ret);
        }

        var retSmaList = GetMovingAverageList(stockData, maType, length, retList);
        stockData.SetCustomValues(retList);
        var ulcerIndexList = CalculateUlcerIndex(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ulcerIndex = ulcerIndexList[i];
            var retSma = retSmaList[i];

            var prevMartin = GetLastOrDefault(martinList);
            var martin = ulcerIndex != 0 ? retSma / ulcerIndex : 0;
            martinList.Add(martin);

            var signal = GetCompareSignal(martin - 2, prevMartin - 2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mr", martinList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(martinList);
        stockData.IndicatorName = IndicatorName.MartinRatio;

        return stockData;
    }


    /// <summary>
    /// Calculates the Omega Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateOmegaRatio(this StockData stockData, int length = 30, double bmk = 0.05)
    {
        List<double> omegaList = new(stockData.Count);
        List<double> retList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            retList.Add(ret);

            double downSide = 0, upSide = 0;
            for (var j = 0; j < length; j++)
            {
                var iValue = i >= j ? retList[i - j] : 0;
                downSide += iValue < bench ? bench - iValue : 0;
                upSide += iValue > bench ? iValue - bench : 0;
            }

            var prevOmega = GetLastOrDefault(omegaList);
            var omega = downSide != 0 ? upSide / downSide : 0;
            omegaList.Add(omega);

            var signal = GetCompareSignal(omega - 5, prevOmega - 5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Or", omegaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(omegaList);
        stockData.IndicatorName = IndicatorName.OmegaRatio;

        return stockData;
    }

}

