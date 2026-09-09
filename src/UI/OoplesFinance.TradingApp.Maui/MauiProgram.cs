using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OoplesFinance.TradingApp.Maui.Helpers;
using OoplesFinance.TradingApp.Maui.Services;
using OoplesFinance.TradingApp.Maui.ViewModels;
using OoplesFinance.TradingApp.Maui.Views;
using OoplesFinance.TradingApp.Maui.Views.Onboarding;
using OoplesFinance.StockIndicators.Builder.Cloud;
using OoplesFinance.StockIndicators.Builder.MarketData;
using OoplesFinance.StockIndicators.Builder.Trading;
using SkiaSharp.Views.Maui.Controls.Hosting;
using LiveChartsCore.SkiaSharpView.Maui;
using System.Reflection;
using System.Text.Json;

namespace OoplesFinance.TradingApp.Maui;

/// <summary>
/// Application settings loaded from appsettings.local.json or environment variables
/// </summary>
public class AppSettings
{
    public SupabaseSettings Supabase { get; set; } = new();
    public AlpacaSettings Alpaca { get; set; } = new();
    public RedisSettings Redis { get; set; } = new();
    public FeatureSettings Features { get; set; } = new();
    public NotificationSettingsConfig Notifications { get; set; } = new();

    public class SupabaseSettings
    {
        public string Url { get; set; } = "https://xueswywycjwmsuyhcgop.supabase.co";
        public string AnonKey { get; set; } = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inh1ZXN3eXd5Y2p3bXN1eWhjZ29wIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Njk3NDM3MTMsImV4cCI6MjA4NTMxOTcxM30.b84t8ufZEIexGUdtafYhbK5NzxJ2qEBGpNHMMy9kLfg";
    }

