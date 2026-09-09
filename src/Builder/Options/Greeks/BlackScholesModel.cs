namespace OoplesFinance.StockIndicators.Builder.Options.Greeks;

/// <summary>
/// Black-Scholes option pricing model.
/// Calculates option prices and Greeks for European-style options.
/// </summary>
public static class BlackScholesModel
{
    private const decimal SqrtTwoPi = 2.5066282746310002m;
    private const int MaxIterations = 100;
    private const decimal IvTolerance = 0.0001m;

    /// <summary>
    /// Calculates the theoretical price of a call option.
    /// </summary>
    /// <param name="s">Current stock price.</param>
    /// <param name="k">Strike price.</param>
    /// <param name="t">Time to expiration in years.</param>
    /// <param name="r">Risk-free interest rate (annualized, decimal).</param>
    /// <param name="sigma">Volatility (annualized, decimal).</param>
    /// <returns>The theoretical call price.</returns>
    public static decimal CallPrice(decimal s, decimal k, decimal t, decimal r, decimal sigma)
    {
        if (t <= 0) return Math.Max(0, s - k);
        if (sigma <= 0) return Math.Max(0, s - k * (decimal)Math.Exp((double)(-r * t)));

        var (d1, d2) = CalculateD1D2(s, k, t, r, sigma);
        var nd1 = NormalCdf(d1);
        var nd2 = NormalCdf(d2);

        var callPrice = s * nd1 - k * (decimal)Math.Exp((double)(-r * t)) * nd2;
        return Math.Max(0, callPrice);
    }

    /// <summary>
    /// Calculates the theoretical price of a put option.
    /// </summary>
    /// <param name="s">Current stock price.</param>
    /// <param name="k">Strike price.</param>
    /// <param name="t">Time to expiration in years.</param>
    /// <param name="r">Risk-free interest rate (annualized, decimal).</param>
    /// <param name="sigma">Volatility (annualized, decimal).</param>
    /// <returns>The theoretical put price.</returns>
    public static decimal PutPrice(decimal s, decimal k, decimal t, decimal r, decimal sigma)
    {
        if (t <= 0) return Math.Max(0, k - s);
        if (sigma <= 0) return Math.Max(0, k * (decimal)Math.Exp((double)(-r * t)) - s);

        var (d1, d2) = CalculateD1D2(s, k, t, r, sigma);
        var nMd1 = NormalCdf(-d1);
        var nMd2 = NormalCdf(-d2);

        var putPrice = k * (decimal)Math.Exp((double)(-r * t)) * nMd2 - s * nMd1;
        return Math.Max(0, putPrice);
    }

    /// <summary>
    /// Calculates the option price.
    /// </summary>
    public static decimal OptionPrice(OptionType type, decimal s, decimal k, decimal t, decimal r, decimal sigma)
    {
        return type == OptionType.Call
            ? CallPrice(s, k, t, r, sigma)
            : PutPrice(s, k, t, r, sigma);
    }

    /// <summary>
    /// Calculates all Greeks for an option.
    /// </summary>
    /// <param name="type">Option type (call or put).</param>
    /// <param name="s">Current stock price.</param>
    /// <param name="k">Strike price.</param>
    /// <param name="t">Time to expiration in years.</param>
    /// <param name="r">Risk-free interest rate (annualized, decimal).</param>
    /// <param name="sigma">Volatility (annualized, decimal).</param>
    /// <returns>The calculated Greeks.</returns>
    public static OptionGreeks CalculateGreeks(OptionType type, decimal s, decimal k, decimal t, decimal r, decimal sigma)
    {
        if (t <= 0 || sigma <= 0)
        {
            return new OptionGreeks
            {
                Delta = type == OptionType.Call ? (s > k ? 1m : 0m) : (s < k ? -1m : 0m)
            };
        }

        var (d1, d2) = CalculateD1D2(s, k, t, r, sigma);
        var nd1 = NormalCdf(d1);
        var nMd1 = NormalCdf(-d1);
        var nd1Pdf = NormalPdf(d1);
        var sqrtT = (decimal)Math.Sqrt((double)t);
        var expRt = (decimal)Math.Exp((double)(-r * t));

        var greeks = new OptionGreeks();

        // Delta: dV/dS
        greeks.Delta = type == OptionType.Call ? nd1 : nd1 - 1m;

        // Gamma: d²V/dS² (same for calls and puts)
        greeks.Gamma = nd1Pdf / (s * sigma * sqrtT);

        // Vega: dV/dσ (same for calls and puts, per 1% change in IV)
        greeks.Vega = s * sqrtT * nd1Pdf / 100m;

        // Theta: dV/dt (per day, so divide by 365)
        if (type == OptionType.Call)
        {
            greeks.Theta = (-(s * nd1Pdf * sigma) / (2 * sqrtT)
                - r * k * expRt * NormalCdf(d2)) / 365m;
        }
        else
        {
            greeks.Theta = (-(s * nd1Pdf * sigma) / (2 * sqrtT)
                + r * k * expRt * NormalCdf(-d2)) / 365m;
        }

        // Rho: dV/dr (per 1% change in interest rate)
        if (type == OptionType.Call)
        {
            greeks.Rho = k * t * expRt * NormalCdf(d2) / 100m;
        }
        else
        {
            greeks.Rho = -k * t * expRt * NormalCdf(-d2) / 100m;
        }

        // Vanna: d²V/dSdσ
        greeks.Vanna = nd1Pdf * d2 / sigma;

        // Charm: dDelta/dt (per day)
        var charmTerm = 2 * r * t - d2 * sigma * sqrtT;
        if (type == OptionType.Call)
        {
            greeks.Charm = -nd1Pdf * charmTerm / (2 * t * sigma * sqrtT) / 365m;
        }
        else
        {
            greeks.Charm = -nd1Pdf * charmTerm / (2 * t * sigma * sqrtT) / 365m;
        }

        // Volga (Vomma): d²V/dσ²
        greeks.Volga = greeks.Vega * d1 * d2 / sigma;

        return greeks;
    }

