#nullable enable

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;

public static class EditorProjectIoGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        return new Releaser();
    }

    public static bool TryEnter(out IDisposable? lease)
    {
        if (!Gate.Wait(0))
        {
            lease = null;
            return false;
        }

        lease = new Releaser();
        return true;
    }

    private sealed class Releaser : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
                Gate.Release();
        }
    }
}
