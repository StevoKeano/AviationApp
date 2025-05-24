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
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;

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
    private string dmmsText;
    private string warningLabelText;
    private bool isActive = false;
    private ButtonState buttonState = ButtonState.Paused;
    private bool isFlashing = false;
    private bool showSkullWarning = false;
    private Color pageBackground = Colors.Transparent;
    private CancellationTokenSource flashingCts = null;
    private CancellationTokenSource ttsCts = null;
    private Task _flashingTask = null;
    private Task _ttsTask = null;
    private readonly object _flashLock = new object();
    private DateTime _lastFlashUpdate = DateTime.MinValue;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(100);
    private int _originalMediaVolume = -1;
    private string ttsAlertText;
    private float messageFrequency;
    private bool showSkull;
    private bool autoActivateMonitoring; // New: Auto-activate DMMS monitoring
    private bool suppressWarningsUntilAboveDmms; // New: Suppress warnings at startup

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
        set
        {
            if (dmmsText != value)
            {
                dmmsText = value;
                OnPropertyChanged();
                Preferences.Set("DmmsValue", value);
                Log.Debug("MainPage", $"Saved DmmsText to Preferences: {value}");
            }
        }
    }

    public string WarningLabelText
    {
        get => warningLabelText;
        set { warningLabelText = value; OnPropertyChanged(); }
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

    public bool IsFlashing
    {
        get => isFlashing;
        set
        {
            isFlashing = value;
            OnPropertyChanged();
            UpdateSkullVisibility();
            Log.Debug("MainPage", $"IsFlashing set to: {isFlashing}");
        }
    }

    public bool ShowSkullWarning
    {
        get => showSkullWarning;
        set
        {
            showSkullWarning = value;
            OnPropertyChanged();
            Log.Debug("MainPage", $"ShowSkullWarning set to: {showSkullWarning}");
        }
    }

    private void UpdateSkullVisibility()
    {
        ShowSkullWarning = IsFlashing && showSkull;
        Log.Debug("MainPage", $"UpdateSkullVisibility - IsFlashing: {IsFlashing}, ShowSkull: {showSkull}, ShowSkullWarning: {ShowSkullWarning}");
    }

    public MainPage()
    {
        // Load settings at startup
        dmmsText = Preferences.Get("DmmsValue", "70");
        warningLabelText = Preferences.Get("WarningLabelText", "Drop below DMMS and DIE!");
        ttsAlertText = Preferences.Get("TtsAlertText", "SPEED CHECK, YOUR GONNA FALL OUTTA THE SKY LIKE A PIANO");
        messageFrequency = Preferences.Get("MessageFrequency", 5f);
        showSkull = Preferences.Get("ShowSkull", false);
        autoActivateMonitoring = Preferences.Get("AutoActivateMonitoring", true); // New: Default true
        showSkullWarning = false;
        suppressWarningsUntilAboveDmms = true; // New: Initialize to suppress until speed check
        Log.Debug("MainPage", $"Loaded settings at startup - DmmsText: {dmmsText}, WarningLabelText: {warningLabelText}, TtsAlertText: {ttsAlertText}, MessageFrequency: {messageFrequency}, ShowSkull: {showSkull}, AutoActivateMonitoring: {autoActivateMonitoring}, ShowSkullWarning: {showSkullWarning}, SuppressWarnings: {suppressWarningsUntilAboveDmms}");

        InitializeComponent();
        BindingContext = this;

        OnPropertyChanged(nameof(DmmsText));
        OnPropertyChanged(nameof(WarningLabelText));
        OnPropertyChanged(nameof(ShowSkullWarning));

        // Test TTS on startup
        Task.Run(async () =>
        {
            try
            {
                await TextToSpeech.Default.SpeakAsync("TTS Test", new SpeechOptions { Volume = 1.0f }, CancellationToken.None);
                Log.Debug("MainPage", "Startup TTS test played successfully");
            }
            catch (Exception ex)
            {
                Log.Error("MainPage", $"Startup TTS test failed: {ex.Message}\n{ex.StackTrace}");
            }
        });

        WeakReferenceMessenger.Default.Register<LocationMessage>(this, async (recipient, message) =>
        {
            try
            {
                if (DateTime.Now - _lastFlashUpdate < _debounceInterval)
                {
                    return;
                }
                _lastFlashUpdate = DateTime.Now;

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

                float speedKnots = (float)(location.Speed.GetValueOrDefault() / 0.514444);
                double? altitudeFeet = location.Altitude.HasValue ? location.Altitude.Value * 3.28084 : null;

                LatitudeText = $"Latitude: {location.Latitude:F6}";
                LongitudeText = $"Longitude: {location.Longitude:F6}";
                AltitudeText = $"Altitude: {altitudeFeet?.ToString("F1") ?? "N/A"} ft";
                SpeedText = $"Speed: {speedKnots:F1} knots";
                LastUpdateText = $"Last Update: {updateTime:HH:mm:ss}";

                float speedKmh = (float)(location.Speed.GetValueOrDefault() * 3.6);
                Debug.WriteLine($"MainPage: Location Update - Lat: {location.Latitude:F6}, Lon: {location.Longitude:F6}, Alt: {location.Altitude?.ToString("F1") ?? "N/A"}m ({altitudeFeet?.ToString("F1") ?? "N/A"}ft), Speed: {speedKmh:F1} km/h ({speedKnots:F1} knots), Time: {updateTime:HH:mm:ss}");
                Log.Debug("MainPage", $"Location Update - Lat: {location.Latitude:F6}, Lon: {location.Longitude:F6}, Alt: {location.Altitude?.ToString("F1") ?? "N/A"}m ({altitudeFeet?.ToString("F1") ?? "N/A"}ft), Speed: {speedKmh:F1} km/h ({speedKnots:F1} knots), Time: {updateTime:HH:mm:ss}");

                float dmmsKnots = 0f;
                bool isDmmsValid = IsActive && float.TryParse(DmmsText, out dmmsKnots) && dmmsKnots > 0;

                // Update warning suppression based on speed
                if (isDmmsValid && speedKnots > dmmsKnots && suppressWarningsUntilAboveDmms)
                {
                    suppressWarningsUntilAboveDmms = false;
                    Log.Debug("MainPage", "Speed exceeded DMMS, enabling normal alerts");
                }

                // Trigger alerts only if not suppressed
                if (isDmmsValid && speedKnots < dmmsKnots && !suppressWarningsUntilAboveDmms)
                {
                    if (!IsFlashing)
                    {
                        IsFlashing = true;
                        await StartFlashingBackground();
                        BringAppToForeground();
                    }
                }
                else if (IsFlashing)
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

    private async void OnOptionsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new OptionsPage());
    }

    private async void OnCounterClicked(object sender, EventArgs e)
    {
        if (count < 1 || !IsActive)
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

        if (IsFlashing)
        {
            await StopFlashingBackground();
        }

        SemanticScreenReader.Announce(CounterBtn.Text);
    }

    private async void OnButtonTapped(object sender, EventArgs e)
    {
        await CounterBtnBorder.ScaleTo(0.95, 100);
        await CounterBtnBorder.ScaleTo(1.0, 100);
    }

    private async Task StartFlashingBackground()
    {
        lock (_flashLock)
        {
            if (IsFlashing && flashingCts != null && !flashingCts.IsCancellationRequested)
            {
                Log.Debug("MainPage", "Flashing already active, skipping start");
                return;
            }
            flashingCts?.Dispose();
            ttsCts?.Dispose();
            flashingCts = new CancellationTokenSource();
            ttsCts = new CancellationTokenSource();
            IsFlashing = true;
            Log.Debug("MainPage", "New CancellationTokenSource created for flashing and TTS");
        }

        // Set media volume to maximum (Android-specific)
#if ANDROID
        try
        {
            var audioManager = (Android.Media.AudioManager)Android.App.Application.Context.GetSystemService(Context.AudioService);
            _originalMediaVolume = audioManager.GetStreamVolume(Android.Media.Stream.Music);
            int maxVolume = audioManager.GetStreamMaxVolume(Android.Media.Stream.Music);
            audioManager.SetStreamVolume(Android.Media.Stream.Music, maxVolume, 0);
            Log.Debug("MainPage", $"Captured original media volume: {_originalMediaVolume}, set to maximum: {maxVolume}");
        }
        catch (Exception ex)
        {
            Log.Error("MainPage", $"Failed to capture or set media volume: {ex.Message}\n{ex.StackTrace}");
        }
#endif

        // Start flashing and TTS concurrently
        Log.Debug("MainPage", $"Starting flashing and TTS tasks with TtsAlertText: {ttsAlertText}, MessageFrequency: {messageFrequency}");
        _flashingTask = Task.Run(async () =>
        {
            try
            {
                if (flashingCts == null)
                {
                    Log.Error("MainPage", "Flashing task started with null flashingCts");
                    return;
                }
                while (!flashingCts.Token.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(() => PageBackground = Colors.Red.WithAlpha(0.8f));
                    await Task.Delay(500, flashingCts.Token);
                    MainThread.BeginInvokeOnMainThread(() => PageBackground = Colors.Transparent);
                    await Task.Delay(500, flashingCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                Log.Debug("MainPage", "Flashing task cancelled");
            }
            catch (Exception ex)
            {
                Log.Error("MainPage", $"Flashing error: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                lock (_flashLock)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        PageBackground = Colors.Transparent;
                        IsFlashing = false;
                    });
                    flashingCts?.Dispose();
                    flashingCts = null;
                    _flashingTask = null;
                    Log.Debug("MainPage", "Flashing task cleaned up");
                }
            }
        }, flashingCts.Token);

        _ttsTask = Task.Run(async () =>
        {
            try
            {
                if (ttsCts == null)
                {
                    Log.Error("MainPage", "TTS task started with null ttsCts");
                    return;
                }
                Log.Debug("MainPage", "Starting TTS loop");
                // Immediate TTS playback to align with flashing
                await TextToSpeech.Default.SpeakAsync(
                    ttsAlertText,
                    new SpeechOptions { Volume = 1.0f },
                    ttsCts.Token);
                Log.Debug("MainPage", "TTS: Initial message played at maximum volume");
                while (!ttsCts.Token.IsCancellationRequested)
                {
                    await Task.Delay((int)(messageFrequency * 1000), ttsCts.Token);
                    if (!ttsCts.Token.IsCancellationRequested)
                    {
                        Log.Debug("MainPage", $"Attempting to play TTS: {ttsAlertText}");
                        await TextToSpeech.Default.SpeakAsync(
                            ttsAlertText,
                            new SpeechOptions { Volume = 1.0f },
                            ttsCts.Token);
                        Log.Debug("MainPage", "TTS: Played message at maximum volume");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Log.Debug("MainPage", "TTS task cancelled");
            }
            catch (Exception ex)
            {
                Log.Error("MainPage", $"TTS error: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                lock (_flashLock)
                {
                    ttsCts?.Dispose();
                    ttsCts = null;
                    _ttsTask = null;
                    Log.Debug("MainPage", "TTS task cleaned up");
                }
            }
        }, ttsCts.Token);
    }

    private async Task StopFlashingBackground()
    {
        lock (_flashLock)
        {
            if (flashingCts != null)
            {
                flashingCts.Cancel();
                flashingCts.Dispose();
                flashingCts = null;
                Log.Debug("MainPage", "Flashing CancellationTokenSource cancelled and disposed");
            }
            if (ttsCts != null)
            {
                ttsCts.Cancel();
                ttsCts.Dispose();
                ttsCts = null;
                Log.Debug("MainPage", "TTS CancellationTokenSource cancelled and disposed");
            }
        }

        if (_flashingTask != null)
        {
            await _flashingTask;
        }
        if (_ttsTask != null)
        {
            await _ttsTask;
        }

        // Restore original media volume (Android-specific)
#if ANDROID
        try
        {
            if (_originalMediaVolume != -1)
            {
                var audioManager = (Android.Media.AudioManager)Android.App.Application.Context.GetSystemService(Context.AudioService);
                audioManager.SetStreamVolume(Android.Media.Stream.Music, _originalMediaVolume, 0);
                Log.Debug("MainPage", $"Restored media volume to original: {_originalMediaVolume}");
                _originalMediaVolume = -1;
            }
        }
        catch (Exception ex)
        {
            Log.Error("MainPage", $"Failed to restore media volume: {ex.Message}\n{ex.StackTrace}");
        }
#endif

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsFlashing = false;
            PageBackground = Colors.Transparent;
        });
        Log.Debug("MainPage", "Flashing and TTS stopped");
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
            Log.Error("MainPage", $"Failed to bring app to foreground: {ex.Message}\n{ex.StackTrace}");
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Reload settings live
        warningLabelText = Preferences.Get("WarningLabelText", "Drop below DMMS and DIE!");
        ttsAlertText = Preferences.Get("TtsAlertText", "SPEED CHECK, YOUR GONNA FALL OUTTA THE SKY LIKE A PIANO");
        messageFrequency = Preferences.Get("MessageFrequency", 5f);
        showSkull = Preferences.Get("ShowSkull", false);
        autoActivateMonitoring = Preferences.Get("AutoActivateMonitoring", true);
        ShowSkullWarning = IsFlashing && showSkull;
        OnPropertyChanged(nameof(WarningLabelText));
        OnPropertyChanged(nameof(ShowSkullWarning));
        Log.Debug("MainPage", $"OnAppearing - Reloaded settings: WarningLabelText: {warningLabelText}, TtsAlertText: {ttsAlertText}, MessageFrequency: {messageFrequency}, ShowSkull: {showSkull}, AutoActivateMonitoring: {autoActivateMonitoring}, ShowSkullWarning: {ShowSkullWarning}, SuppressWarnings: {suppressWarningsUntilAboveDmms}");

        // Auto-activate DMMS monitoring if enabled
        if (autoActivateMonitoring && !IsActive)
        {
            Log.Debug("MainPage", "Auto-activating DMMS monitoring");
            await StartLocationService();
            IsActive = true;
            ButtonState = ButtonState.Active;
            CounterBtnBorder.Background = (Brush)Resources["ActiveGradient"];
            count = 1;
            CounterBtn.Text = "DMMS Monitoring Started";
            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }
}