namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Laguerre all-pass stages retaining differences through normalization.</summary>
internal struct AdaptiveLaguerreStages
{
    private SpreadNumber _l0, _l1, _l2, _l3;

    internal double Next(double source, double gain)
    {
        var l0 = SpreadNumber.Add(_l0, SpreadNumber.Subtract(new(source), _l0).Times(gain));
        SpreadNumber Stage(SpreadNumber current, SpreadNumber oldInput, SpreadNumber oldOutput) =>
            SpreadNumber.Add(SpreadNumber.Add(oldOutput, SpreadNumber.Subtract(oldInput, current)),
                SpreadNumber.Subtract(current, oldOutput).Times(gain));
        var l1 = Stage(l0, _l0, _l1);
        var l2 = Stage(l1, _l1, _l2);
        var l3 = Stage(l2, _l2, _l3);
        _l0 = l0; _l1 = l1; _l2 = l2; _l3 = l3;
        var d0 = SpreadNumber.Subtract(l0, l1).Value;
        var d1 = SpreadNumber.Subtract(l1, l2).Value;
        var d2 = SpreadNumber.Subtract(l2, l3).Value;
        var total = Math.Abs(d0)+Math.Abs(d1)+Math.Abs(d2);
        var scale = Math.Max(Math.Max(Math.Abs(l0.Value), Math.Abs(l1.Value)), Math.Max(Math.Abs(l2.Value), Math.Abs(l3.Value)));
        return total <= 1.4210854715202004e-14*scale ? 0
            : (Math.Max(0, d0)+Math.Max(0, d1)+Math.Max(0, d2))/total;
    }
}
