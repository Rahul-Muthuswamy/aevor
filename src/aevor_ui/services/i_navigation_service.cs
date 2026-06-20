using System;
using Aevor.UI.ViewModels;

namespace Aevor.UI.Services;

public interface INavigationService
{
    BaseViewModel? CurrentView { get; }
    event Action? NavigationChanged;
    void NavigateTo<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : BaseViewModel;
}
