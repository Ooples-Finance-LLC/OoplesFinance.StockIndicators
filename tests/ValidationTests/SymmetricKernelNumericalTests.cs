using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SymmetricKernelNumericalTests
{
    [Theory]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(8, false)]
    [InlineData(14, false)]
    [InlineData(31, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(8, true)]
    [InlineData(14, true)]
    [InlineData(31, true)]
    public void EverySymmetricTapPairCancelsOppositeInputs(int length, bool gaussian)
    {
        IStreamingIndicatorState state = gaussian
            ? new ArnaudLegouxMovingAverageState(length, .5, 6)
            : new EhlersHammingMovingAverageState(length, 3);
        using var lifetime = (IDisposable)state;
        for (var pair = 0; pair < length / 2; pair++)
        foreach (var magnitude in new[] { 1d, double.MaxValue, double.Epsilon })
        {
            state.Reset();
            for (var i = 0; i < length; i++)
            {
                var value = i == pair ? magnitude : i == length - 1 - pair ? -magnitude : 0;
                var time = DateTime.UnixEpoch.AddMinutes(i);
                var bar = new OhlcvBar("SYMMETRY", BarTimeframe.Minutes(1), time, time, value, value, value, value, 1, true);
                foreach (var commit in new[] { false, true })
                {
                    var result = state.Update(bar, commit, true);
                    if (i == length - 1) Assert.Equal(0, result.Value);
                }
            }
        }
    }
}
