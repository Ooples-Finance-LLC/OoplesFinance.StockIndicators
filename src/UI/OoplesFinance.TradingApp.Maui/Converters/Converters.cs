using System.Globalization;

namespace OoplesFinance.TradingApp.Maui.Converters;

/// <summary>
/// Converts a boolean to its inverse.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return value;
    }
}

/// <summary>
/// Converts a number to a color based on positive/negative value.
/// </summary>
public class PnLColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
        {
            return decimalValue >= 0
                ? Application.Current?.Resources["Success"]
                : Application.Current?.Resources["Error"];
        }
        if (value is double doubleValue)
        {
            return doubleValue >= 0
                ? Application.Current?.Resources["Success"]
                : Application.Current?.Resources["Error"];
        }
        return Application.Current?.Resources["TextMuted"];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean to visibility (true = visible, false = collapsed).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an order side to a color (Buy = green, Sell = red).
/// </summary>
public class OrderSideColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string side)
        {
            return side.Equals("buy", StringComparison.OrdinalIgnoreCase)
                ? Application.Current?.Resources["Buy"]
                : Application.Current?.Resources["Sell"];
        }
        return Application.Current?.Resources["TextMuted"];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an order status to a background color.
/// </summary>
public class OrderStatusBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLowerInvariant() switch
            {
                "filled" => Application.Current?.Resources["SuccessDark"],
                "pending" or "new" or "open" => Application.Current?.Resources["InfoDark"],
                "cancelled" or "canceled" => Application.Current?.Resources["Secondary"],
                "rejected" or "failed" => Application.Current?.Resources["ErrorDark"],
                _ => Application.Current?.Resources["Surface"]
            };
        }
        return Application.Current?.Resources["Surface"];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an order status to a text color.
/// </summary>
public class OrderStatusTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Application.Current?.Resources["TextPrimary"];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a count to an alert badge color.
/// </summary>
public class AlertCountColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count && count > 0)
            return Application.Current?.Resources["Error"];
        return Application.Current?.Resources["TextMuted"];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a strategy status to a background color.
/// </summary>
public class StrategyStatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLowerInvariant() switch
            {
                "running" => Application.Current?.Resources["SuccessDark"],
                "paused" => Application.Current?.Resources["WarningDark"],
                "stopped" or "error" => Application.Current?.Resources["ErrorDark"],
                _ => Application.Current?.Resources["Secondary"]
            };
        }
        return Application.Current?.Resources["Secondary"];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to false (for null-safety in bindings).
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a decimal value to a formatted currency string.
/// </summary>
public class CurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
            return decimalValue.ToString("C2", culture);
        if (value is double doubleValue)
            return doubleValue.ToString("C2", culture);
        return "$0.00";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a decimal percentage to a formatted percentage string.
/// </summary>
public class PercentageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
            return decimalValue.ToString("P2", culture);
        if (value is double doubleValue)
            return doubleValue.ToString("P2", culture);
        return "0.00%";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
