using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Aevor.Core.Models;

namespace Aevor.UI.Views
{
    /// <summary>
    /// Interaction logic for CreateBackupWindow.xaml
    /// </summary>
    public partial class CreateBackupWindow : Window
    {
        public BraveProfile? SelectedProfile { get; private set; }

        public CreateBackupWindow(IEnumerable<BraveProfile> profiles)
        {
            InitializeComponent();
            ProfileComboBox.ItemsSource = profiles;
            ProfileComboBox.DisplayMemberPath = "DisplayName";
            ProfileComboBox.SelectedIndex = 0;
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

        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedProfile = ProfileComboBox.SelectedItem as BraveProfile;
            DialogResult = true;
            Close();
        }
    }
}
