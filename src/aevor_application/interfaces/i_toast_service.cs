using System;

namespace Aevor.Application.Interfaces;

/// <summary>
/// Types of toast notification.
/// </summary>
public enum ToastType
{
    Success,
    Warning,
    Error,
    Info
}

/// <summary>
/// Immutable payload raised by <see cref="IToastService.ToastRequested"/>.
/// </summary>
public sealed class ToastNotification
{
    public string    Message    { get; init; } = string.Empty;
    public ToastType Type       { get; init; }
    public int       DurationMs { get; init; } = 3000;
}

/// <summary>
/// Shows non-blocking toast notifications in the bottom-right corner of MainWindow.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Displays a toast notification with the given <paramref name="message"/>, visual
    /// <paramref name="type"/>, and auto-dismiss duration.
    /// </summary>
    void Show(string message, ToastType type, int durationMs = 3000);

    /// <summary>
    /// Raised on the calling thread when <see cref="Show"/> is invoked.
    /// MainWindow subscribes to this to render the toast in the UI.
    /// </summary>
    event Action<ToastNotification>? ToastRequested;
}
