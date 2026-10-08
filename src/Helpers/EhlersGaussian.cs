namespace OoplesFinance.StockIndicators.Helpers;

internal static class EhlersGaussian
{
    internal static double Gain(int length, int poles)
    {
        // Cascaded one-pole sections have half power at angular frequency 2*pi/length.
        // Period two is Nyquist, the shortest representable cycle.
        var sine = Math.Sin(Math.PI / Math.Max(2, length));
        var beta = 2 * sine * sine / (Math.Pow(2, 1d / poles) - 1);
        // Rationalized positive quadratic root avoids cancellation in sqrt(beta²+2beta)-beta.
        return 2 / (1 + Math.Sqrt(1 + 2 / beta));
    }

    internal static double Next(double input, double gain, double[] stages, bool isFinal)
    {
        var value = input;
        for (var stage = 0; stage < stages.Length; stage++)
        {
            value = gain * value + (1 - gain) * stages[stage];
            if (isFinal) stages[stage] = value;
        }
        return value;
    }
}
