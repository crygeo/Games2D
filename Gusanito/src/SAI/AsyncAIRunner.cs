using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;

namespace Gusanito.SAI;

/// <summary>
/// Runs an <see cref="ISnakeAI"/> on a background Task each game tick,
/// returning the last known good direction if computation hasn't finished yet.
///
/// Design decisions:
/// - Never blocks the UI thread. GetNextMove() returns immediately.
/// - Uses CancellationToken per tick: if the AI takes longer than the tick budget,
///   the previous decision is reused and the task is cancelled.
/// - The AI result is stored atomically via Interlocked so no lock is needed on the read path.
/// - Implements ISnakeAI so it is a drop-in replacement for any synchronous AI.
///
/// Lifecycle:
///   1. Construct with an inner ISnakeAI.
///   2. Call RequestCycleRebuild() after NewGame() if the inner AI needs board initialization.
///   3. Call GetNextMove() every tick from the game loop — always returns instantly.
///   4. Dispose to cancel any pending background work.
/// </summary>
public sealed class AsyncAIRunner : ISnakeAI, IDisposable
{
    private readonly ISnakeAI _inner;

    // Last direction computed successfully. Volatile int maps to Direction enum values.
    private volatile int _lastDirection = (int)Direction.Right;

    // Currently running computation task (may be null if idle)
    private Task? _pendingTask;
    private CancellationTokenSource? _pendingCts;

    // Per-tick budget: if the AI takes longer than this, the tick uses the last known direction.
    private readonly TimeSpan _tickBudget;

    private bool _disposed;

    /// <param name="inner">The underlying AI implementation.</param>
    /// <param name="tickBudget">
    /// Maximum time per tick for the AI to compute a move.
    /// Default is 80ms — generous for most tick rates while ensuring UI safety.
    /// </param>
    public AsyncAIRunner(ISnakeAI inner, TimeSpan tickBudget = default)
    {
        _inner      = inner ?? throw new ArgumentNullException(nameof(inner));
        _tickBudget = tickBudget == default ? TimeSpan.FromMilliseconds(80) : tickBudget;
    }

    /// <summary>
    /// If the inner AI is a <see cref="HamiltonianAI"/>, triggers a cycle rebuild
    /// on a background thread. Returns the Task so callers can await if needed.
    /// Safe to fire-and-forget.
    /// </summary>
    public Task RequestCycleRebuildAsync(GameEngine game)
    {
        if (_inner is HamiltonianAI hamAI)
        {
            return Task.Run(() => hamAI.RebuildCycle(game));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the best known direction without blocking.
    /// Fires a background task to compute the next direction for the *following* tick.
    /// </summary>
    public Direction GetNextMove(GameEngine game)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // If previous computation is done, read its result
        if (_pendingTask is { IsCompleted: true })
        {
            _pendingTask = null;
            _pendingCts?.Dispose();
            _pendingCts = null;
        }

        // Fire next computation if not already running
        if (_pendingTask == null)
        {
            // Clone the game state so the background task works on a snapshot,
            // not the live state that the UI thread will mutate next tick.
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
                    // Swallow exceptions from the AI — last known good direction will be used.
                }
            }, cts.Token);
        }

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