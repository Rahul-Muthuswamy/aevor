using System;
using System.Windows;
using System.Windows.Input;

namespace Aevor.UI.Views
{
    /// <summary>
    /// Interaction logic for ConfirmDeleteWindow.xaml
    /// </summary>
    public partial class ConfirmDeleteWindow : Window
    {
        private readonly Func<Task<bool>> _deleteAction;

        public ConfirmDeleteWindow(string message, Func<Task<bool>> deleteAction)
        {
            InitializeComponent();
            MessageText.Text = message;
            _deleteAction = deleteAction;
        }

        private void HeaderGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch to loading state
            MessagePanel.Visibility = Visibility.Collapsed;
            FooterBorder.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            HeaderGrid.IsEnabled = false; // Disable close/drag during deletion

            try
            {
                bool success = await _deleteAction();
                if (success)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    // Restore original view
                    MessagePanel.Visibility = Visibility.Visible;
                    FooterBorder.Visibility = Visibility.Visible;
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    HeaderGrid.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessagePanel.Visibility = Visibility.Visible;
                FooterBorder.Visibility = Visibility.Visible;
                LoadingPanel.Visibility = Visibility.Collapsed;
                HeaderGrid.IsEnabled = true;
                MessageBox.Show($"Delete failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
