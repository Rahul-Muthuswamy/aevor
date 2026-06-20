using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Aevor.Application.Interfaces;

namespace Aevor.UI.Views;

public partial class SetupPasswordWindow : Window
{
    private readonly IMasterPasswordService _masterPasswordService;

    private static readonly SolidColorBrush BrushDanger  = new(Color.FromRgb(239,  68,  68));
    private static readonly SolidColorBrush BrushWarning = new(Color.FromRgb(245, 158,  11));
    private static readonly SolidColorBrush BrushSuccess = new(Color.FromRgb( 16, 185, 129));
    private static readonly SolidColorBrush BrushBorder  = new(Color.FromRgb(229, 231, 235));
    private static readonly SolidColorBrush BrushFocus   = new(Color.FromRgb(203, 108, 230));

    public SetupPasswordWindow(IMasterPasswordService masterPasswordService)
    {
        _masterPasswordService = masterPasswordService;
        InitializeComponent();

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var pw = PasswordBox.Password;

        PwPlaceholder.Visibility = pw.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        var (value, label, brush) = EvaluateStrength(pw);

        StrengthBar.Value      = value;
        StrengthBar.Foreground = brush;
        StrengthLabel.Text     = label;
        StrengthLabel.Foreground = brush;

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

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ConfirmPlaceholder.Visibility =
            ConfirmBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        HideValidation();
    }

    private void PwBox_GotFocus(object sender, RoutedEventArgs e)    => PwBorder.BorderBrush      = BrushFocus;
    private void PwBox_LostFocus(object sender, RoutedEventArgs e)   => PwBorder.BorderBrush      = BrushBorder;
    private void ConfirmBox_GotFocus(object sender, RoutedEventArgs e) => ConfirmBorder.BorderBrush = BrushFocus;
    private void ConfirmBox_LostFocus(object sender, RoutedEventArgs e) => ConfirmBorder.BorderBrush = BrushBorder;

    private async void SetupBtn_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        var confirm  = ConfirmBox.Password;

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

        SetupBtn.IsEnabled = false;
        SetupBtn.Content   = "Creating…";

        try
        {
            await _masterPasswordService.SetupPasswordAsync(password);

            PasswordBox.Clear();
            ConfirmBox.Clear();

            if (!OnboardingWindow.HasCompletedOnboarding())
            {
                var onboarding = new OnboardingWindow();
                onboarding.Show();
            }
            else
            {
                ((App)System.Windows.Application.Current).LaunchMainApplication();
            }
            Close();
        }
        catch (Exception ex)
        {
            ShowValidation("Setup failed: " + ex.Message);
            SetupBtn.IsEnabled = true;
            SetupBtn.Content   = "Create Master Password";
        }
    }

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
