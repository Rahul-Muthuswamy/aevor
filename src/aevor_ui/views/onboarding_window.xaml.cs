using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Aevor.UI.Views;

public partial class OnboardingWindow : Window
{
    private int _currentSlide;
    private const int TotalSlides = 3;

    private static readonly SolidColorBrush ActiveDot =
        new(Color.FromRgb(203, 108, 230));
    private static readonly SolidColorBrush InactiveDot =
        new(Color.FromRgb(229, 231, 235));

    static OnboardingWindow()
    {
        ActiveDot.Freeze();
        InactiveDot.Freeze();
    }

    public OnboardingWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        };
        UpdateSlide();
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSlide < TotalSlides - 1)
        {
            _currentSlide++;
            UpdateSlide();
        }
        else
        {

            PersistOnboardingFlag();
            ((App)System.Windows.Application.Current).LaunchMainApplication();
            Close();
        }
    }

    private void UpdateSlide()
    {
        Slide1.Visibility = _currentSlide == 0 ? Visibility.Visible : Visibility.Collapsed;
        Slide2.Visibility = _currentSlide == 1 ? Visibility.Visible : Visibility.Collapsed;
        Slide3.Visibility = _currentSlide == 2 ? Visibility.Visible : Visibility.Collapsed;

        Dot1.Fill = _currentSlide == 0 ? ActiveDot : InactiveDot;
        Dot2.Fill = _currentSlide == 1 ? ActiveDot : InactiveDot;
        Dot3.Fill = _currentSlide == 2 ? ActiveDot : InactiveDot;

        NextBtn.Content = _currentSlide == TotalSlides - 1 ? "Get Started" : "Next";
    }

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aevor",
        "settings.json");

    public static bool HasCompletedOnboarding()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return false;

            var json = File.ReadAllText(SettingsFilePath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("hasCompletedOnboarding", out var prop))
                return prop.GetBoolean();
        }
        catch
        {

        }
        return false;
    }

    private static void PersistOnboardingFlag()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Dictionary<string, JsonElement>? existing = null;
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var raw = File.ReadAllText(SettingsFilePath);
                    existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw);
                }
                catch
                {

                }
            }

            existing ??= new Dictionary<string, JsonElement>();
            existing["hasCompletedOnboarding"] = JsonSerializer.SerializeToElement(true);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(existing, options);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {

        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {

        PersistOnboardingFlag();
        ((App)System.Windows.Application.Current).LaunchMainApplication();
        Close();
    }
}