    /// <summary>
    /// Calculates implied volatility from option price using Newton-Raphson.
    /// </summary>
    /// <param name="type">Option type.</param>
    /// <param name="optionPrice">Market price of the option.</param>
    /// <param name="s">Current stock price.</param>
    /// <param name="k">Strike price.</param>
    /// <param name="t">Time to expiration in years.</param>
    /// <param name="r">Risk-free interest rate.</param>
    /// <returns>The implied volatility, or null if convergence failed.</returns>
    public static decimal? ImpliedVolatility(OptionType type, decimal optionPrice, decimal s, decimal k, decimal t, decimal r)
    {
        if (optionPrice <= 0 || t <= 0 || s <= 0 || k <= 0)
        {
            return null;
        }

        // Initial guess using Brenner-Subrahmanyam approximation
        var sigma = (decimal)Math.Sqrt(2 * Math.PI / (double)t) * optionPrice / s;
        sigma = Math.Max(0.01m, Math.Min(sigma, 5m));

        // Newton-Raphson iteration
        for (var i = 0; i < MaxIterations; i++)
        {
            var price = OptionPrice(type, s, k, t, r, sigma);
            var diff = price - optionPrice;

            if (Math.Abs(diff) < IvTolerance)
            {
                return sigma;
            }

            // Calculate vega for Newton-Raphson step
            var vega = CalculateVega(s, k, t, sigma);
            if (vega < 0.0001m)
            {
                // Vega too small, try bisection instead
                return ImpliedVolatilityBisection(type, optionPrice, s, k, t, r);
            }

            sigma -= diff / (vega * 100m); // vega is per 1%, so multiply by 100

            // Keep sigma in reasonable bounds
            sigma = Math.Max(0.001m, Math.Min(sigma, 10m));
        }

        // If Newton-Raphson fails, try bisection
        return ImpliedVolatilityBisection(type, optionPrice, s, k, t, r);
    }

    /// <summary>
    /// Calculates implied volatility using bisection method (more robust but slower).
    /// </summary>
    private static decimal? ImpliedVolatilityBisection(OptionType type, decimal optionPrice, decimal s, decimal k, decimal t, decimal r)
    {
        var low = 0.001m;
        var high = 5m;

        for (var i = 0; i < MaxIterations; i++)
        {
            var mid = (low + high) / 2m;
            var price = OptionPrice(type, s, k, t, r, mid);

            if (Math.Abs(price - optionPrice) < IvTolerance)
            {
                return mid;
            }

            if (price > optionPrice)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }

        return (low + high) / 2m;
    }

    #region Helper Methods

    private static (decimal d1, decimal d2) CalculateD1D2(decimal s, decimal k, decimal t, decimal r, decimal sigma)
    {
        var sqrtT = (decimal)Math.Sqrt((double)t);
        var d1 = ((decimal)Math.Log((double)(s / k)) + (r + sigma * sigma / 2) * t) / (sigma * sqrtT);
        var d2 = d1 - sigma * sqrtT;
        return (d1, d2);
    }

    private static decimal CalculateVega(decimal s, decimal k, decimal t, decimal sigma)
    {
        var (d1, _) = CalculateD1D2(s, k, t, 0.05m, sigma);
        var sqrtT = (decimal)Math.Sqrt((double)t);
        return s * sqrtT * NormalPdf(d1) / 100m;
    }

    /// <summary>
    /// Standard normal cumulative distribution function.
    /// </summary>
    public static decimal NormalCdf(decimal x)
    {
        return (decimal)NormalCdfDouble((double)x);
    }

    /// <summary>
    /// Standard normal probability density function.
    /// </summary>
    public static decimal NormalPdf(decimal x)
    {
        return (decimal)Math.Exp(-0.5 * (double)(x * x)) / SqrtTwoPi;
    }

