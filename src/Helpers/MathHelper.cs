//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators.Helpers;

public static class MathHelper
{
    public const double Phi = 1.61803398874989484820458683436;
    public const double Sqrt2 = 1.4142135623730950488016887242;

    /// <summary>
    /// Logs the specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public static double Log(double value)
    {
        return value > 0 ? Math.Log(value) : 0;
    }

    /// <summary>
    /// SQRTs the specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public static double Sqrt(double value)
    {
        return value < 0 ? 0 : Math.Sqrt(value);
    }

    /// <summary>
    /// Convert to Degrees From Radians
    /// </summary>
    /// <param name="val">The value to convert to degrees</param>
    /// <returns>The value in degrees</returns>
    public static double ToDegrees(this double radianValue)
    {
        return 180 / Math.PI * radianValue;
    }

    /// <summary>
    /// Convert to Radians From Degrees
    /// </summary>
    /// <param name="val">The value to convert to radians</param>
    /// <returns>The value in radians</returns>
    public static double ToRadians(this double degreeValue)
    {
        return Math.PI / 180 * degreeValue;
    }

    /// <summary>
    /// Calculates the value raised to a certain power
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="power">The power.</param>
    /// <returns></returns>
    public static double Pow(double value, double power)
    {
        double result;

        try
        {
            result = Math.Pow(value, power);
        }
        catch (OverflowException)
        {
            result = double.MaxValue;
        }

        return result;
    }

    /// <summary>
    /// Calculates E raised to a certain power
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public static double Exp(double value)
    {
        double result;

        try
        {
            result = Math.Exp(Math.Min(100, value));
        }
        catch (OverflowException)
        {
            result = double.MaxValue - 1;
        }
        catch (Exception)
        {
            throw;
        }

        return result;
    }

    /// <summary>
    /// Determines whether [is value null or infinity] [the specified value].
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    ///   <c>true</c> if [is value null or infinity] [the specified value]; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsValueNullOrInfinity(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value);
    }

    /// <summary>
    /// Minimums the or maximum.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="maxValue">The maximum value.</param>
    /// <param name="minValue">The minimum value.</param>
    /// <returns></returns>
    public static double MinOrMax(double value, double maxValue, double minValue)
    {
        return Math.Min(Math.Max(value, minValue), maxValue);
    }

    /// <summary>
    /// Minimums the or maximum.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public static int MinOrMax(int value)
    {
        var result = double.IsNaN(value) ? 0 : double.IsPositiveInfinity(value) ? int.MaxValue : double.IsNegativeInfinity(value) ? int.MinValue : value;
        return Math.Min(Math.Max(result, 2), 530);
    }
}
