using OoplesFinance.TradingApp.Maui.Models;

namespace OoplesFinance.TradingApp.Maui.Services;

/// <summary>
/// Service for managing portfolio and positions.
/// </summary>
public interface IPortfolioService
{
    Task<AccountInfo> GetAccountAsync();
    Task<IReadOnlyList<Position>> GetPositionsAsync();
    Task<Position?> GetPositionAsync(string symbol);
    Task RefreshAsync();
}

/// <summary>
/// Service for order management.
/// </summary>
public interface IOrderService
{
    event EventHandler<Order>? OrderUpdated;

    Task<Order> SubmitOrderAsync(OrderRequest request);
    Task<bool> CancelOrderAsync(string orderId);
    Task<Order?> GetOrderAsync(string orderId);
    Task<IReadOnlyList<Order>> GetOpenOrdersAsync();
    Task<IReadOnlyList<Order>> GetOrderHistoryAsync(DateTime? from = null, DateTime? to = null);
}

/// <summary>
/// Service for market data.
/// </summary>
public interface IMarketDataService
{
    Task<Quote?> GetQuoteAsync(string symbol);
    Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols);
    Task<IReadOnlyList<Bar>> GetHistoricalBarsAsync(string symbol, DateTime start, DateTime end, BarInterval interval);
    Task<MarketStatus> GetMarketStatusAsync();
    Task SubscribeToQuotesAsync(IEnumerable<string> symbols, Action<Quote> onQuote);
    Task UnsubscribeFromQuotesAsync(IEnumerable<string> symbols);
}

/// <summary>
/// Service for price alerts.
/// </summary>
public interface IAlertService
{
    event EventHandler<Alert>? AlertTriggered;

    Task<string> CreateAlertAsync(string symbol, string condition, decimal targetPrice);
    Task DeleteAlertAsync(string alertId);
    Task<IReadOnlyList<Alert>> GetAllAlertsAsync();
    Task<IReadOnlyList<Alert>> GetActiveAlertsAsync();
    Task UpdateAlertAsync(string alertId, bool isActive);
}

/// <summary>
/// Service for user settings.
/// </summary>
public interface ISettingsService
{
    Task<IReadOnlyList<string>> GetWatchlistAsync();
    Task AddToWatchlistAsync(string symbol);
    Task RemoveFromWatchlistAsync(string symbol);
    Task<T?> GetSettingAsync<T>(string key);
    Task SetSettingAsync<T>(string key, T value);
}

/// <summary>
/// Service for notifications.
/// </summary>
public interface INotificationService
{
    Task ShowLocalNotificationAsync(string title, string message);
    Task ShowLocalNotificationAsync(string title, string message, Dictionary<string, string>? data);
    Task RequestPermissionAsync();
    Task<string?> RegisterAsync();
    Task UnregisterAsync();
    Task ScheduleNotificationAsync(string title, string message, DateTime scheduledTime);
    Task ScheduleNotificationAsync(string title, string message, DateTime scheduledTime, Dictionary<string, string>? data);
    Task CancelAllScheduledNotificationsAsync();
}

/// <summary>
/// Service for authentication.
/// </summary>
public interface IAuthService
{
    event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? Email { get; }

    Task<AuthResult> SignInAsync(string email, string password);
    Task<AuthResult> SignUpAsync(string email, string password);
    Task SignOutAsync();
    Task<AuthResult> RefreshSessionAsync();
    string GetOAuthSignInUrl(string provider, string redirectUrl);
    Task<AuthResult> HandleOAuthCallbackAsync(string code);
}

/// <summary>
/// Service for strategy management.
/// </summary>
public interface IStrategyService
{
    Task<IReadOnlyList<Strategy>> GetStrategiesAsync();
    Task<Strategy?> GetStrategyAsync(string strategyId);
    Task<string> SaveStrategyAsync(Strategy strategy);
    Task DeleteStrategyAsync(string strategyId);
    Task<IReadOnlyList<BacktestResult>> GetBacktestResultsAsync(string strategyId);
    Task<BacktestResult> RunBacktestAsync(string strategyId, DateTime startDate, DateTime endDate);
}

/// <summary>
/// Event args for auth state changes.
/// </summary>
public sealed class AuthStateChangedEventArgs : EventArgs
{
    public bool IsAuthenticated { get; }
    public string? UserId { get; }
    public string? Email { get; }

    public AuthStateChangedEventArgs(bool isAuthenticated, string? userId = null, string? email = null)
    {
        IsAuthenticated = isAuthenticated;
        UserId = userId;
        Email = email;
    }
}

/// <summary>
/// Result of authentication operations.
/// </summary>
public sealed class AuthResult
{
    public bool Success { get; init; }
    public string? UserId { get; init; }
    public string? Email { get; init; }
    public string? ErrorMessage { get; init; }

    public static AuthResult Succeeded(string userId, string email) =>
        new() { Success = true, UserId = userId, Email = email };

    public static AuthResult Failed(string error) =>
        new() { Success = false, ErrorMessage = error };
}