    private static double NormalCdfDouble(double x)
    {
        // Approximation using Abramowitz and Stegun formula
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x / 2);

        return 0.5 * (1.0 + sign * y);
    }

    #endregion

    #region Put-Call Parity

    /// <summary>
    /// Calculates the put price from call price using put-call parity.
    /// </summary>
    public static decimal PutFromCallParity(decimal callPrice, decimal s, decimal k, decimal t, decimal r)
    {
        return callPrice - s + k * (decimal)Math.Exp((double)(-r * t));
    }

    /// <summary>
    /// Calculates the call price from put price using put-call parity.
    /// </summary>
    public static decimal CallFromPutParity(decimal putPrice, decimal s, decimal k, decimal t, decimal r)
    {
        return putPrice + s - k * (decimal)Math.Exp((double)(-r * t));
    }

    /// <summary>
    /// Checks if put-call parity holds (within tolerance).
    /// </summary>
    public static bool CheckPutCallParity(decimal callPrice, decimal putPrice, decimal s, decimal k, decimal t, decimal r, decimal tolerance = 0.01m)
    {
        var expectedPut = PutFromCallParity(callPrice, s, k, t, r);
        return Math.Abs(expectedPut - putPrice) <= tolerance * s;
    }

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Calculates option price and Greeks in one call.
    /// </summary>
    public static (decimal Price, OptionGreeks Greeks) PriceAndGreeks(
        OptionType type, decimal s, decimal k, decimal t, decimal r, decimal sigma)
    {
        var price = OptionPrice(type, s, k, t, r, sigma);
        var greeks = CalculateGreeks(type, s, k, t, r, sigma);
        return (price, greeks);
    }

    /// <summary>
    /// Calculates time to expiration in years from days.
    /// </summary>
    public static decimal DaysToYears(int days) => days / 365m;

    /// <summary>
    /// Calculates time to expiration in years from a date.
    /// </summary>
    public static decimal TimeToExpiration(DateTime expirationDate)
    {
        var days = (expirationDate.Date - DateTime.UtcNow.Date).Days;
        return Math.Max(0, days) / 365m;
    }

    #endregion
}

/// <summary>
/// Binomial option pricing model for American options.
/// </summary>
public static class BinomialModel
{
    /// <summary>
    /// Calculates option price using Cox-Ross-Rubinstein binomial tree.
    /// </summary>
    /// <param name="type">Option type.</param>
    /// <param name="s">Current stock price.</param>
    /// <param name="k">Strike price.</param>
    /// <param name="t">Time to expiration in years.</param>
    /// <param name="r">Risk-free interest rate.</param>
    /// <param name="sigma">Volatility.</param>
    /// <param name="steps">Number of time steps.</param>
    /// <param name="isAmerican">Whether to price as American (with early exercise).</param>
    /// <returns>The option price.</returns>
    public static decimal OptionPrice(
        OptionType type,
        decimal s,
        decimal k,
        decimal t,
        decimal r,
        decimal sigma,
        int steps = 100,
        bool isAmerican = true)
    {
        if (t <= 0) return Math.Max(0, type == OptionType.Call ? s - k : k - s);
        if (steps < 1) steps = 1;

        var dt = t / steps;
        var u = (decimal)Math.Exp((double)(sigma * (decimal)Math.Sqrt((double)dt)));
        var d = 1m / u;
        var disc = (decimal)Math.Exp((double)(-r * dt));
        var p = ((decimal)Math.Exp((double)(r * dt)) - d) / (u - d);

        // Initialize asset prices at maturity
        var prices = new decimal[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            prices[i] = s * (decimal)Math.Pow((double)u, steps - i) * (decimal)Math.Pow((double)d, i);
        }

        // Initialize option values at maturity
        var values = new decimal[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            values[i] = type == OptionType.Call
                ? Math.Max(0, prices[i] - k)
                : Math.Max(0, k - prices[i]);
        }

        // Work backwards through the tree
        for (var j = steps - 1; j >= 0; j--)
        {
            for (var i = 0; i <= j; i++)
            {
                prices[i] = prices[i] / u;
                var holdValue = disc * (p * values[i] + (1 - p) * values[i + 1]);

                if (isAmerican)
                {
                    var exerciseValue = type == OptionType.Call
                        ? Math.Max(0, prices[i] - k)
                        : Math.Max(0, k - prices[i]);
                    values[i] = Math.Max(holdValue, exerciseValue);
                }
                else
                {
                    values[i] = holdValue;
                }
            }
        }

        return values[0];
    }

    /// <summary>
    /// Calculates the early exercise premium for an American option.
    /// </summary>
    public static decimal EarlyExercisePremium(
        OptionType type, decimal s, decimal k, decimal t, decimal r, decimal sigma, int steps = 100)
    {
        var american = OptionPrice(type, s, k, t, r, sigma, steps, isAmerican: true);
        var european = OptionPrice(type, s, k, t, r, sigma, steps, isAmerican: false);
        return american - european;
    }
}
