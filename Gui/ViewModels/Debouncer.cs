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

        CancellationTokenSource localCts = new(); // Create a new CTS for this specific run
        cts = localCts; // store it so it may be canceled by subsequent calls
        var token = localCts.Token; // Use the token from the local CTS to prevent a race condition in case field is reassigned before this runs

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
            finally
            {
                localCts.Dispose(); // dispose after use
                if (ReferenceEquals(cts, localCts)) cts = null; // clear field only if it's the latest - not from more recent calls
            }
        });
    }

    internal void Cancel()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }
}
