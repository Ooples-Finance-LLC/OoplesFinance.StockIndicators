using System.Globalization;

namespace OoplesFinance.StrategyBuilder.Maui.Converters;

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InvertedBoolConverter : IValueConverter
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
/// Converts trade direction to appropriate color.
/// </summary>
public class DirectionColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string direction)
        {
            return direction.ToUpperInvariant() switch
            {
                "LONG" or "BUY" => Color.FromArgb("#4EC9B0"),  // Green
                "SHORT" or "SELL" => Color.FromArgb("#F14C4C"), // Red
                _ => Colors.White
            };
        }
        return Colors.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts profit/loss value to appropriate color.
/// </summary>
public class ProfitLossColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
        {
            return decimalValue >= 0
                ? Color.FromArgb("#4EC9B0")  // Green for profit
                : Color.FromArgb("#F14C4C"); // Red for loss
        }
        if (value is double doubleValue)
        {
            return doubleValue >= 0
                ? Color.FromArgb("#4EC9B0")
                : Color.FromArgb("#F14C4C");
        }
        return Colors.White;
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
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to visibility (null = collapsed, not null = visible).
/// </summary>
public class NullToVisibilityConverter : IValueConverter
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
/// Converts node type string to appropriate color.
/// </summary>
public class NodeTypeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string nodeType)
        {
            return nodeType.ToLowerInvariant() switch
            {
                "indicator" => Color.FromArgb("#264F78"),
                "signal" => Color.FromArgb("#4B2F4B"),
                "action" => Color.FromArgb("#2F4B3E"),
                "datasource" => Color.FromArgb("#4B3E2F"),
                _ => Color.FromArgb("#2D2D30")
            };
        }
        return Color.FromArgb("#2D2D30");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a percentage value to a formatted string.
/// </summary>
public class PercentageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
            return decimalValue.ToString("P2", culture);
        if (value is double doubleValue)
            return doubleValue.ToString("P2", culture);
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a currency value to a formatted string.
/// </summary>
public class CurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
            return decimalValue.ToString("C2", culture);
        if (value is double doubleValue)
            return doubleValue.ToString("C2", culture);
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
