using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Aevor.Core.Models;

namespace Aevor.UI.Views
{

    public partial class CreateBackupWindow : Window
    {
        private readonly Func<BraveProfile, Task<bool>> _createBackupFunc;
        public BraveProfile? SelectedProfile { get; private set; }

        public CreateBackupWindow(IEnumerable<BraveProfile> profiles, Func<BraveProfile, Task<bool>> createBackupFunc)
        {
            InitializeComponent();
            ProfileComboBox.ItemsSource = profiles;
            ProfileComboBox.DisplayMemberPath = "DisplayName";
            ProfileComboBox.SelectedIndex = 0;
            _createBackupFunc = createBackupFunc;
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

        private async void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedProfile = ProfileComboBox.SelectedItem as BraveProfile;
            if (SelectedProfile == null) return;

            SelectionPanel.Visibility = Visibility.Collapsed;
            FooterBorder.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            HeaderGrid.IsEnabled = false;

            try
            {
                bool success = await _createBackupFunc(SelectedProfile);
                if (success)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {

                    SelectionPanel.Visibility = Visibility.Visible;
                    FooterBorder.Visibility = Visibility.Visible;
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    HeaderGrid.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                SelectionPanel.Visibility = Visibility.Visible;
                FooterBorder.Visibility = Visibility.Visible;
                LoadingPanel.Visibility = Visibility.Collapsed;
                HeaderGrid.IsEnabled = true;
                MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
