using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aevor.Application.Interfaces;

namespace Aevor.UI.Views;

public partial class MainWindow : Window
{
    private readonly IToastService? _toastService;

    public MainWindow()
    {
        InitializeComponent();

        // Resolve toast service from DI and subscribe
        _toastService = ((App)System.Windows.Application.Current).Services
            .GetService(typeof(IToastService)) as IToastService;

        if (_toastService != null)
        {
            _toastService.ToastRequested += OnToastRequested;
        }
    }

    // ── Profile flyout ────────────────────────────────────────────────────

    private void ProfileBtn_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = !ProfilePopup.IsOpen;
    }

    // Called by the Settings and Security buttons inside the flyout so the
    // popup closes immediately when the user selects a nav item.
    private void ProfilePopup_CloseOnNav(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
    }

    // ── Toast Notifications ───────────────────────────────────────────────

    private void OnToastRequested(ToastNotification toast)
    {
        // Marshal to UI thread
        Dispatcher.InvokeAsync(() => ShowToast(toast));
    }

    private void ShowToast(ToastNotification toast)
    {
        // Determine colors based on toast type
        var (barColorHex, circleBgHex, icon) = toast.Type switch
        {
            ToastType.Success => ("#10B981", "#D1FAE5", "✓"),
            ToastType.Warning => ("#F59E0B", "#FEF3C7", "⚠"),
            ToastType.Error   => ("#EF4444", "#FEE2E2", "✕"),
            _                 => ("#3B82F6", "#E0F2FE", "ℹ")
        };

        var barBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(barColorHex));
        var circleBgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(circleBgHex));
        var textBrush = System.Windows.Application.Current.TryFindResource("TextBrush") as Brush 
            ?? new SolidColorBrush(Color.FromRgb(31, 41, 55));

        // Build outer toast container
        var border = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 8),
            MinWidth = 280,
            MaxWidth = 400,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 2,
                Color = Colors.Black,
                Opacity = 0.12,
                Direction = 270
            }
        };

        var outerGrid = new Grid();
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Column 0: left colored bar 4px wide
        var leftBar = new Border
        {
            Background = barBrush,
            Width = 4,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetColumn(leftBar, 0);
        outerGrid.Children.Add(leftBar);

        // Column 1: Inner grid with Thickness(20, 16, 20, 16) padding
        var innerBorder = new Border
        {
            Padding = new Thickness(20, 16, 20, 16)
        };
        Grid.SetColumn(innerBorder, 1);
        outerGrid.Children.Add(innerBorder);

        var innerGrid = new Grid();
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Circular icon border
        var iconBorder = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = circleBgBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = barBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBorder.Child = iconText;
        Grid.SetColumn(iconBorder, 0);
        innerGrid.Children.Add(iconBorder);

        // Message text
        var msgText = new TextBlock
        {
            Text = toast.Message,
            Foreground = textBrush,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (System.Windows.Application.Current.TryFindResource("BodyTextBlockStyle") is Style bodyStyle)
        {
            msgText.Style = bodyStyle;
        }
        else
        {
            msgText.FontWeight = FontWeights.Medium;
            msgText.FontSize = 13;
        }
        Grid.SetColumn(msgText, 1);
        innerGrid.Children.Add(msgText);

        innerBorder.Child = innerGrid;
        border.Child = outerGrid;

        // Fade-in animation
        border.Opacity = 0;
        ToastContainer.Items.Insert(0, border);

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        border.BeginAnimation(OpacityProperty, fadeIn);

        // Auto-dismiss
        var duration = Math.Max(toast.DurationMs, 1000);
        Task.Delay(duration).ContinueWith(_ =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (__, ___) =>
                {
                    ToastContainer.Items.Remove(border);
                };
                border.BeginAnimation(OpacityProperty, fadeOut);
            });
        });
    }
}
