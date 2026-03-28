using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;

namespace Gusanito.SAI;

/// <summary>
/// Synchronous AI runner — calls the inner AI directly on the game-loop thread.
///
/// Use this when the AI computes fast enough to fit within a single game tick
/// without perceptible stall (A* on boards up to ~40x40 is typically under 2ms).
///
/// Replaces AsyncAIRunner when async overhead (Task allocation, cloning, scheduling)
/// is not justified by the AI's actual computation time.
///
/// Implements ISnakeAI so it is a drop-in replacement for AsyncAIRunner in MainVM.
/// </summary>
public sealed class SyncAIRunner : ISnakeAI
{
    private readonly ISnakeAI _inner;

    public SyncAIRunner(ISnakeAI inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>
    /// If the inner AI is a <see cref="HamiltonianAI"/>, triggers a synchronous cycle rebuild.
    /// Call this after NewGame() — it may block for several seconds on large boards.
    /// For large boards, run this on a background thread manually and show a loading state.
    /// </summary>
    public void RebuildCycle(GameEngine game)
    {
        if (_inner is HamiltonianAI hamAI)
            hamAI.RebuildCycle(game);
    }

    /// <inheritdoc />
    public Direction GetNextMove(GameEngine game)
    {
        var snapshow = game.Clone();
        return _inner.GetNextMove(snapshow);
    }
}