using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Aevor.UI.Views;

public partial class CloneWizardView : UserControl
{
    public CloneWizardView()
    {
        InitializeComponent();
    }

    private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled)
        {
            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            var parent = ((System.Windows.FrameworkElement)sender).Parent as UIElement;
            parent?.RaiseEvent(eventArg);
        }
    }
}
