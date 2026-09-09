using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class BrokerConnectionPage : ContentPage
{
    private readonly BrokerConnectionViewModel _viewModel;

    // Parameterless constructor for Shell navigation
    public BrokerConnectionPage() : this(
        App.Current?.Handler?.MauiContext?.Services.GetService<BrokerConnectionViewModel>()
        ?? new BrokerConnectionViewModel(new Services.BrokerConnectionService()))
    {
        App.LogError("BrokerConnectionPage", "Parameterless constructor called");
    }

    public BrokerConnectionPage(BrokerConnectionViewModel viewModel)
    {
        App.LogError("BrokerConnectionPage", "Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            App.LogError("BrokerConnectionPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("BrokerConnectionPage.Constructor", ex);
            throw;
        }
    }

    protected override void OnAppearing()
    {
        App.LogError("BrokerConnectionPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();
            _viewModel.Initialize();
            App.LogError("BrokerConnectionPage.OnAppearing", "Completed successfully");
        }
        catch (Exception ex)
        {
            App.LogException("BrokerConnectionPage.OnAppearing", ex);
        }
    }
}
