using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AviationApp;

public partial class OptionsPage : ContentPage, INotifyPropertyChanged
{
    private float _messageFrequency;
    private bool _showSkull;
    private string _warningLabelText;
    private string _ttsAlertText;

    public float MessageFrequency
    {
        get => _messageFrequency;
        set { _messageFrequency = value; OnPropertyChanged(); }
    }

    public bool ShowSkull
    {
        get => _showSkull;
        set { _showSkull = value; OnPropertyChanged(); }
    }

    public string WarningLabelText
    {
        get => _warningLabelText;
        set { _warningLabelText = value; OnPropertyChanged(); }
    }

    public string TtsAlertText
    {
        get => _ttsAlertText;
        set { _ttsAlertText = value; OnPropertyChanged(); }
    }

    public OptionsPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Load saved settings or defaults
        MessageFrequency = Preferences.Get("MessageFrequency", 10f); // Default 10 seconds
        ShowSkull = Preferences.Get("ShowSkull", false); // Default off
        WarningLabelText = Preferences.Get("WarningLabelText", "Drop below DMMS and DIE!"); // Default from MainPage
        TtsAlertText = Preferences.Get("TtsAlertText", "SPEED CHECK"); // Default from MainPage
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // Validate MessageFrequency
        if (!float.TryParse(MessageFrequencyEntry.Text, out float frequency) || frequency <= 0)
        {
            await DisplayAlert("Error", "Please enter a valid frequency (seconds > 0).", "OK");
            return;
        }

        // Save settings
        Preferences.Set("MessageFrequency", frequency);
        Preferences.Set("ShowSkull", ShowSkull);
        Preferences.Set("WarningLabelText", WarningLabelText);
        Preferences.Set("TtsAlertText", TtsAlertText);

        await DisplayAlert("Success", "Options saved!", "OK");
        await Navigation.PopAsync(); // Return to MainPage
    }

    public new event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        });
    }
}