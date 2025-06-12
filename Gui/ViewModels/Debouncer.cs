namespace FomoCal.Gui.ViewModels;

internal class Debouncer
{
    private readonly TimeSpan delay;
    private readonly Func<Task>? asyncAction;
    private readonly Action? syncAction;
    private readonly Action<Exception> onError;
    private CancellationTokenSource? cts;

    internal Debouncer(TimeSpan delay, Action syncAction, Action<Exception> onError)
    {
        this.delay = delay;
        this.syncAction = syncAction;
        this.onError = onError;
    }

    internal Debouncer(TimeSpan delay, Func<Task> asyncAction, Action<Exception> onError)
    {
        this.delay = delay;
        this.asyncAction = asyncAction;
        this.onError = onError;
    }

    internal void Run()
    {
        cts?.Cancel(); // cancel any previous waiting task
        cts?.Dispose();
        cts = new CancellationTokenSource();
        var token = cts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, token);
                if (token.IsCancellationRequested) return;

                if (syncAction is not null) MainThread.BeginInvokeOnMainThread(syncAction);
                else if (asyncAction is not null) MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try { await asyncAction(); }
                    catch (Exception ex) { onError.Invoke(ex); }
                });
            }
            catch (Exception ex)
            {
                // raise Error if not debounced
                if (ex is not TaskCanceledException || !token.IsCancellationRequested) onError.Invoke(ex);
            }
        });
    }

    internal void Cancel() => cts?.Cancel();
}
