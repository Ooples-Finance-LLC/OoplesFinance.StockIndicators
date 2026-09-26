namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RoundedKaufmanWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly double _fast, _slow;
    private double _previous;
    private ExactMeanAccumulator _travel;
    private double _beforeWindow;
    private bool _hasBeforeWindow;

    internal RoundedKaufmanWindow(int length, int fastLength = 2, int slowLength = 30)
    {
        _values = new PooledRingBuffer<double>(Math.Max(1, length));
        _fast = 2 / (Math.Max(1, fastLength) + 1d);
        _slow = 2 / (Math.Max(1, slowLength) + 1d);
    }

    private static void AddDistance(ref ExactMeanAccumulator sum, double left, double right, int direction = 1)
    {
        sum.Add(Math.Max(left, right), direction);
        sum.Add(Math.Min(left, right), -direction);
    }

    private static double Blend(double previous, double current, double weight)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(previous);
        sum.AddProduct(current, weight);
        sum.AddProduct(previous, weight, -1);
        return sum.Mean(1);
    }

    private static double UpdateAverage(double previous, double current, double efficiency, double fast, double slow)
    {
        var gain = Blend(slow, fast, efficiency);
        return Blend(previous, current, gain * gain);
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length,
        int fastLength = 2, int slowLength = 30, bool efficiencyOnly = false)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        var fast = 2 / (Math.Max(1, fastLength) + 1d);
        var slow = 2 / (Math.Max(1, slowLength) + 1d);
        double previous = 0;
        for (var i = 0; i < input.Length; i++)
        {
            double efficiency = 0;
            if (i >= length)
            {
                var travel = new ExactMeanAccumulator();
                for (var j = i - length + 1; j <= i; j++) AddDistance(ref travel, input[j], input[j - 1]);
                var distance = new ExactMeanAccumulator();
                AddDistance(ref distance, input[i], input[i - length]);
                efficiency = distance.Ratio(travel);
            }
            if (!efficiencyOnly) previous = i < length ? input[i] : UpdateAverage(previous, input[i], efficiency, fast, slow);
            output[i] = efficiencyOnly ? efficiency : previous;
        }
    }

    internal (double Average, double Efficiency) Next(double value, bool commit)
    {
        var travel = _travel;
        if (_values.Count > 0) AddDistance(ref travel, value, _values[_values.Count - 1]);
        if (_hasBeforeWindow) AddDistance(ref travel, _values[0], _beforeWindow, -1);
        double efficiency = 0;
        if (_values.Count == _values.Capacity)
        {
            var distance = new ExactMeanAccumulator();
            AddDistance(ref distance, value, _values[0]);
            efficiency = distance.Ratio(travel);
        }
        var average = _values.Count < _values.Capacity ? value : UpdateAverage(_previous, value, efficiency, _fast, _slow);
        if (commit)
        {
            _travel = travel;
            if (_values.Count == _values.Capacity)
            {
                _beforeWindow = _values[0];
                _hasBeforeWindow = true;
            }
            _values.TryAdd(value, out _);
            _previous = average;
        }
        return (average, efficiency);
    }

    internal void Reset() { _values.Clear(); _previous = 0; _travel = default; _beforeWindow = 0; _hasBeforeWindow = false; }
    public void Dispose() => _values.Dispose();
}
