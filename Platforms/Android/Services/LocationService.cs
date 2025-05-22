using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Util;
using AndroidX.Core.App;
using CommunityToolkit.Mvvm.Messaging;
using System.Net.Http.Json;
using Location = Android.Locations.Location;

namespace AviationApp.Services;

[Service(ForegroundServiceType = Android.Content.PM.ForegroundService.TypeLocation)]
public class LocationService : Service
{
    private LocationManager locationManager;
    private Notification notification;
    private LocationListener locationListener;

    public override void OnCreate()
    {
        base.OnCreate();
        Log.Debug("LocationService", "OnCreate called");
        try
        {
            locationManager = GetSystemService(LocationService) as LocationManager;
            if (locationManager == null)
            {
                Log.Error("LocationService", "LocationManager is null");
                return;
            }

            if (locationManager.IsProviderEnabled(LocationManager.GpsProvider))
            {
                locationListener = new LocationListener(this);
                locationManager.RequestLocationUpdates(LocationManager.GpsProvider, 1000, 1, locationListener);
                Log.Debug("LocationService", "Requested location updates with GPS provider");
            }
            else
            {
                Log.Error("LocationService", "GPS provider is disabled");
                if (locationManager.IsProviderEnabled(LocationManager.NetworkProvider))
                {
                    locationListener = new LocationListener(this);
                    locationManager.RequestLocationUpdates(LocationManager.NetworkProvider, 1000, 1, locationListener);
                    Log.Debug("LocationService", "Requested location updates with network provider");
                }
                else
                {
                    Log.Error("LocationService", "Network provider is also disabled");
                }
            }

            notification = CreateNotification();
            if (notification == null)
            {
                Log.Error("LocationService", "Failed to create notification");
            }
        }
        catch (Exception ex)
        {
            Log.Error("LocationService", $"OnCreate error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        Log.Debug("LocationService", "OnStartCommand called");
        try
        {
            if (notification == null)
            {
                Log.Warn("LocationService", "Notification is null, recreating");
                notification = CreateNotification();
            }

            if (notification == null)
            {
                Log.Error("LocationService", "Failed to create notification, cannot start foreground service");
                return StartCommandResult.Sticky;
            }

            StartForeground(1, notification);
            Log.Debug("LocationService", "Foreground service started");
            return StartCommandResult.Sticky;
        }
        catch (Exception ex)
        {
            Log.Error("LocationService", $"OnStartCommand error: {ex.Message}\n{ex.StackTrace}");
            return StartCommandResult.Sticky;
        }
    }

    private Notification CreateNotification()
    {
        try
        {
            var channelId = "LocationServiceChannel";
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(channelId, "Location Service", NotificationImportance.Low);
                var notificationManager = GetSystemService(NotificationService) as NotificationManager;
                if (notificationManager == null)
                {
                    Log.Error("LocationService", "NotificationManager is null");
                    return null;
                }
                notificationManager.CreateNotificationChannel(channel);
            }

            var builder = new NotificationCompat.Builder(this, channelId)
                .SetContentTitle("Location Service")
                .SetContentText("Tracking location")
                .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
                .SetPriority((int)NotificationPriority.Low);

            return builder.Build();
        }
        catch (Exception ex)
        {
            Log.Error("LocationService", $"Notification creation failed: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    public override IBinder OnBind(Intent intent)
    {
        return null;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (locationManager != null && locationListener != null)
        {
            locationManager.RemoveUpdates(locationListener);
            Log.Debug("LocationService", "Removed location updates");
        }
    }

    private class LocationListener : Java.Lang.Object, ILocationListener
    {
        private readonly LocationService service;

        public LocationListener(LocationService service)
        {
            this.service = service;
            Log.Debug("LocationService", "LocationListener created");
        }

        public void OnLocationChanged(Location location)
        {
            try
            {
                if (location == null)
                {
                    Log.Error("LocationService", "Received null location");
                    return;
                }

                Log.Debug("LocationService", $"Location updated: Lat={location.Latitude}, Lon={location.Longitude}, Time={DateTime.Now:HH:mm:ss}");
                WeakReferenceMessenger.Default.Send(new LocationMessage(location, DateTime.Now));
                Log.Debug("LocationService", "Sent LocationMessage");
                // Disabled to avoid crash
                // service.SendLocationToPrinter(location);
            }
            catch (Exception ex)
            {
                Log.Error("LocationService", $"OnLocationChanged error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void OnProviderDisabled(string provider)
        {
            Log.Error("LocationService", $"Provider disabled: {provider}");
        }

        public void OnProviderEnabled(string provider)
        {
            Log.Debug("LocationService", $"Provider enabled: {provider}");
        }

        public void OnStatusChanged(string provider, Availability status, Bundle extras)
        {
            Log.Debug("LocationService", $"Status changed: {provider}, {status}");
        }
    }

    private async void SendLocationToPrinter(Location location)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = "http://192.168.137.100/api/location";
            var data = new { location.Latitude, location.Longitude, location.Altitude };
            var response = await httpClient.PostAsJsonAsync(url, data);
            Log.Debug("LocationService", $"Sent location to printer: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Log.Error("LocationService", $"Printer network error: {ex.Message}\n{ex.StackTrace}");
        }
    }
}

public class LocationMessage
{
    public Location Location { get; }
    public DateTime UpdateTime { get; }
    public LocationMessage(Location location, DateTime updateTime)
    {
        Location = location;
        UpdateTime = updateTime;
    }
}