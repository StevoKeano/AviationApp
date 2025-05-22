using Android.Content;
using System.Diagnostics;

namespace AviationApp;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage()
	{
		InitializeComponent();
	}

	private async void OnCounterClicked(object sender, EventArgs e)
	{
        Debug.WriteLine("MainPage: OnCounterClicked started");
        await StartLocationService();
        Debug.WriteLine("MainPage: OnCounterClicked completed");
        count++;

		if (count == 1)
			CounterBtn.Text = $"Clicked {count} time";
		else
			CounterBtn.Text = $"Clicked {count} times";

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
    //private async Task StartLocationService()
    //{
    //    var intent = new Intent(Android.App.Application.Context, typeof(AviationApp.Services.LocationService));
    //    Android.App.Application.Context.StartForegroundService(intent);
    //}
    private async Task StartLocationService()
    {
        Debug.WriteLine("MainPage: StartLocationService started");
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            Debug.WriteLine($"MainPage: Permission status: {status}");
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission Denied", "Location permission is required for tracking.", "OK");
                Debug.WriteLine("MainPage: Permission denied");
                return;
            }

            var context = Android.App.Application.Context;
            var startServiceIntent = new Intent(context, typeof(AviationApp.Services.LocationService)); // Line 28
            context.StartForegroundService(startServiceIntent); // Line 29
            Debug.WriteLine("MainPage: StartForegroundService called");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainPage: StartLocationService failed: {ex}");
            await DisplayAlert("Error", $"Failed to start service: {ex.Message}", "OK");
        }
    }

    private void StopLocationService()
    {
        Debug.WriteLine("MainPage: StopLocationService started");
        try
        {
            var context = Android.App.Application.Context;
            var stopServiceIntent = new Intent(context, typeof(AviationApp.Services.LocationService)); // Line 35
            context.StopService(stopServiceIntent); // Line 36
            Debug.WriteLine("MainPage: StopService called");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainPage: StopLocationService failed: {ex}");
        }
    }
}

