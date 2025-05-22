using Microsoft.Maui.Controls;

namespace AviationApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell(); // Use AppShell instead of MainPage
    }

    //protected override Window CreateWindow(IActivationState? activationState)
    //{
    //    return new Window(new MainPage());
    //}
}