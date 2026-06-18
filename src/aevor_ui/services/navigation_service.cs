using System;
using Microsoft.Extensions.DependencyInjection;
using Aevor.UI.ViewModels;

namespace Aevor.UI.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private BaseViewModel? _currentView;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public BaseViewModel? CurrentView
    {
        get => _currentView;
        private set
        {
            if (_currentView != value)
            {
                _currentView = value;
                NavigationChanged?.Invoke();
            }
        }
    }

    public event Action? NavigationChanged;

    public void NavigateTo<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : BaseViewModel
    {
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        configure?.Invoke(viewModel);
        CurrentView = viewModel;
    }
}
