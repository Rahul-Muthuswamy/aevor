using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Aevor.Application.Interfaces;

namespace Aevor.UI.Views;

public partial class UnlockWindow : Window
{
    private readonly IMasterPasswordService _masterPasswordService;

    private int  _failedAttempts;
    private bool _isLockedOut;

    private static readonly SolidColorBrush BrushBorder = new(Color.FromRgb(229, 231, 235));
    private static readonly SolidColorBrush BrushFocus  = new(Color.FromRgb(203, 108, 230));

    private const int MaxAttempts    = 3;
    private const int CooldownSeconds = 30;

    public UnlockWindow(IMasterPasswordService masterPasswordService)
    {
        _masterPasswordService = masterPasswordService;
        InitializeComponent();

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

        Loaded += (_, _) => PasswordBox.Focus();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        PwPlaceholder.Visibility =
            PasswordBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        HideValidation();
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _ = TryUnlockAsync();
    }

    private void PwBox_GotFocus(object sender, RoutedEventArgs e)  => PwBorder.BorderBrush = BrushFocus;
    private void PwBox_LostFocus(object sender, RoutedEventArgs e) => PwBorder.BorderBrush = BrushBorder;

    private void UnlockBtn_Click(object sender, RoutedEventArgs e) => _ = TryUnlockAsync();

    private async Task TryUnlockAsync()
    {
        if (_isLockedOut)
            return;

        var password = PasswordBox.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowValidation("Please enter your master password.");
            return;
        }

        UnlockBtn.IsEnabled = false;
        UnlockBtn.Content   = "Verifying…";

        bool success;
        try
        {
            success = await _masterPasswordService.VerifyPasswordAsync(password);
        }
        catch (Exception ex)
        {
            ShowValidation("Verification error: " + ex.Message);
            UnlockBtn.IsEnabled = true;
            UnlockBtn.Content   = "Unlock Aevor";
            return;
        }
        finally
        {

            PasswordBox.Clear();
        }

        if (success)
        {

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
            return;
        }

        _failedAttempts++;
        ShowValidation("Incorrect password.");

        if (_failedAttempts >= MaxAttempts)
        {
            await StartLockoutAsync();
        }
        else
        {
            UnlockBtn.IsEnabled = true;
            UnlockBtn.Content   = "Unlock Aevor";
        }
    }

    private async Task StartLockoutAsync()
    {
        _isLockedOut        = true;
        UnlockBtn.IsEnabled = false;
        HideValidation();

        LockoutText.Visibility = Visibility.Visible;

        for (int remaining = CooldownSeconds; remaining > 0; remaining--)
        {
            LockoutText.Text = remaining == 1
                ? "Too many failed attempts. Wait 1 second…"
                : $"Too many failed attempts. Wait {remaining} seconds…";
            await Task.Delay(1000);
        }

        _isLockedOut           = false;
        _failedAttempts        = 0;
        LockoutText.Text       = string.Empty;
        LockoutText.Visibility = Visibility.Collapsed;
        UnlockBtn.IsEnabled    = true;
        UnlockBtn.Content      = "Unlock Aevor";
        PasswordBox.Focus();
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
