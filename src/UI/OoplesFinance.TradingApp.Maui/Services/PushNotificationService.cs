namespace OoplesFinance.TradingApp.Maui.Services;

/// <summary>
/// Cross-platform push notification service.
/// Handles Firebase Cloud Messaging (Android) and APNs (iOS).
/// </summary>
public sealed class PushNotificationService : INotificationService
{
    private readonly IPreferences _preferences;
    private string? _deviceToken;

    /// <summary>
    /// Event raised when a push notification is received.
    /// </summary>
    public event EventHandler<PushNotificationReceivedEventArgs>? NotificationReceived;

    /// <summary>
    /// Event raised when device token is updated.
    /// </summary>
    public event EventHandler<string>? TokenUpdated;

    /// <summary>
    /// Gets the current device token for push notifications.
    /// </summary>
    public string? DeviceToken => _deviceToken;

    public PushNotificationService()
    {
        _preferences = Preferences.Default;
        _deviceToken = _preferences.Get("push_token", string.Empty);
    }

    /// <summary>
    /// Requests permission for push notifications.
    /// </summary>
    public async Task RequestPermissionAsync()
    {
#if ANDROID
        await RequestAndroidPermissionAsync();
#elif IOS
        await RequestiOSPermissionAsync();
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Registers the device for push notifications.
    /// </summary>
    public async Task<string?> RegisterAsync()
    {
#if ANDROID
        return await RegisterAndroidAsync();
#elif IOS
        return await RegisteriOSAsync();
#else
        return null;
#endif
    }

    /// <summary>
    /// Unregisters the device from push notifications.
    /// </summary>
    public async Task UnregisterAsync()
    {
        _deviceToken = null;
        _preferences.Remove("push_token");

#if ANDROID
        await UnregisterAndroidAsync();
#elif IOS
        await UnregisteriOSAsync();
#endif
    }

    /// <summary>
    /// Shows a local notification.
    /// </summary>
    public async Task ShowLocalNotificationAsync(string title, string message)
    {
        await ShowLocalNotificationAsync(title, message, null);
    }

    /// <summary>
    /// Shows a local notification with optional data.
    /// </summary>
    public async Task ShowLocalNotificationAsync(string title, string message, Dictionary<string, string>? data)
    {
#if ANDROID
        await ShowAndroidNotificationAsync(title, message, data);
#elif IOS
        await ShowiOSNotificationAsync(title, message, data);
#else
        // Desktop fallback - use toast or alert
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Application.Current!.MainPage!.DisplayAlert(title, message, "OK");
        });
#endif
    }

    /// <summary>
    /// Schedules a local notification for a future time.
    /// </summary>
    public async Task ScheduleNotificationAsync(string title, string message, DateTime scheduledTime)
    {
        await ScheduleNotificationAsync(title, message, scheduledTime, null);
    }

    /// <summary>
    /// Schedules a local notification with optional data.
    /// </summary>
    public async Task ScheduleNotificationAsync(string title, string message, DateTime scheduledTime, Dictionary<string, string>? data)
    {
        var delay = scheduledTime - DateTime.Now;
        if (delay <= TimeSpan.Zero)
        {
            await ShowLocalNotificationAsync(title, message, data);
            return;
        }

#if ANDROID || IOS
        // Use platform-specific scheduling
        await SchedulePlatformNotificationAsync(title, message, scheduledTime, data);
#else
        // Desktop fallback - use timer
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            await ShowLocalNotificationAsync(title, message, data);
        });
