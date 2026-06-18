using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Aevor.Application.Interfaces;

namespace Aevor.UI.Views;

public partial class SetupPasswordWindow : Window
{
    private readonly IMasterPasswordService _masterPasswordService;

    // Brushes cached once
    private static readonly SolidColorBrush BrushDanger  = new(Color.FromRgb(239,  68,  68)); // #EF4444
    private static readonly SolidColorBrush BrushWarning = new(Color.FromRgb(245, 158,  11)); // #F59E0B
    private static readonly SolidColorBrush BrushSuccess = new(Color.FromRgb( 16, 185, 129)); // #10B981
    private static readonly SolidColorBrush BrushBorder  = new(Color.FromRgb(229, 231, 235)); // #E5E7EB
    private static readonly SolidColorBrush BrushFocus   = new(Color.FromRgb(203, 108, 230)); // #CB6CE6

    public SetupPasswordWindow(IMasterPasswordService masterPasswordService)
    {
        _masterPasswordService = masterPasswordService;
        InitializeComponent();

        // Allow window dragging via the root border
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
    }

    // ── Password strength ────────────────────────────────────────────────

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var pw = PasswordBox.Password;

        // Toggle placeholder
        PwPlaceholder.Visibility = pw.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Strength calculation
        var (value, label, brush) = EvaluateStrength(pw);

        StrengthBar.Value      = value;
        StrengthBar.Foreground = brush;
        StrengthLabel.Text     = label;
        StrengthLabel.Foreground = brush;

        // Clear stale validation
        HideValidation();
    }

    private static (double value, string label, SolidColorBrush brush) EvaluateStrength(string pw)
    {
        if (pw.Length < 8)
            return (25, "Weak", BrushDanger);

        bool hasMixed = Regex.IsMatch(pw, "[A-Z]") && Regex.IsMatch(pw, "[a-z]");
        bool hasSpecial = Regex.IsMatch(pw, @"[^A-Za-z0-9]") || Regex.IsMatch(pw, @"\d");

        if (hasMixed && hasSpecial && pw.Length >= 8)
            return (100, "Strong", BrushSuccess);

        if (hasMixed)
            return (75, "Good", BrushWarning);

        return (50, "Fair", BrushWarning);
    }

    // ── Confirm password ──────────────────────────────────────────────────

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ConfirmPlaceholder.Visibility =
            ConfirmBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        HideValidation();
    }

    // ── Focus: change border colour ───────────────────────────────────────

    private void PwBox_GotFocus(object sender, RoutedEventArgs e)    => PwBorder.BorderBrush      = BrushFocus;
    private void PwBox_LostFocus(object sender, RoutedEventArgs e)   => PwBorder.BorderBrush      = BrushBorder;
    private void ConfirmBox_GotFocus(object sender, RoutedEventArgs e) => ConfirmBorder.BorderBrush = BrushFocus;
    private void ConfirmBox_LostFocus(object sender, RoutedEventArgs e) => ConfirmBorder.BorderBrush = BrushBorder;

    // ── Submit ────────────────────────────────────────────────────────────

    private async void SetupBtn_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        var confirm  = ConfirmBox.Password;

        // ── Validate ──
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowValidation("Please enter a master password.");
            return;
        }
        if (password.Length < 8)
        {
            ShowValidation("Password must be at least 8 characters.");
            return;
        }
        if (password != confirm)
        {
            ShowValidation("Passwords do not match.");
            return;
        }

        // ── Disable UI while working ──
        SetupBtn.IsEnabled = false;
        SetupBtn.Content   = "Creating…";

        try
        {
            await _masterPasswordService.SetupPasswordAsync(password);

            // Zero WPF's internal copy as best-effort
            PasswordBox.Clear();
            ConfirmBox.Clear();

            // Hand off to LaunchMainApplication — which shows MainWindow
            ((App)System.Windows.Application.Current).LaunchMainApplication();
            Close();
        }
        catch (Exception ex)
        {
            ShowValidation("Setup failed: " + ex.Message);
            SetupBtn.IsEnabled = true;
            SetupBtn.Content   = "Create Master Password";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ShowValidation(string message)
    {
        ValidationText.Text       = message;
        ValidationText.Visibility = Visibility.Visible;
    }

    private void HideValidation()
    {
        ValidationText.Text       = string.Empty;
        ValidationText.Visibility = Visibility.Collapsed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();
}
