using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ClassicOscillatorFiniteInputTests
{
    [Theory]
    [InlineData(ClassicAverageMethod.Sma)]
    [InlineData(ClassicAverageMethod.Wma)]
    [InlineData(ClassicAverageMethod.Trima)]
    public void RejectedClosesPreserveValidationAndStateAcrossReset(ClassicAverageMethod method)
    {
        foreach (var periods in new[] { (1, 1), (1, 3), (2, 5), (5, 2) })
        foreach (bool percentage in new[] { false, true })
        {
            var owner = new ClassicPriceOscillator(periods.Item1, periods.Item2, method, percentage);
            var actual = (IMultiOutputState)owner.CreateState();
            var control = (IMultiOutputState)owner.CreateState();
            using var actualLifetime = actual as IDisposable;
            using var controlLifetime = control as IDisposable;
            var output = new double[2];
            var expected = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                actual.Reset(); control.Reset();
                foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    Replay(10); Replay(11); Replay(12);
                    var rejection = Assert.Throws<ArgumentOutOfRangeException>(() => ExactVarianceWindow.Units(invalid));
                    var bad = new Bar(default, invalid, invalid, invalid, invalid, 1);
                    var exception = Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(in bad, output));
                    Assert.Equal(rejection.ParamName, exception.ParamName);
                    Assert.Equal(new double[2], output);
                    foreach (double value in new[] { 0d, -0d, double.Epsilon, -double.Epsilon,
                        double.MaxValue, -double.MaxValue, 1, 2, 3, 4 }) Replay(value);
                }
            }

            void Replay(double value)
            {
                var bar = new Bar(default, value, value, value, value, 1);
                actual.Update(in bar, output);
                control.Update(in bar, expected);
                for (int slot = 0; slot < 2; slot++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot]), BitConverter.DoubleToInt64Bits(output[slot]));
            }
        }
    }
}
