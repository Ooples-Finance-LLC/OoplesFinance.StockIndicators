using System.Collections;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using Trady.Analysis.Extension;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Only native calls and necessary output ownership belong in timed methods.
// Normalization, reflection, references and assertions below are setup/test work.
internal sealed class CpuNativeWorkload
{
    internal string PairId { get; }
    internal CompetitorData Data { get; }
    private readonly QuanTAlib.TValue[] _values;
    private readonly (DateTime, double)[] _tuples;
    private readonly decimal?[] _decimalCloses;
    private readonly (decimal Open, decimal Close)[] _bodies;
    private readonly double[] _doubleOutput;
    private readonly int[] _integerOutput;
    internal CpuNativeWorkload(string pairId, int count, bool commonGrid = false)
    {
        PairId = pairId;
        var data = ComparisonVerifier.BenchmarkFixture(ComparisonPairs.Get(CanonicalPair(pairId)), count);
        // Decimal-native libraries receive exactly the same binary-grid values,
        // not decimal-rounded approximations of the double fixture.
        Data = commonGrid || pairId.StartsWith("Trady.", StringComparison.Ordinal) || pairId.StartsWith("Skender.", StringComparison.Ordinal)
            ? CompetitorData.FromOhlcv(Grid(data.Opens), Grid(data.Highs), Grid(data.Lows), Grid(data.Closes), Grid(data.Volumes))
            : data;
        _values = Data.Closes.Select(v => new QuanTAlib.TValue(v, true, false)).ToArray();
        _tuples = Data.Dates.Zip(Data.Closes, (date, close) => (date, close)).ToArray();
        _decimalCloses = Data.Closes.Select(value => (decimal?)value).ToArray();
        _bodies = Data.Opens.Zip(Data.Closes, (open, close) => ((decimal)open, (decimal)close)).ToArray();
        _doubleOutput = new double[count]; _integerOutput = new int[count];
    }
    internal static string CanonicalPair(string id) => id.EndsWith(".Tuple", StringComparison.Ordinal) ? id[..^6] : id;
    private static double[] Grid(double[] values) => values.Select(v => Math.Round(v * 1024) / 1024).ToArray();
    internal static bool SupportsReusable(string id) => id is "TaLib.Functions.Asin" or "TaLib.Candles.RickshawMan";
    internal static bool SupportsStreaming(string id) => id is "QuanTAlib.Jma" or "QuanTAlib.Atr";
    internal double[] OoplesOwned()
    {
        if (PairId == "TaLib.Functions.Asin")
        {
            var values = new double[Data.Count];
            IndicatorKernels.Asin(Data.Closes, values); return values;
        }
        var kernel = CpuKernelPilots.Create(PairId);
        var output = new double[Data.Count * kernel.OutputCount];
        kernel.Process(Data.IndicatorBars, output);
        if (PairId == "Skender.GetFractal")
        {
            // Owning batch results belong to the center bar, like Skender's.
            // Confirmation-time placement is only the incremental API contract.
            Array.Copy(output, 40, output, 0, output.Length - 40);
            Array.Fill(output, double.NaN, output.Length - 40, 40);
        }
        return output;
    }
    internal QuanTAlib.AbstractBase NewNativeStream() => PairId switch
    {
        "QuanTAlib.Jma" => new QuanTAlib.Jma(20, 0, 10),
        "QuanTAlib.Atr" => new QuanTAlib.Atr(20),
        "QuanTAlib.Sma" => new QuanTAlib.Sma(20),
        _ => throw new NotSupportedException(PairId + " has no streaming pilot API.")
    };
    internal void RunOoplesStream(IndicatorKernel kernel, double[] output)
    {
        for (var i = 0; i < Data.Count; i++) kernel.Update(Data.IndicatorBars[i], output.AsSpan(i, 1));
    }
    internal void RunNativeStream(QuanTAlib.AbstractBase model, double[] output)
    {
        if (model is QuanTAlib.Jma jma)
            for (var i = 0; i < Data.Count; i++) output[i] = jma.Calc(_values[i]).Value;
        else if (model is QuanTAlib.Atr atr)
            for (var i = 0; i < Data.Count; i++) output[i] = atr.Calc(Data.Bars[i]).Value;
        else if (model is QuanTAlib.Sma sma)
            for (var i = 0; i < Data.Count; i++) output[i] = sma.Calc(_values[i]).Value;
        else throw new NotSupportedException(model.GetType().Name);
    }
    internal object NativeOwned()
    {
        switch (PairId)
        {
            case "QuanTAlib.Jma":
            case "QuanTAlib.Atr":
            case "QuanTAlib.Sma":
                var output = new double[Data.Count];
                RunNativeStream(NewNativeStream(), output);
                return output;
            case "TaLib.Functions.Asin":
                var doubles = new double[Data.Count]; Asin(doubles); return doubles;
            case "TaLib.Functions.Sma":
                var averages = new double[Data.Count];
                Functions.Sma<double>(Data.Closes, System.Range.All, averages, out _, 20);
                return averages;
            case "Skender.GetSma":
                return Data.Quotes.GetSma(20);
            case "Skender.GetSma.Tuple":
                return _tuples.GetSma(20);
            case "TaLib.Candles.RickshawMan":
                var integers = new int[Data.Count]; Rickshaw(integers); return integers;
            case "Skender.GetRollingPivots":
                return Data.Quotes.GetRollingPivots(20, 0, PivotPointType.Standard);
            case "Skender.GetFractal":
                return Data.Quotes.GetFractal(20, 20, EndType.HighLow);
            case "Trady.Candlestick.BullishShortDay":
                return new Trady.Analysis.Candlestick.BullishShortDay(Data.Candles, 20, .25m).Compute();
            case "Trady.Indicator.SimpleMovingAverage":
                return Data.Candles.Sma(20);
            case "Trady.Indicator.SimpleMovingAverage.Tuple":
                return new Trady.Analysis.Indicator.SimpleMovingAverageByTuple(_decimalCloses, 20).Compute();
            case "Trady.Candlestick.BullishShortDay.Tuple":
                return new Trady.Analysis.Candlestick.BullishShortDayByTuple(_bodies, 20, .25m).Compute();
            default: throw new ArgumentOutOfRangeException(nameof(PairId));
        }
    }
    internal object NativeReusable()
    {
        if (PairId == "TaLib.Functions.Asin") { Asin(_doubleOutput); return _doubleOutput; }
        if (PairId == "TaLib.Candles.RickshawMan") { Rickshaw(_integerOutput); return _integerOutput; }
        throw new NotSupportedException(PairId + " has no reusable batch pilot API.");
    }
    private void Asin(double[] output) => Functions.Asin<double>(Data.Closes, System.Range.All, output, out _);
    private void Rickshaw(int[] output) => Candles.RickshawMan<double>(
        Data.Opens, Data.Highs, Data.Lows, Data.Closes, System.Range.All, output, out _);

