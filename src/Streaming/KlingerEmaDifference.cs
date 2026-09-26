namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Stable difference of Klinger EMA legs without subtracting two large rounded averages.</summary>
internal sealed class KlingerEmaDifference
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private int _count;
    private double _slow;
    private double _difference;

    internal KlingerEmaDifference(int fastLength, int slowLength)
    { _fastLength = Math.Max(1, fastLength); _slowLength = Math.Max(1, slowLength); }

    internal double Next(double value, bool commit)
    {
        var fastGain = _count < _fastLength ? 1d / (_count + 1) : 2d / (_fastLength + 1);
        var slowGain = _count < _slowLength ? 1d / (_count + 1) : 2d / (_slowLength + 1);
        var residual = value - _slow;
        var difference = (1 - fastGain) * _difference + (fastGain - slowGain) * residual;
        if (commit)
        {
            _slow += slowGain * residual;
            _difference = difference;
            _count++;
        }
        return difference;
    }

    internal void Reset() { _count = 0; _slow = 0; _difference = 0; }
}