#endif
    }

    /// <summary>
    /// Cancels all scheduled notifications.
    /// </summary>
    public Task CancelAllScheduledNotificationsAsync()
    {
#if ANDROID
        return CancelAndroidNotificationsAsync();
#elif IOS
        return CanceliOSNotificationsAsync();
#else
        return Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Handles incoming push notification.
    /// </summary>
    public void HandleNotification(string title, string message, Dictionary<string, string>? data)
    {
        NotificationReceived?.Invoke(this, new PushNotificationReceivedEventArgs(title, message, data));

        // Handle specific notification types
        if (data?.TryGetValue("type", out var notificationType) == true)
        {
            switch (notificationType)
            {
                case "order_fill":
                    HandleOrderFillNotification(data);
                    break;
                case "price_alert":
                    HandlePriceAlertNotification(data);
                    break;
                case "strategy_signal":
                    HandleStrategySignalNotification(data);
                    break;
                case "risk_warning":
                    HandleRiskWarningNotification(data);
                    break;
            }
        }
    }

    /// <summary>
    /// Updates the device token.
    /// </summary>
    public void UpdateToken(string token)
    {
        _deviceToken = token;
        _preferences.Set("push_token", token);
        TokenUpdated?.Invoke(this, token);
    }

    #region Notification Handlers

    private void HandleOrderFillNotification(Dictionary<string, string> data)
    {
        // Navigate to order history or show order details
        if (data.TryGetValue("order_id", out var orderId))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync($"orderHistory?highlight={orderId}");
            });
        }
    }

    private void HandlePriceAlertNotification(Dictionary<string, string> data)
    {
        // Navigate to alerts page or show alert details
        if (data.TryGetValue("symbol", out var symbol))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync($"chart?symbol={symbol}");
            });
        }
    }

    private void HandleStrategySignalNotification(Dictionary<string, string> data)
    {
        // Navigate to strategy monitor
        if (data.TryGetValue("strategy_id", out var strategyId))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync($"strategyMonitor?highlight={strategyId}");
            });
        }
    }

    private void HandleRiskWarningNotification(Dictionary<string, string> data)
    {
        // Show risk warning dialog
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var message = data.TryGetValue("message", out var msg) ? msg : "A risk warning has been triggered.";
            await Application.Current!.MainPage!.DisplayAlert("Risk Warning", message, "View Details", "Dismiss");
        });
    }

    #endregion

    #region Platform-Specific Implementations

#if ANDROID
    private Task RequestAndroidPermissionAsync()
    {
        // Android 13+ requires POST_NOTIFICATIONS permission
        // This would use Permissions API
        return Task.CompletedTask;
    }

    private Task<string?> RegisterAndroidAsync()
    {
        // Register with Firebase Cloud Messaging
        // FirebaseMessaging.Instance.Token.AddOnCompleteListener(...)
        return Task.FromResult<string?>(null);
    }

    private Task UnregisterAndroidAsync()
    {
        // Unregister from FCM
        return Task.CompletedTask;
    }

    private Task ShowAndroidNotificationAsync(string title, string message, Dictionary<string, string>? data)
    {
        // Use Android NotificationManager
        // Create notification channel, build notification, show
        return Task.CompletedTask;
    }

    private Task SchedulePlatformNotificationAsync(string title, string message, DateTime scheduledTime, Dictionary<string, string>? data)
    {
        // Use AlarmManager for scheduling
        return Task.CompletedTask;
    }

    private Task CancelAndroidNotificationsAsync()
    {
        // Cancel all pending alarms
        return Task.CompletedTask;
    }
#endif

#if IOS
    private async Task RequestiOSPermissionAsync()
    {
        // Request UNUserNotificationCenter authorization
        // UNUserNotificationCenter.Current.RequestAuthorization(...)
        await Task.CompletedTask;
    }

    private Task<string?> RegisteriOSAsync()
    {
        // Register for remote notifications
        // UIApplication.SharedApplication.RegisterForRemoteNotifications()
        return Task.FromResult<string?>(null);
    }

    private Task UnregisteriOSAsync()
    {
        // Unregister
        return Task.CompletedTask;
    }

    private Task ShowiOSNotificationAsync(string title, string message, Dictionary<string, string>? data)
    {
        // Use UNUserNotificationCenter
        return Task.CompletedTask;
    }

    private Task SchedulePlatformNotificationAsync(string title, string message, DateTime scheduledTime, Dictionary<string, string>? data)
    {
        // Use UNNotificationRequest with UNCalendarNotificationTrigger
        return Task.CompletedTask;
    }

    private Task CanceliOSNotificationsAsync()
    {
        // UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests()
        return Task.CompletedTask;
    }
