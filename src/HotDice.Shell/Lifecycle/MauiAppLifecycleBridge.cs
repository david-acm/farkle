using Farkle.Client.Lifecycle;

namespace HotDice.Shell.Lifecycle;

/// <summary>
/// The only thing the shell contributes to the realtime lifecycle: translating MAUI's window
/// activation events into the platform-free rules in <see cref="GameSessionLifecycle"/>.
/// </summary>
/// <remarks>
/// Deliberately trivial. Every decision — when to reconnect, when to re-sync, what to do when a
/// resume beats the network back — lives in the shared core where desktop unit tests can reach it
/// (ADR 0010). If this class ever grows a branch, that branch belongs on the other side of the seam.
/// </remarks>
public sealed class MauiAppLifecycleBridge(GameSessionLifecycle lifecycle)
{
    public void Attach(Window window)
    {
        window.Activated   += async (_, _) => await lifecycle.OnForegroundedAsync();
        window.Deactivated += async (_, _) => await lifecycle.OnBackgroundedAsync();
        window.Stopped     += async (_, _) => await lifecycle.OnBackgroundedAsync();
        window.Resumed     += async (_, _) => await lifecycle.OnForegroundedAsync();
    }
}
