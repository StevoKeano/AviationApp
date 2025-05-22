using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Util;
using Microsoft.Maui.Controls;
using System.Collections.Generic;

namespace AviationApp.Services;

[Service(ForegroundServiceType = Android.Content.PM.ForegroundService.TypeLocation)]
public class LocationService : Service, ILocationListener
{
    private LocationManager _locationManager;
    private const string ChannelId = "location_service";
    private const int NotificationId = 1001;

    public override void OnCreate()
    {
        base.OnCreate();
        System.Diagnostics.Debug.WriteLine("LocationService: OnCreate started");
        try
        {
            _locationManager = GetSystemService(Android.Content.Context.LocationService) as LocationManager;
            if (_locationManager == null)
            {
                System.Diagnostics.Debug.WriteLine("LocationService: Failed to get LocationManager");
                StopSelf(); // Gracefully stop the service
                return;
            }

            CreateNotificationChannel();
            System.Diagnostics.Debug.WriteLine("LocationService: OnCreate completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LocationService: OnCreate failed: {ex}");
            StopSelf(); // Stop the service on error
        }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var notification = CreateNotification();
        StartForeground(NotificationId, notification);
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
            if (Shell.Current == null)
            {
                Log.Error("function OnLocationChanged aka LocationUpdate?", "Shell.Current is null");
                // Fallback: Log or notify via other means
                return;
            }
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
            .SetSmallIcon(Resource.Drawable.ic_notification) // Replace with your icon
            .SetOngoing(true)
            .Build();
        return notification;
    }

    private void CreateNotificationChannel()
    {
        System.Diagnostics.Debug.WriteLine("LocationService: Creating notification channel");
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(ChannelId, "Location Service", NotificationImportance.Low);
                var notificationManager = GetSystemService(NotificationService) as NotificationManager;
                if (notificationManager != null)
                {
                    notificationManager.CreateNotificationChannel(channel);
                    System.Diagnostics.Debug.WriteLine("LocationService: Notification channel created");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("LocationService: NotificationManager is null");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LocationService: CreateNotificationChannel failed: {ex}");
        }
    }
}