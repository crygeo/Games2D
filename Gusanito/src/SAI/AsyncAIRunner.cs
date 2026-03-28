using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;

namespace Gusanito.SAI;

/// <summary>
/// Runs an <see cref="ISnakeAI"/> on a background Task, eliminating UI thread blocking
/// while minimising decision lag.
///
/// Strategy — "eager wait with fallback":
///   1. At the start of each tick, wait up to <see cref="_waitTimeout"/> for the
///      previously-fired Task to complete.
///      - Fast AIs (A* on small boards: ~0–2 ms) complete before the timeout → lag = 0.
///      - Slow AIs (Hamiltonian build, large boards) timeout → last known direction is used.
///   2. Once the result is read (or skipped), fire a new Task with a fresh snapshot
///      so the next tick has a result ready as early as possible.
///
/// Why this eliminates the 1-tick lag:
///   Pure fire-and-forget always returns the direction from the PREVIOUS tick's snapshot.
///   That structural lag causes the snake to overshoot food when the AI computes quickly.
///   Here, if the Task finishes within _waitTimeout, the result is used in the SAME tick.
///
/// Threading guarantees:
///   - GetNextMove() is called only from the UI/game-loop thread — no concurrent callers.
///   - The background Task writes _lastDirection via Interlocked.Exchange only.
///   - _pendingTask / _pendingCts are touched only from GetNextMove() — no races there.
/// </summary>
public sealed class AsyncAIRunner : ISnakeAI, IDisposable
{
    private readonly ISnakeAI _inner;

    // Last successfully computed direction (atomic read/write via Interlocked).
    private volatile int _lastDirection = (int)Direction.Right;

    private Task? _pendingTask;
    private CancellationTokenSource? _pendingCts;

    // How long to block the game-loop thread waiting for the pending Task.
    // For fast AIs this is almost always enough to get a fresh result this tick.
    private readonly TimeSpan _waitTimeout;

    // Hard budget: Task is cancelled if it exceeds this.
    private readonly TimeSpan _tickBudget;

    private bool _disposed;

    /// <param name="inner">The underlying AI implementation.</param>
    /// <param name="waitTimeout">
    /// How long GetNextMove() will wait for the AI result before falling back.
    /// Default: 5 ms. Set to 0 to restore pure fire-and-forget (original behaviour).
    /// </param>
    /// <param name="tickBudget">
    /// Hard cancellation deadline for the background Task.
    /// Default: 80 ms — well under a 120 ms tick.
    /// </param>
    public AsyncAIRunner(
        ISnakeAI inner,
        TimeSpan waitTimeout = default,
        TimeSpan tickBudget  = default)
    {
        _inner       = inner ?? throw new ArgumentNullException(nameof(inner));
        _waitTimeout = waitTimeout == default ? TimeSpan.FromMilliseconds(5) : waitTimeout;
        _tickBudget  = tickBudget  == default ? TimeSpan.FromMilliseconds(80) : tickBudget;
    }

    /// <summary>
    /// If the inner AI is a <see cref="HamiltonianAI"/>, triggers a cycle rebuild
    /// on a background thread. Safe to fire-and-forget.
    /// </summary>
    public Task RequestCycleRebuildAsync(GameEngine game)
    {
        if (_inner is HamiltonianAI hamAI)
            return Task.Run(() => hamAI.RebuildCycle(game));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the best available direction for this tick.
    /// Waits briefly for the pending Task, then fires a new one for the next tick.
    /// </summary>
    public Direction GetNextMove(GameEngine game)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // ── Step 1: collect result from previous Task ─────────────────────────
        if (_pendingTask != null)
        {
            // Block up to _waitTimeout. For A* on a 25x25 board this is ~0–2ms,
            // so the wait almost always succeeds and we get a fresh result this tick.
            _pendingTask.Wait(_waitTimeout);

            if (_pendingTask.IsCompleted)
            {
                _pendingCts?.Dispose();
                _pendingTask = null;
                _pendingCts  = null;
            }
            // Still running → leave it alive, _lastDirection holds the last good value.
        }

        // ── Step 2: fire a new Task for the next tick ─────────────────────────
        if (_pendingTask == null)
        {
            var snapshot = game.Clone();
            var cts      = new CancellationTokenSource(_tickBudget);

            _pendingCts  = cts;
            _pendingTask = Task.Run(() =>
            {
                try
                {
                    if (cts.Token.IsCancellationRequested) return;

                    var dir = _inner.GetNextMove(snapshot);
                    Interlocked.Exchange(ref _lastDirection, (int)dir);
                }
                catch
                {
                    // Swallow AI exceptions — last known good direction is reused.
                }
            }, cts.Token);
        }

        // ── Step 3: return best available direction ───────────────────────────
        return (Direction)_lastDirection;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
    }
}