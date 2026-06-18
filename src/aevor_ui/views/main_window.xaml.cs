using System.Windows;
using System.Windows.Controls.Primitives;

namespace Aevor.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
}
