using System.Collections.Concurrent;

namespace CarWashSaaS.Client.Core;

public sealed class ToastService : IToastService, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly List<ToastItem> _toasts = [];
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellations = new();
    private bool _disposed;

    public event Action? OnChange;

    public IReadOnlyList<ToastItem> Toasts
    {
        get
        {
            lock (_syncRoot)
            {
                return _toasts.ToList();
            }
        }
    }

    public void Show(string message, ToastLevel level = ToastLevel.Info, string? title = null, int durationMs = 4500)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var toast = new ToastItem(message, level, title, durationMs);
        lock (_syncRoot)
        {
            _toasts.Add(toast);
        }

        NotifyStateChanged();

        if (durationMs > 0)
        {
            var cts = new CancellationTokenSource();
            _cancellations[toast.Id] = cts;

            _ = ScheduleAutoDismissAsync(toast.Id, durationMs, cts.Token);
        }
    }

    public void ShowSuccess(string message, string? title = "Sucesso", int durationMs = 4000) =>
        Show(message, ToastLevel.Success, title, durationMs);

    public void ShowError(string message, string? title = "Erro", int durationMs = 5500) =>
        Show(message, ToastLevel.Error, title, durationMs);

    public void ShowWarning(string message, string? title = "Atenção", int durationMs = 4500) =>
        Show(message, ToastLevel.Warning, title, durationMs);

    public void ShowInfo(string message, string? title = "Aviso", int durationMs = 4000) =>
        Show(message, ToastLevel.Info, title, durationMs);

    public void Dismiss(Guid id)
    {
        bool removed;
        lock (_syncRoot)
        {
            removed = _toasts.RemoveAll(t => t.Id == id) > 0;
        }

        if (_cancellations.TryRemove(id, out var cts))
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch
            {
                // Ignore cancellation exceptions
            }
        }

        if (removed)
        {
            NotifyStateChanged();
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _toasts.Clear();
        }

        foreach (var key in _cancellations.Keys)
        {
            if (_cancellations.TryRemove(key, out var cts))
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch
                {
                    // Ignore cancellation exceptions
                }
            }
        }

        NotifyStateChanged();
    }

    private async Task ScheduleAutoDismissAsync(Guid id, int durationMs, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(durationMs, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                Dismiss(id);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when dismissed manually
        }
    }

    private void NotifyStateChanged()
    {
        if (_disposed) return;
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        Clear();
    }
}
