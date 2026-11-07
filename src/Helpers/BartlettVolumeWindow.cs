namespace OoplesFinance.StockIndicators.Helpers;

// Periodic Bartlett weights, w(lag) = min(lag, length - lag).
internal sealed class BartlettVolumeWindow : IDisposable
{
    private readonly int _length, _half;
    private readonly Queue<Term> _recent = new(), _oldest = new();
    private Term? _middle;
    private ExactMeanAccumulator _weighted, _mass, _recentProduct, _recentVolume, _oldProduct, _oldVolume;
    private readonly record struct Term(ExactMeanAccumulator Product, ExactMeanAccumulator Volume);

    internal BartlettVolumeWindow(int length)
    {
        _length = Math.Max(1, length);
        _half = _length / 2;
    }

    internal double Next(double price, double volume, bool final)
    {
        // Decode even the zero-weight current tap, preserving finite-input errors.
        var product = new ExactMeanAccumulator(); product.AddProduct(price, volume);
        var weight = new ExactMeanAccumulator(); weight.AddProduct(volume, 1);
        if (_length == 1) return product.Ratio(weight);

        // A shift adds one to the newest half's tap weights and subtracts one
        // from the oldest half's. An odd window has one unchanged middle tap.
        var weighted = _weighted; weighted.AddExact(_recentProduct); weighted.Subtract(_oldProduct);
        var mass = _mass; mass.AddExact(_recentVolume); mass.Subtract(_oldVolume);
        var result = weighted.Ratio(mass);
        if (!final) return result;
        _weighted = weighted; _mass = mass;
        _recent.Enqueue(new(product, weight));
        _recentProduct.AddExact(product); _recentVolume.AddExact(weight);
        if (_recent.Count > _half)
        {
            var outgoing = _recent.Dequeue();
            _recentProduct.Subtract(outgoing.Product); _recentVolume.Subtract(outgoing.Volume);
            if ((_length & 1) != 0)
            {
                var previousMiddle = _middle;
                _middle = outgoing;
                if (previousMiddle is null) return result;
                outgoing = previousMiddle.Value;
            }
            _oldest.Enqueue(outgoing);
            _oldProduct.AddExact(outgoing.Product); _oldVolume.AddExact(outgoing.Volume);
            if (_oldest.Count > _half)
            {
                outgoing = _oldest.Dequeue();
                _oldProduct.Subtract(outgoing.Product); _oldVolume.Subtract(outgoing.Volume);
            }
        }
        return result;
    }

    internal void Reset()
    {
        _recent.Clear(); _oldest.Clear(); _middle = null;
        _weighted = _mass = _recentProduct = _recentVolume = _oldProduct = _oldVolume = default;
    }
    public void Dispose() => Reset();
}
