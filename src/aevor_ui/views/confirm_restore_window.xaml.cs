using System;
using System.Windows;
using System.Windows.Input;

namespace Aevor.UI.Views
{
    /// <summary>
    /// Interaction logic for ConfirmRestoreWindow.xaml
    /// </summary>
    public partial class ConfirmRestoreWindow : Window
    {
        public ConfirmRestoreWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
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

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