    internal void Verify()
    {
        var pair = ComparisonPairs.Get(PairId);
        ComparisonVerifier.Check(pair, ComparisonVerifier.BenchmarkFixture(pair, 160), 20, verifyIsolation: false);
        var kernel = CpuKernelPilots.Create(PairId);
        CpuKernelPilots.Verify(PairId, Data, kernel, new double[Data.Count * kernel.OutputCount]);
        CpuKernelPilots.VerifyOutput(PairId, Data, kernel.OutputCount, OoplesOwned(), aligned: true);
        var expected = (pair.CompetitorReference ?? pair.Reference)?.Invoke(Data, 20) ?? ComparisonVerifier.Reference(Data.Closes, 20, weighted: false);
        ComparisonVerifier.Compare(expected, Normalize(NativeOwned()), PairId + " direct owning", pair.ErrorBudget);
        if (SupportsReusable(PairId))
        {
            VerifyNativeStatus();
            ComparisonVerifier.Compare(expected, Normalize(NativeReusable()), PairId + " direct reusable", pair.ErrorBudget);
            ComparisonVerifier.Compare(expected, Normalize(NativeReusable()), PairId + " repeated reusable", pair.ErrorBudget);
        }
        if (SupportsStreaming(PairId))
        {
            var values = new double[Data.Count];
            RunOoplesStream(CpuKernelPilots.Create(PairId), values);
            CpuKernelPilots.VerifyOutput(PairId, Data, 1, values);
            RunNativeStream(NewNativeStream(), values);
            ComparisonVerifier.Compare(expected, new ComparisonSeries(0, values), PairId + " direct streaming", pair.ErrorBudget);
        }
    }
    private void VerifyNativeStatus()
    {
        TALib.Core.RetCode code; System.Range range;
        if (PairId == "TaLib.Functions.Asin")
            code = Functions.Asin<double>(Data.Closes, System.Range.All, _doubleOutput, out range);
        else
            code = Candles.RickshawMan<double>(Data.Opens, Data.Highs, Data.Lows, Data.Closes, System.Range.All, _integerOutput, out range);
        var first = PairId == "TaLib.Functions.Asin" ? 0 : 10;
        if (code != TALib.Core.RetCode.Success || range.GetOffsetAndLength(Data.Count) != (first, Data.Count - first))
            throw new InvalidOperationException(PairId + " returned invalid native status/range.");
    }
    internal ComparisonSeries Normalize(object native)
    {
        if (PairId == "TaLib.Functions.Sma")
        {
            var values = new double[Data.Count];
            Array.Fill(values, double.NaN);
            Array.Copy((double[])native, 0, values, 19, Data.Count - 19);
            return new(19, values);
        }
        if (PairId == "TaLib.Candles.RickshawMan")
        {
            var values = new double[Data.Count]; var packed = (int[])native;
            for (var i = 10; i < values.Length; i++) values[i] = packed[i - 10];
            return new(10, values);
        }
        if (native is double[] doubles) return new(PairId == "QuanTAlib.Sma" ? 19 : 0, doubles);
        var rows = ((IEnumerable)native).Cast<object>().ToArray();
        if (PairId == "Trady.Indicator.SimpleMovingAverage.Tuple")
            return new(19, rows.Select(value => value is null ? double.NaN : (double)(decimal)value).ToArray());
        if (PairId == "Trady.Candlestick.BullishShortDay.Tuple")
            return new(0, rows.Select(value => (bool)value ? 1d : 0d).ToArray());
        if (PairId is "Skender.GetSma" or "Skender.GetSma.Tuple")
            return new(19, rows.Cast<SmaResult>().Select(row => row.Sma ?? double.NaN).ToArray());
        if (PairId.StartsWith("Trady.", StringComparison.Ordinal))
        {
            var values = rows.Select(row => row.GetType().GetProperty("Tick")!.GetValue(row))
                .Select(value => value is bool flag ? (flag ? 1d : 0d) : value is null
                    ? (PairId.Contains("Candlestick", StringComparison.Ordinal) ? 0 : double.NaN)
                    : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new(PairId.Contains("Candlestick", StringComparison.Ordinal) ? 0 : 19, values);
        }
        var names = PairId == "Skender.GetFractal" ? new[] { "Bear", "Bull" } : PivotLevelComparison.Names;
        var columns = names.Select(name => rows.Select(row =>
        {
            var property = PairId == "Skender.GetFractal" ? "Fractal" + name : name;
            var value = row.GetType().GetProperty(property)!.GetValue(row);
            return value is null ? (double?)null : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }).ToArray()).ToArray();
        return RetrospectivePriceComparison.Series(names, columns);
    }
}
