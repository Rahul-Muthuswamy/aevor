using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Aevor.UI.Views;

public partial class OnboardingWindow : Window
{
    private int _currentSlide; // 0-based
    private const int TotalSlides = 3;

    private static readonly SolidColorBrush ActiveDot =
        new(Color.FromRgb(203, 108, 230));   // PrimaryBrush #CB6CE6
    private static readonly SolidColorBrush InactiveDot =
        new(Color.FromRgb(229, 231, 235));   // BorderBrush #E5E7EB

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

    // ── Navigation ────────────────────────────────────────────────────────

    private void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSlide < TotalSlides - 1)
        {
            _currentSlide++;
            UpdateSlide();
        }
        else
        {
            // Last slide → persist flag and launch main app
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

    // ── Persistence ───────────────────────────────────────────────────────

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
            // Corrupt or missing — treat as not completed
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

            // Merge with existing settings if present
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
                    // Overwrite if corrupt
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
            // Non-fatal — worst case, user sees onboarding again
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Skip = complete onboarding and launch
        PersistOnboardingFlag();
        ((App)System.Windows.Application.Current).LaunchMainApplication();
        Close();
    }
}
