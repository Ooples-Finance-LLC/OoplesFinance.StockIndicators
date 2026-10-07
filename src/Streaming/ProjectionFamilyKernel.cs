using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

internal sealed class ProjectionFamilyKernel : IDisposable
{
    private readonly IndicatorName _family;
    private readonly ProjectionBandsCalculator _bands;
    private readonly Average? _signal, _price;
    private BigInteger _previousSlope, _previousPrice, _previousUpper, _previousLower, _previousMean;
    internal ProjectionFamilyKernel(IndicatorName family, int length, MovingAvgType kind, int smooth)
    {
        _family = family; _bands = new(length);
        if (family != IndicatorName.ProjectionBands)
        { _signal = new(kind, family == IndicatorName.ProjectionBandwidth ? length : smooth); _price = new(kind, length); }
    }
    internal (ProjectionBandsSnapshot Bands, BigInteger Line, BigInteger Mean, Signal Trade) Next(double price, double high, double low, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var value = ExactVarianceWindow.Units(price); var bands = _bands.Update(high, low, final);
        BigInteger line, mean, slope; Signal trade;
        if (_family == IndicatorName.ProjectionBands)
        {
            line = bands.MiddleUnits; mean = BigInteger.Zero; slope = value - line;
            trade = slope.Sign > 0 && slope > _previousSlope ? Signal.StrongBuy
                : slope.Sign < 0 && slope < _previousSlope ? Signal.StrongSell
                : slope.Sign > 0 || _previousPrice < _previousLower && value > bands.LowerUnits ? Signal.Buy
                : slope.Sign < 0 || _previousPrice > _previousUpper && value < bands.UpperUnits ? Signal.Sell : Signal.None;
        }
        else
        {
            line = _family == IndicatorName.ProjectionOscillator ? bands.Oscillator(price) : bands.Bandwidth;
            mean = _signal!.Next(line, final); slope = value - _price!.Next(value, final);
            var active = _family == IndicatorName.ProjectionOscillator ? mean >= _previousMean : line >= mean;
            trade = !active ? Signal.None : slope.Sign > 0 ? slope > _previousSlope ? Signal.StrongBuy : Signal.Buy
                : slope.Sign < 0 ? slope < _previousSlope ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        if (final)
        {
            _previousSlope = slope; _previousPrice = value; _previousUpper = bands.UpperUnits;
            _previousLower = bands.LowerUnits; _previousMean = mean;
        }
        return (bands,line,mean,trade);
    }
    internal StreamingIndicatorStateResult Update(OhlcvBar bar, bool final, bool outputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = Next(bar.Close,bar.High,bar.Low,final);
        var line = ProjectionBandsSnapshot.Publish(point.Line);
        return new(line, !outputs ? null : _family == IndicatorName.ProjectionBands
            ? new Dictionary<string,double> { ["UpperBand"] = point.Bands.Upper, ["MiddleBand"] = point.Bands.Middle, ["LowerBand"] = point.Bands.Lower }
            : new Dictionary<string,double> { [_family == IndicatorName.ProjectionOscillator ? "Pbo" : "Pbw"] = line, ["Signal"] = ProjectionBandsSnapshot.Publish(point.Mean) });
    }
    internal static (Dictionary<string,List<double>> Outputs, List<Signal> Signals) Calculate(StockData data, IndicatorName family, int length, MovingAvgType kind, int smooth)
    {
        var (input, high, low, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var values in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        var keys = family == IndicatorName.ProjectionBands ? new[] { "UpperBand", "MiddleBand", "LowerBand" }
            : new[] { family == IndicatorName.ProjectionOscillator ? "Pbo" : "Pbw", "Signal" };
        var result = keys.ToDictionary(k=>k,_=>new List<double>(input.Count)); var signals = new List<Signal>(input.Count);
        using var kernel = new ProjectionFamilyKernel(family,length,kind,smooth);
        for (var i=0;i<input.Count;i++)
        {
            var point = kernel.Next(input[i],high[i],low[i],true);
            if (family == IndicatorName.ProjectionBands)
            { result["UpperBand"].Add(point.Bands.Upper); result["MiddleBand"].Add(point.Bands.Middle); result["LowerBand"].Add(point.Bands.Lower); }
            else { result[keys[0]].Add(ProjectionBandsSnapshot.Publish(point.Line)); result["Signal"].Add(ProjectionBandsSnapshot.Publish(point.Mean)); }
            signals.Add(point.Trade);
        }
        return (result,signals);
    }
    internal void Reset()
    { _bands.Reset(); _signal?.Reset(); _price?.Reset(); _previousSlope = _previousPrice = _previousUpper = _previousLower = _previousMean = default; }
    public void Dispose() { _bands.Dispose(); _signal?.Dispose(); _price?.Dispose(); }
    private sealed class Average : IDisposable
    {
        private readonly RocBankAverage? _wide;
        private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        {
            if (StrengthWindow.Supports(kind)) _wide = new(kind, length, 1, observedHistory:true);
            else _fallback = MovingAverageSmootherFactory.Create(kind,Math.Max(1,length));
        }
        internal BigInteger Next(BigInteger value, bool final)
        {
            if (_wide is null) return ExactVarianceWindow.Units(_fallback!.Next(ProjectionBandsSnapshot.Publish(value),final));
            var exact = new ExactMeanAccumulator(); exact.Add(double.Epsilon,value);
            var result = _wide.Next(RocBankValue.Round(exact),final);
            return ExactVarianceWindow.Units(result.Mantissa) << result.UpperShift;
        }
        internal void Reset() { _wide?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _wide?.Dispose(); _fallback?.Dispose(); }
    }
}
