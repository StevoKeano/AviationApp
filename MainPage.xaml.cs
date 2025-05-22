using Android.Content;
using Android.Locations;
using Android.Util;
using AviationApp.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Dispatching;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AviationApp;

public partial class MainPage : ContentPage, INotifyPropertyChanged
{
    private int count = 0;
    private string latitudeText = "Latitude: N/A";
    private string longitudeText = "Longitude: N/A";
    private string altitudeText = "Altitude: N/A";
    private string speedText = "Speed: N/A";
    private string lastUpdateText = "Last Update: N/A";

    public string LatitudeText
    {
        get => latitudeText;
        set { latitudeText = value; OnPropertyChanged(); }
    }

    public string LongitudeText
    {
        get => longitudeText;
        set { longitudeText = value; OnPropertyChanged(); }
    }

    public string AltitudeText
    {
        get => altitudeText;
        set { altitudeText = value; OnPropertyChanged(); }
    }

    public string SpeedText
    {
        get => speedText;
        set { speedText = value; OnPropertyChanged(); }
    }

    public string LastUpdateText
    {
        get => lastUpdateText;
        set { lastUpdateText = value; OnPropertyChanged(); }
    }

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
        // Register with strong reference to ensure persistence
        WeakReferenceMessenger.Default.Register<LocationMessage>(this, (recipient, message) =>
        {
            try
            {
                Log.Debug("MainPage", "Received LocationMessage");
                var androidLocation = message.Location;
                var updateTime = message.UpdateTime;
                if (androidLocation == null)
                {
                    Log.Error("MainPage", "Received null location");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Location Error", "Unable to retrieve location data.", "OK");
                    });
                    return;
                }

                var location = new Microsoft.Maui.Devices.Sensors.Location
                {
                    Latitude = androidLocation.Latitude,
                    Longitude = androidLocation.Longitude,
                    Altitude = androidLocation.HasAltitude ? androidLocation.Altitude : null,
                    Speed = androidLocation.HasSpeed ? androidLocation.Speed : null
                };

                float speedKmh = (float)(location.Speed.GetValueOrDefault() * 3.6);
                LatitudeText = $"Latitude: {location.Latitude:F6}";
                LongitudeText = $"Longitude: {location.Longitude:F6}";
                AltitudeText = $"Altitude: {location.Altitude?.ToString("F1") ?? "N/A"} m";
                SpeedText = $"Speed: {speedKmh:F1} km/h";
                LastUpdateText = $"Last Update: {updateTime:HH:mm:ss}";

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await Shell.Current.DisplayAlert("Location Update",
                            $"Lat: {location.Latitude:F6}, Lon: {location.Longitude:F6}, " +
                            $"Alt: {location.Altitude?.ToString("F1") ?? "N/A"}m, Speed: {speedKmh:F1} km/h, " +
                            $"Last Update: {updateTime:HH:mm:ss}", "OK");
                        Log.Debug("MainPage", "Displayed alert");
                    }
                    catch (Exception ex)
                    {
                        Log.Error("MainPage", $"DisplayAlert error: {ex.Message}\n{ex.StackTrace}");
                        await DisplayAlert("Error", "Failed to display location.", "OK");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error("MainPage", $"Message handler error: {ex.Message}\n{ex.StackTrace}");
            }
        });
    }

    private async void OnCounterClicked(object sender, EventArgs e)
    {
        if (count < 1)
        {
            Debug.WriteLine("MainPage: OnCounterClicked started");
            await StartLocationService();
            Debug.WriteLine("MainPage: OnCounterClicked completed");
        }
        else
        {
            Debug.WriteLine("MainPage: Location Service already called");
        }
        count++;

        if (count == 1)
            CounterBtn.Text = $"Clicked {count} time";
        else
            CounterBtn.Text = $"Clicked {count} times";

        SemanticScreenReader.Announce(CounterBtn.Text);
    }

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
            var startServiceIntent = new Intent(context, typeof(AviationApp.Services.LocationService));
            context.StartForegroundService(startServiceIntent);
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
            var stopServiceIntent = new Intent(context, typeof(AviationApp.Services.LocationService));
            context.StopService(stopServiceIntent);
            Debug.WriteLine("MainPage: StopService called");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainPage: StopLocationService failed: {ex}");
        }
    }

    public new event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}