namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Unit-DC input gains for the nine fixed Ehlers Chebyshev cascades.</summary>
internal static class EhlersChebyshevGains
{
    // At constant input x, the first section is g*(2+lead)*x/(1-p1+p2),
    // and the second multiplies it by (2+zero)/(1-f1+f2). Solving for
    // output == x avoids the permanent bias from independently rounded gains.
    internal const double Minus2 = (1 - 0.293 + 0.063) * (1 - 0.451 + 0.481) / ((2 + 1.907) * (2 + 0.513));
    internal const double Minus1 = (1 - 0.731 + 0.166) * (1 - 1.008 + 0.561) / ((2 + 1.777) * (2 + 0.977));
    internal const double Zero = (1 - 1.026 + 0.282) * (1 - 1.329 + 0.644) / ((2 + 1.572) * (2 + 0.356));
    internal const double One = (1 - 1.281 + 0.426) * (1 - 1.565 + 0.729) / ((2 + 1.192) * (2 - 0.384));
    internal const double Two = (1 - 1.46 + 0.543) * (1 - 1.703 + 0.793) / ((2 + 0.681) * (2 - 0.966));
    internal const double Three = (1 - 1.606 + 0.65) * (1 - 1.801 + 0.848) / ((2 + 0.012) * (2 - 1.408));
    internal const double Four = (1 - 1.716 + 0.74) * (1 - 1.866 + 0.89) / ((2 - 0.669) * (2 - 1.685));
    internal const double Five = (1 - 1.8 + 0.811) * (1 - 1.91 + 0.922) / ((2 - 1.226) * (2 - 1.842));
    internal const double Six = (1 - 1.873 + 0.878) * (1 - 1.946 + 0.951) / ((2 - 1.659) * (2 - 1.957));
}
