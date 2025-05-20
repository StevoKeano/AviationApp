using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using Microsoft.Maui.Controls;
using System.Collections.Generic;

namespace AviationApp.Services;

[Service]
public class LocationService : Service, ILocationListener
{
    private LocationManager _locationManager;
    private const string ChannelId = "location_service";
    private const int NotificationId = 1001;

    public override void OnCreate()
    {
        base.OnCreate();
        _locationManager = (LocationManager)GetSystemService(Android.Content.Context.LocationService);

        CreateNotificationChannel();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, CreateNotification());
        RequestLocationUpdates();
        return StartCommandResult.Sticky;
    }

    private void RequestLocationUpdates()
    {
        if (_locationManager != null)
        {
            _locationManager.RequestLocationUpdates(LocationManager.GpsProvider, 5000, 10, this);
        }
    }

    public void OnLocationChanged(Android.Locations.Location location)
    {
        var speedKmh = location.Speed * 3.6f; // Convert m/s to km/h
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.DisplayAlert("Location Update",
                $"Lat: {location.Latitude}, Lon: {location.Longitude}, Alt: {location.Altitude}m, Speed: {speedKmh:F1} km/h", "OK");
        });
    }

    public void OnLocationChanged(IList<Android.Locations.Location> locations)
    {
        foreach (var location in locations)
        {
            OnLocationChanged(location);
        }
    }

    public void OnProviderDisabled(string? provider) { }
    public void OnProviderEnabled(string? provider) { }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }

    private Notification CreateNotification()
    {
        var notification = new Notification.Builder(this, ChannelId)
            .SetContentTitle("AviationApp Location Service")
            .SetContentText("Tracking location for aviation data")
            .SetSmallIcon(Resource.Drawable.abc_ic_arrow_drop_right_black_24dp) // Replace with your icon
            .SetOngoing(true)
            .Build();
        return notification;
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Location Service", NotificationImportance.Low);
            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            notificationManager?.CreateNotificationChannel(channel);
        }
    }
}