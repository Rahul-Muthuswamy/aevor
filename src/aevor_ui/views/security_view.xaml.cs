using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Aevor.UI.ViewModels;

namespace Aevor.UI.Views;

public partial class SecurityView : UserControl
{
    private bool _wasLoading = false;

    public SecurityView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SecurityViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is SecurityViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            _wasLoading = newVm.IsLoading;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SecurityViewModel.IsLoading))
        {
            if (sender is SecurityViewModel vm)
            {

                if (_wasLoading && !vm.IsLoading)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        var storyboard = (Storyboard)FindResource("FadeInResults");
                        storyboard.Begin(this, true);
                    });
                }
                _wasLoading = vm.IsLoading;
            }
        }
    }
}
