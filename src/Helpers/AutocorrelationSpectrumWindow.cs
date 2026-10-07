using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AutocorrelationSpectrumWindow
{
    private readonly int _upper, _lower, _lag;
    private readonly RoofAutocorrelationWindow _correlation;
    private readonly List<double> _history = new();
    private readonly Dictionary<int, double> _powers = new();
    internal AutocorrelationSpectrumWindow(int upper, int lower, int firstLag)
    { _upper = Math.Max(1, upper); _lower = Math.Max(1, lower); _lag = Math.Max(0, firstLag); _correlation = new(_upper, _lower); }
    internal (double Value, Signal Signal) Next(double price, bool commit)
    {
        var point = _correlation.Next(price, commit); var active = _powers.Count > 0;
        for (var lag = _lag; !active && lag <= _upper && lag <= _history.Count; lag++) active = (lag == 0 ? point.Value : _history[_history.Count - lag]) != 0;
        var next = new List<double>(); var peak = 0d;
        if (active)
        {
            for (long period = _lower; period <= _upper; period++)
            {
                var real = new ExactMeanAccumulator(); var imaginary = new ExactMeanAccumulator();
                for (var lag = _lag; lag <= _upper && lag <= _history.Count; lag++)
                { var value = lag == 0 ? point.Value : _history[_history.Count - lag]; var angle = 2 * Math.PI * ((double)lag / period); real.AddProduct(value, Math.Cos(angle)); imaginary.AddProduct(value, Math.Sin(angle)); }
                var x = real.Mean(1); var y = imaginary.Mean(1); var energy = new ExactMeanAccumulator(); energy.AddProduct(x, x); energy.AddProduct(y, y); var square = energy.Mean(1);
                _powers.TryGetValue((int)period, out var previous); var power = new ExactMeanAccumulator(); power.AddProduct(square * square, .2); power.AddProduct(previous, .8); var valueNow = power.Mean(1); next.Add(valueNow); peak = Math.Max(peak, valueNow);
            }
        }
        var weighted = new ExactMeanAccumulator(); var total = new ExactMeanAccumulator();
        for (var index = 0; index < next.Count; index++)
        { var power = peak == 0 ? 0 : next[index] / peak; if (power >= .5) { weighted.AddProduct(_lower + (double)index, power); total.Add(power); } }
        var cycle = total.IsExactlyZero ? 0 : weighted.Ratio(total);
        if (commit) { for (var index = 0; index < next.Count; index++) _powers[_lower + index] = next[index]; if (_history.Count == _upper) _history.RemoveAt(0); _history.Add(point.Value); } return (cycle, point.Signal);
    }
    internal void Reset() { _correlation.Reset(); _history.Clear(); _powers.Clear(); }
}
