using OoplesFinance.StockIndicators.Builder.Notifications;

namespace OoplesFinance.StockIndicators.Builder.Catalogs;

/// <summary>
/// Catalog of notification channels.
/// </summary>
public sealed class NotificationCatalog
{
    private readonly List<INotificationChannel> _channels = new();
    private readonly List<NotificationRoute> _routes = new();
    private NotificationRouteBuilder? _currentRouteBuilder;

    /// <summary>
    /// Adds a console notification channel.
    /// </summary>
    public NotificationCatalog Console()
    {
        _channels.Add(new ConsoleNotificationChannel());
        return this;
    }

    /// <summary>
    /// Adds an email notification channel.
    /// </summary>
    public NotificationCatalog Email(EmailOptions options)
    {
        _channels.Add(new EmailNotificationChannel(options));
        return this;
    }

    /// <summary>
    /// Adds an SMS notification channel.
    /// </summary>
    public NotificationCatalog Sms(SmsOptions options)
    {
        _channels.Add(new SmsNotificationChannel(options));
        return this;
    }

    /// <summary>
    /// Adds a webhook notification channel.
    /// </summary>
    public NotificationCatalog Webhook(WebhookOptions options)
    {
        _channels.Add(new WebhookNotificationChannel(options));
        return this;
    }

    /// <summary>
    /// Adds a Telegram notification channel.
    /// </summary>
    public NotificationCatalog Telegram(TelegramOptions? options = null)
    {
        _channels.Add(new TelegramNotificationChannel(options ?? new TelegramOptions()));
        return this;
    }

    /// <summary>
    /// Adds a Discord notification channel.
    /// </summary>
    public NotificationCatalog Discord(DiscordOptions? options = null)
    {
        _channels.Add(new DiscordNotificationChannel(options ?? new DiscordOptions()));
        return this;
    }

    /// <summary>
    /// Adds a WebSocket notification channel.
    /// </summary>
    public NotificationCatalog WebSocket(WebSocketOptions options)
    {
        _channels.Add(new WebSocketNotificationChannel(options));
        return this;
    }

    /// <summary>
    /// Adds a WebSocket notification channel with a URI.
    /// </summary>
    public NotificationCatalog WebSocket(string uri)
    {
        return WebSocket(new WebSocketOptions { Uri = uri });
    }

    /// <summary>
    /// Adds a custom notification channel.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when channel is null.</exception>
    public NotificationCatalog AddChannel(INotificationChannel channel)
    {
        if (channel is null) throw new ArgumentNullException(nameof(channel));
        _channels.Add(channel);
        return this;
    }

    /// <summary>
    /// Starts a notification route for a specific signal name or pattern.
    /// Use with fluent methods like SendWebSocket(), SendEmail(), etc.
    /// Supports wildcards: "RSI*" matches any signal starting with "RSI".
    /// </summary>
    /// <param name="signalNameOrPattern">The signal name or pattern (supports * wildcard).</param>
    /// <returns>A route builder for configuring notification channels.</returns>
    /// <example>
    /// <code>
    /// builder.ConfigureNotifications(notify => {
    ///     notify.OnSignal("RSI Oversold Exit")
    ///           .SendWebSocket("ws://localhost:8080")
    ///           .SendEmail(emailOptions);
    /// });
    /// </code>
    /// </example>
    public NotificationRouteBuilder OnSignal(string signalNameOrPattern)
    {
        // Complete any previous route builder
        _currentRouteBuilder?.Complete();

        _currentRouteBuilder = new NotificationRouteBuilder(signalNameOrPattern, route =>
        {
            _routes.Add(route);
        });
        return _currentRouteBuilder;
    }

    /// <summary>
    /// Starts a notification route for a specific signal handle.
    /// </summary>
    /// <param name="signal">The signal handle.</param>
    /// <returns>A route builder for configuring notification channels.</returns>
    public NotificationRouteBuilder OnSignal(SignalHandle signal)
    {
        // Complete any previous route builder
        _currentRouteBuilder?.Complete();

        _currentRouteBuilder = new NotificationRouteBuilder(signal, route =>
        {
            _routes.Add(route);
        });
        return _currentRouteBuilder;
    }

    /// <summary>
    /// Configures notifications for all signals (wildcard).
    /// </summary>
    /// <returns>A route builder for configuring notification channels.</returns>
    public NotificationRouteBuilder OnAllSignals()
    {
        return OnSignal("*");
    }

    internal IReadOnlyList<INotificationChannel> Build()
    {
        // Complete any pending route builder
        _currentRouteBuilder?.Complete();
        _currentRouteBuilder = null;

        // Return singleton empty array when no channels configured to avoid allocation
        return _channels.Count == 0 ? Array.Empty<INotificationChannel>() : new List<INotificationChannel>(_channels);
    }

    internal IReadOnlyList<NotificationRoute> BuildRoutes()
    {
        // Complete any pending route builder
        _currentRouteBuilder?.Complete();
        _currentRouteBuilder = null;

        return _routes.Count == 0 ? Array.Empty<NotificationRoute>() : new List<NotificationRoute>(_routes);
    }
}
