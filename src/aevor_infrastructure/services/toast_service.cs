using System;
using Aevor.Application.Interfaces;

namespace Aevor.Infrastructure.Services;

/// <summary>
/// Lightweight toast notification relay.  
/// Calls to <see cref="Show"/> raise <see cref="ToastRequested"/>;
/// MainWindow subscribes and renders the UI element.
/// </summary>
public sealed class ToastService : IToastService
{
    public event Action<ToastNotification>? ToastRequested;

    public void Show(string message, ToastType type, int durationMs = 3000)
    {
        ToastRequested?.Invoke(new ToastNotification
        {
            Message    = message,
            Type       = type,
            DurationMs = durationMs
        });
    }
}
