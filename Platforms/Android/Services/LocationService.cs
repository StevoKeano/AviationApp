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
                locationManager.RequestLocationUpdates(LocationManager.GpsProvider, 1000, 1, new LocationListener(this));
            }
            else
            {
                Log.Error("LocationService", "GPS provider is disabled");
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
                Log.Error("LocationService", "Failed to create notification");
                return StartCommandResult.Sticky;
            }

            StartForeground(1, notification);
            return StartCommandResult.Sticky;
        }
        catch (Exception ex)
        {
            Log.Error("LocationService", $"OnStartCommand error: {ex.Message}\n{ex.StackTrace}");
            throw;
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

    private class LocationListener : Java.Lang.Object, ILocationListener
    {
        private readonly LocationService service;

        public LocationListener(LocationService service)
        {
            this.service = service;
        }

        public void OnLocationChanged(Location location)
        {
            if (location == null)
            {
                Log.Error("LocationService", "Received null location");
                return;
            }

            Log.Debug("LocationService", $"Location updated: Lat={location.Latitude}, Lon={location.Longitude}");
            WeakReferenceMessenger.Default.Send(new LocationMessage(location, DateTime.Now));
            service.SendLocationToPrinter(location);
        }

        public void OnProviderDisabled(string provider) { }
        public void OnProviderEnabled(string provider) { }
        public void OnStatusChanged(string provider, Availability status, Bundle extras) { }
    }

    private async void SendLocationToPrinter(Location location)
    {
        try
        {
            using var httpClient = new HttpClient();
            var url = "http://192.168.137.100/api/location"; // Adjust to printer’s API
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