    public class AlpacaSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public bool UsePaper { get; set; } = true;
    }

    public class RedisSettings
    {
        public string ConnectionString { get; set; } = "localhost:6379";
        public bool Enabled { get; set; } = false;
    }

    public class FeatureSettings
    {
        // Default to mock services for development - set to true when API keys are configured
        public bool UseRealBackend { get; set; } = false;
        public bool UseRedisCache { get; set; } = false;
    }

    public class NotificationSettingsConfig
    {
        public bool EnablePush { get; set; } = true;
        public bool EnableTelegram { get; set; } = false;
        public bool EnableDiscord { get; set; } = false;
        public bool EnableSms { get; set; } = false;
        public bool EnableEmail { get; set; } = false;
        public TelegramConfig Telegram { get; set; } = new();
        public DiscordConfig Discord { get; set; } = new();
        public TwilioConfig Twilio { get; set; } = new();
        public SendGridConfig SendGrid { get; set; } = new();
        public UserConfig User { get; set; } = new();

        public class TelegramConfig
        {
            public string BotToken { get; set; } = string.Empty;
            public string ChatId { get; set; } = string.Empty;
        }

        public class DiscordConfig
        {
            public string WebhookUrl { get; set; } = string.Empty;
        }

        public class TwilioConfig
        {
            public string AccountSid { get; set; } = string.Empty;
            public string AuthToken { get; set; } = string.Empty;
            public string FromNumber { get; set; } = string.Empty;
        }

        public class SendGridConfig
        {
            public string ApiKey { get; set; } = string.Empty;
            public string FromEmail { get; set; } = "noreply@ooplesfinance.com";
        }

        public class UserConfig
        {
            public string Email { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
        }
    }

    /// <summary>
    /// Load settings from appsettings.local.json or use defaults with environment variable overrides
    /// </summary>
    public static AppSettings Load()
    {
        var settings = new AppSettings();

        // Try to load from appsettings.local.json
        var localSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
        if (File.Exists(localSettingsPath))
        {
            try
            {
                var json = File.ReadAllText(localSettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (loaded is not null)
                    settings = loaded;
            }
            catch
            {
                // Ignore parsing errors, use defaults
            }
        }

        // Environment variable overrides (highest priority)
        settings.Supabase.Url = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? settings.Supabase.Url;
        settings.Supabase.AnonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? settings.Supabase.AnonKey;
        settings.Alpaca.ApiKey = Environment.GetEnvironmentVariable("ALPACA_API_KEY") ?? settings.Alpaca.ApiKey;
        settings.Alpaca.ApiSecret = Environment.GetEnvironmentVariable("ALPACA_API_SECRET") ?? settings.Alpaca.ApiSecret;
        settings.Redis.ConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? settings.Redis.ConnectionString;

        return settings;
    }
}

/// <summary>
/// MAUI application entry point for the Ooples Trading App.
/// Supports iOS, Android, macOS, and Windows.
/// </summary>
public static class MauiProgram
{
    // Load settings from appsettings.local.json or environment variables
    private static readonly AppSettings Settings = AppSettings.Load();

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseLiveCharts()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

        // Register AppSettings as singleton
        builder.Services.AddSingleton(Settings);

        // Register Supabase Client (always available for auth)
        builder.Services.AddSingleton(sp => new SupabaseClient(new SupabaseOptions
        {
            Url = Settings.Supabase.Url,
            AnonKey = Settings.Supabase.AnonKey
        }));

        // Register Cache Service (Redis or in-memory fallback)
        if (Settings.Features.UseRedisCache && Settings.Redis.Enabled)
        {
            builder.Services.AddSingleton<ICacheService>(sp => new RedisCacheService(Settings.Redis.ConnectionString));
        }
        else
        {
            builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        // Register Broker Factory
        builder.Services.AddSingleton<IBrokerFactory, BrokerFactory>();

        // Register Adaptive UI Service (skill-level based UI)
        builder.Services.AddSingleton<IAdaptiveUIService, AdaptiveUIService>();

        // Register Core Services (available for both real and mock backends)
        builder.Services.AddSingleton<RateLimiter>();
        builder.Services.AddSingleton(sp => new RealtimeService(Settings.Supabase.Url, Settings.Supabase.AnonKey));
        builder.Services.AddSingleton<ErrorHandler>(sp =>
        {
            // Use MAUI's connectivity service
            return new ErrorHandler(Connectivity.Current);
        });

        // Register Notification Settings
        var notificationSettings = new NotificationSettings
        {
            EnablePush = Settings.Notifications.EnablePush,
            EnableTelegram = Settings.Notifications.EnableTelegram,
            EnableDiscord = Settings.Notifications.EnableDiscord,
            EnableSms = Settings.Notifications.EnableSms,
            EnableEmail = Settings.Notifications.EnableEmail,
            TelegramBotToken = Settings.Notifications.Telegram.BotToken,
            TelegramChatId = Settings.Notifications.Telegram.ChatId,
            DiscordWebhookUrl = Settings.Notifications.Discord.WebhookUrl,
            TwilioAccountSid = Settings.Notifications.Twilio.AccountSid,
            TwilioAuthToken = Settings.Notifications.Twilio.AuthToken,
            TwilioFromNumber = Settings.Notifications.Twilio.FromNumber,
            SendGridApiKey = Settings.Notifications.SendGrid.ApiKey,
            SendGridFromEmail = Settings.Notifications.SendGrid.FromEmail,
            UserEmail = Settings.Notifications.User.Email,
            UserPhoneNumber = Settings.Notifications.User.PhoneNumber
        };
        builder.Services.AddSingleton(notificationSettings);

        if (Settings.Features.UseRealBackend)
        {
            // Register Direct Alpaca Services (no Supabase dependency for POC)
            var alpacaOptions = new AlpacaOptions
            {
                ApiKey = Settings.Alpaca.ApiKey,
                ApiSecret = Settings.Alpaca.ApiSecret,
                UsePaper = Settings.Alpaca.UsePaper
            };

            builder.Services.AddSingleton<ISettingsService, SettingsService>(); // Settings still local
            builder.Services.AddSingleton<IBrokerConnectionService>(sp => new DirectAlpacaBrokerConnectionService(alpacaOptions));
            builder.Services.AddSingleton<IPortfolioService>(sp => new DirectAlpacaPortfolioService(alpacaOptions));
            builder.Services.AddSingleton<IMarketDataService>(sp => new DirectAlpacaMarketDataService(alpacaOptions));
            builder.Services.AddSingleton<IOrderService>(sp => new DirectAlpacaOrderService(alpacaOptions));
            builder.Services.AddSingleton<IAlertService, InMemoryAlertService>(); // In-memory for POC
            builder.Services.AddSingleton<INotificationService, NotificationService>(); // Platform-specific
            builder.Services.AddSingleton<IStrategyMonitorService, StrategyMonitorService>(); // TODO: Real implementation

            // Register Multi-Channel Notification Service
            builder.Services.AddSingleton<IMultiChannelNotificationService>(sp =>
            {
                var pushService = sp.GetRequiredService<INotificationService>();
                var settings = sp.GetRequiredService<NotificationSettings>();
                return new MultiChannelNotificationService(settings, pushService);
            });

            // Register Market Data Provider (Alpaca by default)
            builder.Services.AddSingleton<IMarketDataProvider>(sp =>
            {
                return new AlpacaMarketDataProvider(alpacaOptions);
            });
        }
        else
        {
            // Register Mock Services (for development/demo)
            builder.Services.AddSingleton<ISettingsService, SettingsService>();
            builder.Services.AddSingleton<IBrokerConnectionService, BrokerConnectionService>();
            builder.Services.AddSingleton<IPortfolioService, PortfolioService>();
            builder.Services.AddSingleton<IMarketDataService, MarketDataService>();
            builder.Services.AddSingleton<IOrderService, OrderService>();
            builder.Services.AddSingleton<IAlertService, AlertService>();
            builder.Services.AddSingleton<INotificationService, NotificationService>();
            builder.Services.AddSingleton<IStrategyMonitorService, StrategyMonitorService>();

            // Register Multi-Channel Notification Service
            builder.Services.AddSingleton<IMultiChannelNotificationService>(sp =>
            {
                var pushService = sp.GetRequiredService<INotificationService>();
                var settings = sp.GetRequiredService<NotificationSettings>();
                return new MultiChannelNotificationService(settings, pushService);
            });
        }

        // Register Indicator Service (v2 Builder API)
        builder.Services.AddSingleton<IIndicatorService, IndicatorService>();

        // Register AI Analysis Service (trading signals, regime detection, anomaly detection)
        builder.Services.AddSingleton<IAIAnalysisService>(sp =>
        {
            var indicatorService = sp.GetRequiredService<IIndicatorService>();
            return new AIAnalysisService(indicatorService);
        });

        // Register ViewModels
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<PositionsViewModel>();
        builder.Services.AddTransient<OrderEntryViewModel>();
        builder.Services.AddTransient<OrderHistoryViewModel>();
        builder.Services.AddTransient<AlertsViewModel>();
        builder.Services.AddTransient<WatchlistViewModel>();
        builder.Services.AddTransient<ChartViewModel>(sp =>
        {
            var marketDataService = sp.GetRequiredService<IMarketDataService>();
            var indicatorService = sp.GetRequiredService<IIndicatorService>();
            var aiService = sp.GetRequiredService<IAIAnalysisService>();
            return new ChartViewModel(marketDataService, indicatorService, aiService);
        });
        builder.Services.AddTransient<StrategyMonitorViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<BrokerConnectionViewModel>();
        builder.Services.AddTransient<PositionDetailViewModel>();

        // Auth ViewModels (for Supabase auth)
        builder.Services.AddTransient<AuthLoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<OrderConfirmationViewModel>();

        // Register Views
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<PositionsPage>();
        builder.Services.AddTransient<OrderEntryPage>();
        builder.Services.AddTransient<OrderHistoryPage>();
        builder.Services.AddTransient<AlertsPage>();
        builder.Services.AddTransient<WatchlistPage>();
        builder.Services.AddTransient<ChartPage>();
        builder.Services.AddTransient<StrategyMonitorPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<BrokerConnectionPage>();
        builder.Services.AddTransient<PositionDetailPage>();

        // Auth Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<OrderConfirmationPopup>();

        // Onboarding Pages
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<RiskQuizPage>();
        builder.Services.AddTransient<GoalSetupPage>();
        builder.Services.AddTransient<RiskSliderPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
