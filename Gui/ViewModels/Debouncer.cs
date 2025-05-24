namespace FomoCal.Gui.ViewModels;

internal class Debouncer(TimeSpan delay, Action action, Action<Exception> onError)
{
    private CancellationTokenSource? cts;

    internal void Run()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        var token = cts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, token);
                if (!token.IsCancellationRequested) MainThread.BeginInvokeOnMainThread(action);
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
