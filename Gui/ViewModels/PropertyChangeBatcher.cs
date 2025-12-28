namespace FomoCal.Gui.ViewModels;

public sealed class PropertyChangeBatcher(Action<string> raise)
{
    private readonly Action<string> raise = raise ?? throw new ArgumentNullException(nameof(raise));
    private int level;
    private HashSet<string>? pending;

    public IDisposable Defer()
    {
        level++;
        pending ??= [];
        return new Scope(this);
    }

    public void Notify(string propertyName)
    {
        if (level > 0)
        {
            pending!.Add(propertyName);
            return;
        }

        raise(propertyName);
    }

    private void End()
    {
        if (--level > 0) return;

        if (pending is { Count: > 0 })
        {
            foreach (var prop in pending)
                raise(prop);

            pending.Clear();
        }
    }

    private sealed partial class Scope(PropertyChangeBatcher owner) : IDisposable
    {
        public void Dispose() => owner.End();
    }
}
