using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Aevor.Core.Models;

namespace Aevor.UI.Views
{

    public partial class ApplyTemplateWindow : Window
    {
        private readonly Func<BraveProfile, bool, Task<string?>> _applyFunc;
        public BraveProfile? SelectedProfile { get; private set; }

        public ApplyTemplateWindow(
            string templateName,
            IEnumerable<BraveProfile> profiles,
            bool defaultSafeBackup,
            Func<BraveProfile, bool, Task<string?>> applyFunc)
        {
            InitializeComponent();
            TemplateInfoText.Text = $"Apply '{templateName}' to profile:";
            ProfileComboBox.ItemsSource = profiles;
            ProfileComboBox.DisplayMemberPath = "DisplayName";
            ProfileComboBox.SelectedIndex = 0;
            BackupCheckBox.IsChecked = defaultSafeBackup;
            _applyFunc = applyFunc;
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

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedProfile = ProfileComboBox.SelectedItem as BraveProfile;
            if (SelectedProfile == null) return;

            bool doBackup = BackupCheckBox.IsChecked == true;

            SelectionPanel.Visibility = Visibility.Collapsed;
            FooterBorder.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            HeaderGrid.IsEnabled = false;

            try
            {
                string? errorMessage = await _applyFunc(SelectedProfile, doBackup);
                if (errorMessage == null)
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
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                SelectionPanel.Visibility = Visibility.Visible;
                FooterBorder.Visibility = Visibility.Visible;
                LoadingPanel.Visibility = Visibility.Collapsed;
                HeaderGrid.IsEnabled = true;
                MessageBox.Show($"Apply failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
