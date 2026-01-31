using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using OoplesFinance.StrategyBuilder.Maui.ViewModels;
using OoplesFinance.StrategyBuilder.Maui.Views;

namespace OoplesFinance.StrategyBuilder.Maui;

/// <summary>
/// MAUI application entry point and dependency injection configuration.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services
        builder.Services.AddSingleton<IStrategyService, StrategyService>();
        builder.Services.AddSingleton<IIndicatorLibraryService, IndicatorLibraryService>();
        builder.Services.AddSingleton<INodeConnectionService, NodeConnectionService>();
        builder.Services.AddSingleton<ICodeGenerationService, CodeGenerationService>();
        builder.Services.AddSingleton<IBacktestService, BacktestService>();

        // Register ViewModels
        builder.Services.AddTransient<StrategyCanvasViewModel>();
        builder.Services.AddTransient<IndicatorLibraryViewModel>();
        builder.Services.AddTransient<NodePropertiesViewModel>();
        builder.Services.AddTransient<ToolbarViewModel>();
        builder.Services.AddTransient<BacktestResultsViewModel>();

        // Register Views
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<StrategyCanvasView>();
        builder.Services.AddTransient<IndicatorLibraryPanel>();
        builder.Services.AddTransient<NodePropertiesPanel>();
        builder.Services.AddTransient<BacktestResultsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
