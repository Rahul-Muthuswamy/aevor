using System;

namespace Aevor.Application.Interfaces;

public enum ToastType
{
    Success,
    Warning,
    Error,
    Info
}

public sealed class ToastNotification
{
    public string    Message    { get; init; } = string.Empty;
    public ToastType Type       { get; init; }
    public int       DurationMs { get; init; } = 3000;
}

public interface IToastService
{

    void Show(string message, ToastType type, int durationMs = 3000);

    event Action<ToastNotification>? ToastRequested;
}
