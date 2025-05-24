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
    private bool _autoActivateMonitoring; // New: Auto-activate monitoring

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

    public bool AutoActivateMonitoring // New: Property for toggle
    {
        get => _autoActivateMonitoring;
        set { _autoActivateMonitoring = value; OnPropertyChanged(); }
    }

    public OptionsPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Load saved settings or defaults
        MessageFrequency = Preferences.Get("MessageFrequency", 10f);
        ShowSkull = Preferences.Get("ShowSkull", false);
        WarningLabelText = Preferences.Get("WarningLabelText", "Drop below DMMS and DIE!");
        TtsAlertText = Preferences.Get("TtsAlertText", "SPEED CHECK");
        AutoActivateMonitoring = Preferences.Get("AutoActivateMonitoring", true); // New: Default true
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
        Preferences.Set("AutoActivateMonitoring", AutoActivateMonitoring); // New: Save setting

        await DisplayAlert("Success", "Options saved!", "OK");
        await Navigation.PopAsync();
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