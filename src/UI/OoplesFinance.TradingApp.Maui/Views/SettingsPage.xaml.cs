using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class SettingsPage : ContentPage
{
    private SettingsViewModel? _viewModel;

    public SettingsPage()
    {
        App.LogError("SettingsPage", "Constructor starting");
        try
        {
            InitializeComponent();
            App.LogError("SettingsPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("SettingsPage.Constructor", ex);
            throw;
        }
    }

    public SettingsPage(SettingsViewModel viewModel)
    {
        App.LogError("SettingsPage", "DI Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            App.LogError("SettingsPage", "DI Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("SettingsPage.DIConstructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("SettingsPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();

            if (BindingContext is null || _viewModel is null)
            {
                _viewModel = Application.Current?.Handler?.MauiContext?.Services.GetService<SettingsViewModel>();
                if (_viewModel is not null)
                {
                    BindingContext = _viewModel;
                    App.LogError("SettingsPage.OnAppearing", "Got ViewModel from DI");
                }
            }

            if (_viewModel is not null)
            {
                await _viewModel.LoadSettingsAsync();
                App.LogError("SettingsPage.OnAppearing", "Completed successfully");
            }
        }
        catch (Exception ex)
        {
            App.LogException("SettingsPage.OnAppearing", ex);
        }
    }
}