#endif

    #endregion
}

/// <summary>
/// Event args for push notification received events.
/// </summary>
public sealed class PushNotificationReceivedEventArgs : EventArgs
{
    public string Title { get; }
    public string Message { get; }
    public Dictionary<string, string>? Data { get; }

    public PushNotificationReceivedEventArgs(string title, string message, Dictionary<string, string>? data)
    {
        Title = title;
        Message = message;
        Data = data;
    }
}

/// <summary>
/// Notification types for categorization.
/// </summary>
public static class NotificationTypes
{
    public const string OrderFill = "order_fill";
    public const string PriceAlert = "price_alert";
    public const string StrategySignal = "strategy_signal";
    public const string RiskWarning = "risk_warning";
    public const string SystemMessage = "system_message";
}

/// <summary>
/// Helper class for creating trading-specific notifications.
/// </summary>
public static class TradingNotifications
{
    /// <summary>
    /// Creates an order fill notification.
    /// </summary>
    public static (string title, string message, Dictionary<string, string> data) OrderFilled(
        string symbol, string side, decimal quantity, decimal price, string orderId)
    {
        var title = $"Order Filled: {symbol}";
        var message = $"{side.ToUpperInvariant()} {quantity} shares at ${price:F2}";
        var data = new Dictionary<string, string>
        {
            ["type"] = NotificationTypes.OrderFill,
            ["order_id"] = orderId,
            ["symbol"] = symbol,
            ["side"] = side,
            ["quantity"] = quantity.ToString(),
            ["price"] = price.ToString()
        };
        return (title, message, data);
    }

    /// <summary>
    /// Creates a price alert notification.
    /// </summary>
    public static (string title, string message, Dictionary<string, string> data) PriceAlertTriggered(
        string symbol, string condition, decimal targetPrice, decimal currentPrice, string alertId)
    {
        var title = $"Price Alert: {symbol}";
        var message = $"{symbol} is now {condition} ${targetPrice:F2} (Current: ${currentPrice:F2})";
        var data = new Dictionary<string, string>
        {
            ["type"] = NotificationTypes.PriceAlert,
            ["alert_id"] = alertId,
            ["symbol"] = symbol,
            ["condition"] = condition,
            ["target_price"] = targetPrice.ToString(),
            ["current_price"] = currentPrice.ToString()
        };
        return (title, message, data);
    }

    /// <summary>
    /// Creates a strategy signal notification.
    /// </summary>
    public static (string title, string message, Dictionary<string, string> data) StrategySignal(
        string strategyName, string symbol, string signal, string strategyId)
    {
        var title = $"Strategy Signal: {strategyName}";
        var message = $"{signal.ToUpperInvariant()} signal for {symbol}";
        var data = new Dictionary<string, string>
        {
            ["type"] = NotificationTypes.StrategySignal,
            ["strategy_id"] = strategyId,
            ["strategy_name"] = strategyName,
            ["symbol"] = symbol,
            ["signal"] = signal
        };
        return (title, message, data);
    }

    /// <summary>
    /// Creates a risk warning notification.
    /// </summary>
    public static (string title, string message, Dictionary<string, string> data) RiskWarning(
        string warningType, string message, decimal? threshold = null, decimal? currentValue = null)
    {
        var title = $"Risk Warning: {warningType}";
        var data = new Dictionary<string, string>
        {
            ["type"] = NotificationTypes.RiskWarning,
            ["warning_type"] = warningType,
            ["message"] = message
        };

        if (threshold.HasValue)
            data["threshold"] = threshold.Value.ToString();
        if (currentValue.HasValue)
            data["current_value"] = currentValue.Value.ToString();

        return (title, message, data);
    }
}
