#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Only the default Modified Hikkake batch kernel uses this wrapper. The
    // original state owns every exact range/near decision and confirmation.
    private sealed class ModifiedHikkakeSparseReplay(HikkakeState state)
    {
        private const int Prefix = 10; // Near period five plus original warmup.
        private readonly Bar[] _prior = new Bar[Prefix];
        private int _next, _seen, _remaining;

        public double Update(in Bar bar)
        {
            bool candidate = _seen >= 7 && CouldForm(in bar);
            double value = 0;
            if (candidate || _remaining > 0)
            {
                if (_remaining == 0)
                {
                    state.Reset();
                    int oldest = _seen == Prefix ? _next : 0;
                    for (int i = 0; i < _seen; i++)
                        state.Update(in _prior[(oldest + i) % Prefix]);
                }
                value = state.Update(in bar);
                if (candidate) _remaining = 3;
                else _remaining--;
            }
            // Warmup formations still reach the original state, so a pending
            // confirmation can become visible at the first global-ready row.
            if (_seen < Prefix) value = 0;
            _prior[_next] = bar;
            if (++_next == Prefix) _next = 0;
            if (_seen < Prefix) _seen++;
            return value;
        }

        private bool CouldForm(in Bar bar)
        {
            ref readonly var second = ref _prior[(_next + Prefix - 1) % Prefix];
            ref readonly var first = ref _prior[(_next + Prefix - 2) % Prefix];
            ref readonly var before = ref _prior[(_next + Prefix - 3) % Prefix];
            // This is only a necessary geometry condition. In particular, a
            // near-threshold miss must still let the original pending state
            // confirm on this bar; it cannot be treated as an exact formation.
            return second.High < first.High && second.Low > first.Low
                && first.High < before.High && first.Low > before.Low
                && (bar.High < second.High && bar.Low < second.Low
                    || bar.High > second.High && bar.Low > second.Low);
        }
    }
}
#endif
