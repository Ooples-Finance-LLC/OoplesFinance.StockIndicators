using OoplesFinance.StockIndicators.Builder.Notifications;

namespace OoplesFinance.StockIndicators.Builder.Catalogs;

/// <summary>
/// Catalog of notification channels.
/// </summary>
public sealed class NotificationCatalog
{
    private readonly List<INotificationChannel> _channels = new();

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
    /// Adds a custom notification channel.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when channel is null.</exception>
    public NotificationCatalog AddChannel(INotificationChannel channel)
    {
        if (channel is null) throw new ArgumentNullException(nameof(channel));
        _channels.Add(channel);
        return this;
    }

    internal IReadOnlyList<INotificationChannel> Build()
    {
        // Return singleton empty array when no channels configured to avoid allocation
        return _channels.Count == 0 ? Array.Empty<INotificationChannel>() : new List<INotificationChannel>(_channels);
    }
}
