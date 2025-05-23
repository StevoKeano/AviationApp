using Android.Content;
using Android.Locations;
using Android.Util;
using AviationApp.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics.Platform;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Android.App;
using Android.Content.PM;

namespace AviationApp;

public enum ButtonState { Active, Paused, Failed }

public partial class MainPage : ContentPage, INotifyPropertyChanged
{
    private int count = 0;
    private string latitudeText = "Latitude: N/A";
    private string longitudeText = "Longitude: N/A";
    private string altitudeText = "Altitude: N/A";
    private string speedText = "Speed: N/A";
    private string lastUpdateText = "Last Update: N/A";
    private string dmmsText = "";
    private bool isActive = false;
    private ButtonState buttonState = ButtonState.Paused;
    private bool isFlashing = false;
    private Color pageBackground = Colors.Transparent;
    private CancellationTokenSource flashingCts = null;

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

    public string DmmsText
    {
        get => dmmsText;
        set { dmmsText = value; OnPropertyChanged(); }
    }

    public bool IsActive
    {
        get => isActive;
        set { isActive = value; OnPropertyChanged(); }
    }

    public ButtonState ButtonState
    {
        get => buttonState;
        set { buttonState = value; OnPropertyChanged(); }
    }

    public Color PageBackground
    {
        get => pageBackground;
        set { pageBackground = value; OnPropertyChanged(); }
    }

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
        WeakReferenceMessenger.Default.Register<LocationMessage>(this, async (recipient, message) =>
        {
            try
            {
                Log.Debug("MainPage", $"Received LocationMessage at {DateTime.Now:HH:mm:ss}");
                var androidLocation = message.Location;
                var updateTime = message.UpdateTime;
                if (androidLocation == null)
                {
                    Log.Error("MainPage", "Received null location");
                    return;
                }

                var location = new Microsoft.Maui.Devices.Sensors.Location
                {
                    Latitude = androidLocation.Latitude,
                    Longitude = androidLocation.Longitude,
                    Altitude = androidLocation.HasAltitude ? androidLocation.Altitude : null,
                    Speed = androidLocation.HasSpeed ? androidLocation.Speed : null
                };

                // Convert speed from m/s to knots (1 knot = 0.514444 m/s)
                float speedKnots = (float)(location.Speed.GetValueOrDefault() / 0.514444);
                // Convert altitude from meters to feet (1 meter = 3.28084 feet)
                double? altitudeFeet = location.Altitude.HasValue ? location.Altitude.Value * 3.28084 : null;

                // Update UI labels (latitude/longitude not displayed per XAML)
                LatitudeText = $"Latitude: {location.Latitude:F6}";
                LongitudeText = $"Longitude: {location.Longitude:F6}";
                AltitudeText = $"Altitude: {altitudeFeet?.ToString("F1") ?? "N/A"} ft";
                SpeedText = $"Speed: {speedKnots:F1} knots";
                LastUpdateText = $"Last Update: {updateTime:HH:mm:ss}";

                // Log location info (km/h and meters for GPX, knots and feet for clarity)
                float speedKmh = (float)(location.Speed.GetValueOrDefault() * 3.6);
                Debug.WriteLine($"MainPage: Location Update - Lat: {location.Latitude:F6}, Lon: {location.Longitude:F6}, Alt: {location.Altitude?.ToString("F1") ?? "N/A"}m ({altitudeFeet?.ToString("F1") ?? "N/A"}ft), Speed: {speedKmh:F1} km/h ({speedKnots:F1} knots), Time: {updateTime:HH:mm:ss}");
                Log.Debug("MainPage", $"Location Update - Lat: {location.Latitude:F6}, Lon: {location.Longitude:F6}, Alt: {location.Altitude?.ToString("F1") ?? "N/A"}m ({altitudeFeet?.ToString("F1") ?? "N/A"}ft), Speed: {speedKmh:F1} km/h ({speedKnots:F1} knots), Time: {updateTime:HH:mm:ss}");

                // Check DMMS and flash background if speed is below
                float dmmsKnots = 0f; // Initialize to safe default
                bool isDmmsValid = IsActive && float.TryParse(DmmsText, out dmmsKnots) && dmmsKnots > 0;
                if (isDmmsValid && speedKnots < dmmsKnots)
                {
                    if (!isFlashing)
                    {
                        isFlashing = true;
                        await StartFlashingBackground();
                        BringAppToForeground();
                    }
                }
                else if (isFlashing && (!isDmmsValid || speedKnots >= dmmsKnots || !IsActive))
                {
                    await StopFlashingBackground();
                }
            }
            catch (Exception ex)
            {
                Log.Error("MainPage", $"Message handler error: {ex.Message}\n{ex.StackTrace}");
                Debug.WriteLine($"MainPage: Message handler error: {ex.Message}\n{ex.StackTrace}");
            }
        });
    }

    private async void OnCounterClicked(object sender, EventArgs e)
    {
        if (count < 1 || !isActive)
        {
            Debug.WriteLine("MainPage: OnCounterClicked started");
            await StartLocationService();
            Debug.WriteLine("MainPage: OnCounterClicked completed");
            IsActive = true;
            ButtonState = ButtonState.Active;
            CounterBtnBorder.Background = (Brush)Resources["ActiveGradient"];
            count = 1;
        }
        else
        {
            Debug.WriteLine("MainPage: Location Service already called");
            if (StopLocationService() == 0)
            {                
                IsActive = false;
                ButtonState = ButtonState.Paused;
                CounterBtnBorder.Background = (Brush)Resources["PausedGradient"];
            }
            else
            {
                ButtonState = ButtonState.Failed;
                CounterBtn.Text = $"Pause failed, please click again. Tried {count} times...";
                CounterBtnBorder.Background = (Brush)Resources["FailedGradient"];
            }
        }
        if (count == 1)
        {
            CounterBtn.Text = $"DMMS Monitoring Started";
        }
        else
        {
            if (ButtonState == ButtonState.Paused)
            {
                CounterBtn.Text = $"=== P A U S E D ===";
                CounterBtn.TextColor = Microsoft.Maui.Graphics.Color.FromRgba("#FFFFFF");
            }
            else if (ButtonState == ButtonState.Failed)
            {
                CounterBtn.Text = $"Pause failed, please click again. Tried {count} times...";
            }
        }

        // Stop flashing on button tap
        if (isFlashing)
        {
            await StopFlashingBackground();
        }

        SemanticScreenReader.Announce(CounterBtn.Text);
    }

    private async void OnButtonTapped(object sender, EventArgs e)
    {
        // Scale animation for 3D button press
        await CounterBtnBorder.ScaleTo(0.95, 100);
        await CounterBtnBorder.ScaleTo(1.0, 100);
    }

    private async Task StartFlashingBackground()
    {
        flashingCts = new CancellationTokenSource();
        try
        {
            while (!flashingCts.Token.IsCancellationRequested)
            {
                PageBackground = Colors.Red.WithAlpha(0.8f);
                await Task.Delay(500, flashingCts.Token);
                PageBackground = Colors.Transparent;
                await Task.Delay(500, flashingCts.Token);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when cancelled
        }
        finally
        {
            PageBackground = Colors.Transparent;
        }
    }

    private async Task StopFlashingBackground()
    {
        if (flashingCts != null)
        {
            flashingCts.Cancel();
            flashingCts.Dispose();
            flashingCts = null;
        }
        isFlashing = false;
        PageBackground = Colors.Transparent;
        await Task.CompletedTask;
    }

    private void BringAppToForeground()
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                Log.Error("MainPage", "Current activity is null");
                return;
            }

            // Check if app is in background
            var packageManager = activity.PackageManager;
            var intent = packageManager.GetLaunchIntentForPackage(activity.PackageName);
            if (intent != null)
            {
                intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
                activity.StartActivity(intent);
                Log.Debug("MainPage", "Brought app to foreground");
            }
            else
            {
                Log.Error("MainPage", "Failed to get launch intent");
            }
        }
        catch (Exception ex)
        {
            Log.Error("MainPage", $"Failed to bring app to foreground: {ex.Message}");
            Debug.WriteLine($"MainPage: Failed to bring app to foreground: {ex.Message}");
        }
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
                Debug.WriteLine("MainPage: Permission Denied - Location permission is required for tracking.");
                Log.Debug("MainPage", "Permission Denied - Location permission is required for tracking.");
                return;
            }

            var context = Android.App.Application.Context;
            var startServiceIntent = new Intent(context, typeof(AviationApp.Services.LocationService));
            context.StartForegroundService(startServiceIntent);
            Debug.WriteLine("MainPage: StartForegroundService called");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainPage: Failed to start service: {ex.Message}");
            Log.Debug("MainPage", $"Failed to start service: {ex.Message}");
        }
    }

    private int StopLocationService()
    {
        Debug.WriteLine("MainPage: StopLocationService started");
        try
        {
            var context = Android.App.Application.Context;
            var stopServiceIntent = new Intent(context, typeof(AviationApp.Services.LocationService));
            context.StopService(stopServiceIntent);
            Debug.WriteLine("MainPage: StopService called");
            return 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainPage: StopLocationService failed: {ex.Message}");
            Log.Debug("MainPage", $"StopLocationService failed: {ex.Message}");
            return -1;
        }
    }

    public new event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}