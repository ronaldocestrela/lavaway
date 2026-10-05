namespace CarWashSaaS.Client.Core;

public interface IToastService
{
    event Action? OnChange;
    IReadOnlyList<ToastItem> Toasts { get; }

    void Show(string message, ToastLevel level = ToastLevel.Info, string? title = null, int durationMs = 4500);
    void ShowSuccess(string message, string? title = "Sucesso", int durationMs = 4000);
    void ShowError(string message, string? title = "Erro", int durationMs = 5500);
    void ShowWarning(string message, string? title = "Atenção", int durationMs = 4500);
    void ShowInfo(string message, string? title = "Aviso", int durationMs = 4000);
    void Dismiss(Guid id);
    void Clear();
}
