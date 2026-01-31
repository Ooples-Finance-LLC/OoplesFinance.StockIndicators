using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
